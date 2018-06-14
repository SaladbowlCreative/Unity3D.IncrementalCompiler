using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace IncrementalCompiler
{
    [DataContract]
    public class CompileResult
    {
        [DataMember] public bool Succeeded;
        [DataMember] public List<string> Warnings = new List<string>();
        [DataMember] public List<string> Errors = new List<string>();

        public void Clear() {
            Succeeded = false;
            Warnings.Clear();
            Errors.Clear();
        }
    }
}
