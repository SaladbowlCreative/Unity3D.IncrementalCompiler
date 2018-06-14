using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.ServiceModel;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.LayoutRenderers;
using NLog.LayoutRenderers.Wrappers;

namespace IncrementalCompiler
{
    partial class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "-dev")
            {
                return RunAsDev(args);
            }
            else if (args.Length > 0 && args[0] == "-server")
            {
                return RunAsServer(args);
            }
            else if (args.Length > 0 && args[0] != "/?")
            {
                return RunAsClient(args);
            }
            else
            {
                ShowUsage();
                return 1;
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Unity3D Incremental C# Compiler using Roslyn");
            Console.WriteLine("https://github.com/SaladLab/Unity3D.IncrementalCompiler/");
            Console.WriteLine("");
            Console.WriteLine("* Client");
            Console.WriteLine("  -out:<file>        Specifies the output file name.");
            Console.WriteLine("  -r:<file>          References metadata from the specified assembly files.");
            Console.WriteLine("  -define:<file>     Defines conditional compilation symbols.");
            Console.WriteLine("  @<file>            Reads a response file for more options.");
            Console.WriteLine("");
            Console.WriteLine("* Server");
            Console.WriteLine("  -server processid  Spawn server for specified process.");
            Console.WriteLine("");
        }

        private static int RunAsClient(string[] args)
        {
            SetupLogger("IncrementalCompiler.log", false);

            var logger = LogManager.GetLogger("Client");
            logger.Info("Started");

            Settings settings;
            try
            {
                settings = Settings.Load() ?? Settings.Default;
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed in loading settings.");
                return 1;
            }

            var currentPath = Directory.GetCurrentDirectory();
            var options = new CompileOptions();
            options.ParseArgument(args);
            options.WorkDirectory = currentPath;
            options.References = options.References.Distinct().ToList();
            options.Files = options.Files.Distinct().ToList();
            options.DebugSymbolFile = settings.DebugSymbolFile;
            options.PrebuiltOutputReuse = settings.PrebuiltOutputReuse;

            logger.Info("CurrentDir: {0}", Directory.GetCurrentDirectory());
            logger.Info("Output: {0}", options.Output);

            if (string.IsNullOrEmpty(options.Output))
            {
                logger.Error("No output");
                return 1;
            }

            // Get unity process ID

            var parentProcessId = 0;
            var pd = options.Defines.FirstOrDefault(d => d.StartsWith("__UNITY_PROCESSID__"));
            if (pd != null)
            {
                int.TryParse(pd.Substring(19), out parentProcessId);
            }
            else
            {
                var parentProcess = Process.GetProcessesByName("Unity").FirstOrDefault();
                if (parentProcess != null)
                    parentProcessId = parentProcess.Id;
            }

            if (parentProcessId == 0)
            {
                logger.Error("No parent process");
                return 1;
            }

            logger.Info("Parent process ID: {0}", parentProcessId);

            // Run

            var useCompilationServer = true;
            // it does not work on mac for some reason
            useCompilationServer &= PlatformHelper.CurrentPlatform == Platform.Windows;

            Process serverProcess = null;
            while (true)
            {
                try
                {
                    var w = new Stopwatch();
                    w.Start();
                    logger.Info("Request to server");
                    var result = CompilerServiceClient.Request(parentProcessId, currentPath, options, useCompilationServer);
                    w.Stop();
                    logger.Info("Done: Succeeded={0}. Duration={1}sec.", result.Succeeded, w.Elapsed.TotalSeconds);
                    Console.WriteLine("Compile {0}. (Duration={1}sec)", result.Succeeded ? "succeeded" : "failed",
                                                                        w.Elapsed.TotalSeconds);
                    foreach (var warning in result.Warnings)
                    {
                        logger.Info(warning);
                        Console.Error.WriteLine(warning);
                    }
                    foreach (var error in result.Errors)
                    {
                        logger.Info(error);
                        Console.Error.WriteLine(error);
                    }
                    return result.Succeeded ? 0 : 1;
                }
                catch (EndpointNotFoundException)
                {
                    if (serverProcess == null)
                    {
                        logger.Info("Spawn server");
                        serverProcess = Process.Start(
                            new ProcessStartInfo
                            {
                                FileName = Assembly.GetEntryAssembly().Location,
                                Arguments = "-server " + parentProcessId,
                                WindowStyle = ProcessWindowStyle.Hidden
                            });
                        Thread.Sleep(100);
                    }
                    else
                    {
                        if (serverProcess.HasExited == false)
                            Thread.Sleep(100);
                        else
                            return 1;
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error in request");
                    Console.Error.WriteLine("Internal error: " + e);
                    return 1;
                }
            }
        }

        private static int RunAsServer(string[] args)
        {
            SetupLogger("IncrementalCompiler-Server.log", false);

            var logger = LogManager.GetLogger("Server");
            logger.Info("Started");

            var parentProcessId = 0;
            if (args.Length >= 2 && int.TryParse(args[1], out parentProcessId) == false)
            {
                logger.Error("Error in parsing parentProcessId (arg={0})", args[1]);
                return 1;
            }

            return CompilerServiceServer.Run(logger, parentProcessId);
        }

        private static void SetupLogger(string fileName, bool useConsole)
        {
            InitNLogConfigurationItemFactory();
            var config = new LoggingConfiguration();

            if (useConsole)
            {
                var consoleTarget = new ColoredConsoleTarget
                {
                    Layout = @"${time}|${logger}|${message}|${exception:format=tostring}"
                };
                config.AddTarget("console", consoleTarget);
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
            }

            var logDirectory = Directory.Exists(".\\Temp") ? ".\\Temp\\" : ".\\";
            var fileTarget = new FileTarget
            {
                FileName = logDirectory + fileName,
                Layout = @"${longdate} ${uppercase:${level}}|${logger}|${message}|${exception:format=tostring}"
            };
            config.AddTarget("file", fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

            LogManager.Configuration = config;
        }

        private static void InitNLogConfigurationItemFactory()
        {
            // Default initialization code for ConfigurationItemFactory.Default spends
            // almost 0.5 sec in il-packed executable. (it scans whole types in assembly to find plugin types)
            // To avoid this slow-down, manual initialization is written.
            // If you need another layout-renderer, filter or anything else in NLog assembly,
            // please insert register code here.

            var factory = new ConfigurationItemFactory(new Assembly[0]);
            factory.LayoutRenderers.RegisterDefinition("time", typeof(TimeLayoutRenderer));
            factory.LayoutRenderers.RegisterDefinition("longdate", typeof(LongDateLayoutRenderer));
            factory.LayoutRenderers.RegisterDefinition("level", typeof(LevelLayoutRenderer));
            factory.LayoutRenderers.RegisterDefinition("logger", typeof(LoggerNameLayoutRenderer));
            factory.LayoutRenderers.RegisterDefinition("message", typeof(MessageLayoutRenderer));
            factory.LayoutRenderers.RegisterDefinition("exception", typeof(ExceptionLayoutRenderer));
            factory.LayoutRenderers.RegisterDefinition("uppercase", typeof(UppercaseLayoutRendererWrapper));
            ConfigurationItemFactory.Default = factory;
        }
    }
}
