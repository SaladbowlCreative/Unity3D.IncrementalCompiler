using System;
using System.Collections.Generic;
using System.Linq;

namespace GenerationAttributes {
    public static class Helpers {
        public static string enumerableToString<A>(IEnumerable<A> enumerable) =>
            String.Join(", ", enumerable.Select(_ => _.ToString()).ToArray());
    }
}
