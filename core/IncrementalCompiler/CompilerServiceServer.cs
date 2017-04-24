using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using NLog;

namespace IncrementalCompiler
{
    public class CompilerServiceServer
    {

        private static ServiceHost serviceHost;

        public static int Run(Logger logger, int parentProcessId)
        {
            // get parent process which will be monitored

            Process parentProcess = null;
            if (parentProcessId != 0)
            {
                try
                {
                    parentProcess = Process.GetProcessById(parentProcessId);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Cannot find parentProcess (Id={0})", parentProcessId);
                    return 1;
                }
            }

            // open service

            try
            {
                var address = CompilerServiceHelper.BaseAddress + parentProcessId;
                serviceHost = new ServiceHost(typeof(CompilerService));
                var binding = CompilerServiceHelper.GetBinding();
                serviceHost.AddServiceEndpoint(typeof(ICompilerService), binding, address);
                serviceHost.Open();
            }
            catch (Exception e)
            {
                if (serviceHost != null)
                {
                    serviceHost.Close();
                }
                logger.Error(e, "Service Host got an error");
                return 1;
            }

            if (parentProcess != null)
            {
                //WaitForExit returns immediately instead of waiting on Mac so use while loop
                if (PlatformHelper.CurrentPlatform == Platform.Mac)
                {
                    while (!parentProcess.HasExited)
                    {
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    parentProcess.WaitForExit();
                }
                if (serviceHost != null)
                {
                    serviceHost.Close();
                }
                logger.Info("Parent process just exited. (PID={0})", parentProcess.Id);
            }

            return 0;
        }
    }
}
