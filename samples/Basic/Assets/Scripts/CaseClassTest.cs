#if UNITY_5


using System;
using System.Collections.Generic;
using GenerationAttributes;

namespace Assets.Scripts {
    // struct DummyStruct {
    //     public readonly int int1, int2;

    //     public string test() {
    //         return Macros.className;
    //     }
    // }

    class Class { }

    enum Enum { A, B, C }
    enum ByteEnum : byte { A, B, C }
    enum LongEnum : long { A, B, C }

    // [Record(GenerateToString = false, GenerateStaticApply = true)]
    // public partial class ClassWithCompanion {
    //     public readonly int int1, int2;
    //     public readonly StructTest structWithHash;
    //     [PublicAccessor] public readonly Class _classRef;
    // }
    //
    // sealed partial class ClassNoCompanion {
    //     public readonly int int2;
    //     [PublicAccessor] public readonly Class _classRef;
    // }
    [Record(GenerateStaticApply = true)]
    public partial struct CCCompanionWithoutGenerics {
        public readonly string name;
        public readonly Func<int, string> get;
        public readonly Func<double, int> nToA;
    }

    [Record(GenerateStaticApply = true)]
    public partial struct CCOneGenericArgument<A> {
        public readonly string name;
        public readonly Func<A, string> get;
    }

    [Record(GenerateConstructor = false)]
    public partial struct CCNoConstructor<A> {
        public readonly string name;
        public readonly Func<A, string> get;
    }

    [Record(GenerateComparer = false, GenerateStaticApply = true)]
    public partial struct CCSeveralGenerics<A, N> {
        public readonly string name;
        public readonly Func<A, string> get;
        public readonly Func<N, A> nToA;
    }

    [Record]
    public partial struct CCNoStaticApply {
        public readonly string name;
        public readonly Func<int, string> get;
        public readonly Func<double, int> nToA;
    }

    // throws exception because record has no fields
    // [Record]
    // public partial struct EmptyRecord {
    // }

    #region evaldo testai

//     [Record(GenerateToString = false, GenerateComparer = false)]
//     sealed partial class ClassTest {
//         public readonly int int1, int2;
//         public readonly string str1, str2;
//         public readonly uint uint1;
//         public readonly StructTest structWithHash;
//         public readonly DummyStruct structNoHash;
//         public readonly float float1;
//         public readonly double double1;
//         public readonly long long1;
//         public readonly bool bool1;
//         public readonly char char1;
//         public readonly byte byte1;
//         public readonly sbyte sbyte1;
//         public readonly short short1;
//         public readonly Enum enum1;
//         public readonly ByteEnum byteEnum;
//         public readonly LongEnum longEnum;
//         [PublicAccessor] public readonly Class _classRef;
//     }

//      [Record(GenerateComparer = false)]
//      partial struct StructTest {
//          public readonly int int1, int2;
//          public readonly string str1, str2;
//          public readonly ClassTest classRef;
//      }

//     [Record]
//     sealed partial class GenericClassTest<A, B, C>
//         where A : struct
//         where B : class
//         where C : InterfaceTest
//     {
//         public readonly A valStruct;
//         public readonly B valClass;
//         public readonly C valInterface;
//     }

//     [Record]
//     partial struct GenericStructTest<A> {
//         public readonly A value;
//     }

//     partial interface InterfaceTest {}

//     [Matcher]
//     abstract class ADTBase {
//         static void sample() {
//             var val = (ADTBase) new One(1);
//             val.match(
//                 one: one => one.val,
//                 two: two => 2
//             );
//         }
//     }

//     [Record]
//     sealed partial class One : ADTBase {
//         public readonly int val;
//     }

//     [Record]
//     [Matcher]
//     sealed partial class Two : ADTBase {
//         public readonly string val;
//         public static readonly int aaa = 10;
//     }

//     [Matcher]
//     interface IAdt { }

//     struct ssss { }

//     [Record]
//     sealed partial class OneI : IAdt {
//         public readonly int val;
//     }

//     [Record]
//     sealed partial class TwoI : IAdt {
//         public readonly string val;
//     }


// //    [Matcher]
//     abstract partial class ADTBaseGeneric<A> { }

//     sealed class GenericOne<A> : ADTBaseGeneric<A> { }
//     sealed class GenericOneV2<B> : ADTBaseGeneric<B> { }
//     sealed class GenericTwo : ADTBaseGeneric<string> { }

//     public partial class Nested1 : IDisposable {
//         [Matcher]
//         public partial class Nested2 {
//             public readonly int val;
//         }

//         public class Nested22 : Nested2 {
//         }

//         #region Implementation of IDisposable

//         public void Dispose() {
//             throw new NotImplementedException();
//         }

//         // line below is left for purpose
//         #endregion
//     }

//     [Record]
//     sealed partial class DoublePartial {
//         [PublicAccessor] readonly string _val;
//     }

//     sealed partial class DoublePartial {
//         [PublicAccessor] readonly int _intVal;
//     }

//     class InvalidStuff {
//         // Can't use this
// //        [ThreadStatic]
//         static int nonono;
//     }
    #endregion
}
#endif
