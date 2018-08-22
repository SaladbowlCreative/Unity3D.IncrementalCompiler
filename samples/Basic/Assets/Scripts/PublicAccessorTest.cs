using System.Collections.Generic;
using GenerationAttributes;

namespace IdenticalNamespace {
    public class IdenticalType {
        Assets.Scripts.Class1 class1;
    }
}

namespace Assets.Scripts.IdenticalNamespace {
    public class IdenticalType {
         Class1 class1;
    }
}

namespace Assets.Scripts {
    public class Class1 { }

    public partial class PublicAccessorTest {
        [PublicAccessor] int _num;
        [PublicAccessor] Class1 class1;
        [PublicAccessor] global::IdenticalNamespace.IdenticalType type1;
        [PublicAccessor] IdenticalNamespace.IdenticalType type2;
        [PublicAccessor] int[] numArray;
        [PublicAccessor] List<IdenticalNamespace.IdenticalType> list;
    }
}
