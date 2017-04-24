using System;
using System.IO;

namespace IncrementalCompiler
{
    public static class PlatformHelper
    {
        public static Platform CurrentPlatform
        {
            get
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Unix:
                        // Well, there are chances MacOSX is reported as Unix instead of MacOSX.
                        // Instead of platform check, we'll do a feature checks (Mac specific root folders)
                        if (Directory.Exists("/Applications")
                            & Directory.Exists("/System")
                            & Directory.Exists("/Users")
                            & Directory.Exists("/Volumes"))
                        {
                            return Platform.Mac;
                        }
                        return Platform.Linux;

                    case PlatformID.MacOSX:
                        return Platform.Mac;

                    default:
                        return Platform.Windows;
                }
            }
        }
    }

    public enum Platform
    {
        Windows,
        Linux,
        Mac
    }
}
