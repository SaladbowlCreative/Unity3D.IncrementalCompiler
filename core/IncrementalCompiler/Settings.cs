using System;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace IncrementalCompiler
{
    public class Settings
    {
        public DebugSymbolFileType DebugSymbolFile;
        public PrebuiltOutputReuseType PrebuiltOutputReuse;

        public static Settings Default = new Settings
        {
            DebugSymbolFile = DebugSymbolFileType.Mdb,
            PrebuiltOutputReuse = PrebuiltOutputReuseType.WhenNoChange,
        };

        public static Settings Load()
        {
            var fileName = Path.ChangeExtension(Assembly.GetEntryAssembly().Location, ".xml");
            if (File.Exists(fileName) == false)
                return null;

            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Load(stream);
            }
        }

        public static Settings Load(Stream stream)
        {
            // To reduce start-up time, do manual parsing instead of using XmlSerializer
            var xdoc = XDocument.Load(stream).Element("Settings");
            return new Settings
            {
                DebugSymbolFile = (DebugSymbolFileType)Enum.Parse(typeof(DebugSymbolFileType), xdoc.Element("DebugSymbolFile").Value),
                PrebuiltOutputReuse = (PrebuiltOutputReuseType)Enum.Parse(typeof(PrebuiltOutputReuseType), xdoc.Element("PrebuiltOutputReuse").Value),
            };
        }
    }
}
