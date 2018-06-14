using System;
using System.Diagnostics;

namespace GenerationAttributes
{
    /// <summary>
    /// When added on a C# interface [CS_interface]:
    /// 1. Generates identical interface in java with all the methods.
    /// 2. Generates a class named [CS_interface]Proxy that implements the generated java interface.
    /// Generated class extends tlplib.JavaListenerProxy
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    [Conditional("CodeGeneration")]
    public class JavaListenerInterfaceAttribute : Attribute
    {
        /// <summary>Android module the code will be generated in</summary>
        public readonly string Module;

        public JavaListenerInterfaceAttribute(string module) {
            Module = module;
        }
    }

    /// <summary>
    /// Can only be used inside class with JavaClass attribute.
    /// Generates a method inside that java class.
    /// Method should not have an implementation. Compiler generates C# method body at compile-time.
    /// When you call this method in C#, it gets redirected to Java automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    [Conditional("CodeGeneration")]
    public class JavaMethodAttribute : Attribute
    {
        /// <summary>Method body in generated Java code.</summary>
        public readonly string MethodBody;

        public JavaMethodAttribute(string methodBody) {
            MethodBody = methodBody;
        }
    }

    /// <summary>
    /// Generates a java class with provided imports and body.
    /// When you instantiate this object, then a mirror object is also instantiated in Java.
    /// You should use <see cref="JavaMethodAttribute"/> on methods inside this class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [Conditional("CodeGeneration")]
    public class JavaClassAttribute : Attribute
    {
        /// <summary>Android module the code will be generated in</summary>
        public readonly string Module;
        /// <summary>Java code that will be put at top of the generated file</summary>
        public readonly string Imports;
        /// <summary>Java code that will be put in the generated class</summary>
        public readonly string ClassBody;

        public JavaClassAttribute(string module, string imports, string classBody) {
            Module = module;
            Imports = imports;
            ClassBody = classBody;
        }
    }

    /// <summary>
    /// Use this on classes that extend Binding, AndroidJavaObject and AndroidJavaProxy.
    /// This attribute is used by <see cref="JavaMethodAttribute"/> to determine what object type this is in Java
    /// when generating code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [Conditional("CodeGeneration")]
    [JavaClass("sdfsdf","","")]
    public class JavaBindingAttribute : Attribute
    {
        /// <summary>Full java class name of associated java object</summary>
        public readonly string JavaClass;

        public JavaBindingAttribute(string javaClass) {
            JavaClass = javaClass;
        }
    }

    public class JavaFile
    {
        public readonly string Module, Path, Contents;

        public JavaFile(string module, string path, string contents) {
            Module = module;
            Path = path;
            Contents = contents;
        }
    }
}
