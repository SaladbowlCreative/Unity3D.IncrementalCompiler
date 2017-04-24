using System.ServiceModel;
using System.ServiceModel.Channels;

namespace IncrementalCompiler
{
    public static class CompilerServiceHelper
    {
        private static string winAddress = "net.pipe://localhost/Unity3D.IncrementalCompiler/";
        private static string macAddress = "http://localhost:52000/Unity3D.IncrementalCompiler/";

        public static string BaseAddress
        {
            get
            {
                return PlatformHelper.CurrentPlatform == Platform.Mac ? macAddress : winAddress;
            }
        }

        public static Binding GetBinding()
        {
            if (PlatformHelper.CurrentPlatform == Platform.Mac)
            {
                return new BasicHttpBinding(BasicHttpSecurityMode.None)
                {
                    MaxBufferSize = int.MaxValue,
                    MaxReceivedMessageSize = int.MaxValue
                };
            }
            else
            {
                return new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
                {
                    MaxBufferSize = int.MaxValue,
                    MaxReceivedMessageSize = int.MaxValue
                };
            }
        }
    }
}
