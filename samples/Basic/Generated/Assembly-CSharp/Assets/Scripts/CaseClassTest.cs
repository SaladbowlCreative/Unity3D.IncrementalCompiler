using System;
using System.Collections.Generic;
using GenerationAttributes;

namespace Assets.Scripts
{
    public partial struct CCCompanionWithoutGenerics : System.IEquatable<CCCompanionWithoutGenerics>
    {
        public CCCompanionWithoutGenerics(string name, Func<int, string> get, Func<double, int> nToA)
        {
            this.name = name;
            this.get = get;
            this.nToA = nToA;
        }

        public override string ToString() => "CCCompanionWithoutGenerics(" + "name: " + name + ", get: " + get + ", nToA: " + nToA + ")";
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ (name == null ? 0 : name.GetHashCode()); // System_String
                hashCode = (hashCode * 397) ^ (get == null ? 0 : get.GetHashCode()); // None
                hashCode = (hashCode * 397) ^ (nToA == null ? 0 : nToA.GetHashCode()); // None
                return hashCode;
            }
        }

        public bool Equals(CCCompanionWithoutGenerics other) => string.Equals(name, other.name) && get.Equals(other.get) && nToA.Equals(other.nToA);
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is CCCompanionWithoutGenerics && Equals((CCCompanionWithoutGenerics)obj);
        }

        public static bool operator ==(CCCompanionWithoutGenerics left, CCCompanionWithoutGenerics right) => left.Equals(right);
        public static bool operator !=(CCCompanionWithoutGenerics left, CCCompanionWithoutGenerics right) => !left.Equals(right);
        public static CCCompanionWithoutGenerics a(string name, Func<int, string> get, Func<double, int> nToA) => new CCCompanionWithoutGenerics(name, get, nToA);
    }
}

namespace Assets.Scripts
{
    public partial struct CCOneGenericArgument<A> : System.IEquatable<CCOneGenericArgument<A>>
    {
        public CCOneGenericArgument(string name, Func<A, string> get)
        {
            this.name = name;
            this.get = get;
        }

        public override string ToString() => "CCOneGenericArgument(" + "name: " + name + ", get: " + get + ")";
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ (name == null ? 0 : name.GetHashCode()); // System_String
                hashCode = (hashCode * 397) ^ (get == null ? 0 : get.GetHashCode()); // None
                return hashCode;
            }
        }

        public bool Equals(CCOneGenericArgument<A> other) => string.Equals(name, other.name) && get.Equals(other.get);
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is CCOneGenericArgument<A> && Equals((CCOneGenericArgument<A>)obj);
        }

        public static bool operator ==(CCOneGenericArgument<A> left, CCOneGenericArgument<A> right) => left.Equals(right);
        public static bool operator !=(CCOneGenericArgument<A> left, CCOneGenericArgument<A> right) => !left.Equals(right);
    }
}

namespace Assets.Scripts
{
    public partial static class CCOneGenericArgument
    {
        public static CCOneGenericArgument<A> a<A>(string name, Func<A, string> get) => new CCOneGenericArgument<A>(name, get);
    }
}

namespace Assets.Scripts
{
    public partial struct CCNoConstructor<A> : System.IEquatable<CCNoConstructor<A>>
    {
        public override string ToString() => "CCNoConstructor(" + "name: " + name + ", get: " + get + ")";
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ (name == null ? 0 : name.GetHashCode()); // System_String
                hashCode = (hashCode * 397) ^ (get == null ? 0 : get.GetHashCode()); // None
                return hashCode;
            }
        }

        public bool Equals(CCNoConstructor<A> other) => string.Equals(name, other.name) && get.Equals(other.get);
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is CCNoConstructor<A> && Equals((CCNoConstructor<A>)obj);
        }

        public static bool operator ==(CCNoConstructor<A> left, CCNoConstructor<A> right) => left.Equals(right);
        public static bool operator !=(CCNoConstructor<A> left, CCNoConstructor<A> right) => !left.Equals(right);
    }
}

namespace Assets.Scripts
{
    public partial struct CCSeveralGenerics<A, N>
    {
        public CCSeveralGenerics(string name, Func<A, string> get, Func<N, A> nToA)
        {
            this.name = name;
            this.get = get;
            this.nToA = nToA;
        }

        public override string ToString() => "CCSeveralGenerics(" + "name: " + name + ", get: " + get + ", nToA: " + nToA + ")";
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ (name == null ? 0 : name.GetHashCode()); // System_String
                hashCode = (hashCode * 397) ^ (get == null ? 0 : get.GetHashCode()); // None
                hashCode = (hashCode * 397) ^ (nToA == null ? 0 : nToA.GetHashCode()); // None
                return hashCode;
            }
        }
    }
}

namespace Assets.Scripts
{
    public partial static class CCSeveralGenerics
    {
        public static CCSeveralGenerics<A, N> a<A, N>(string name, Func<A, string> get, Func<N, A> nToA) => new CCSeveralGenerics<A, N>(name, get, nToA);
    }
}

namespace Assets.Scripts
{
    public partial struct CCNoStaticApply : System.IEquatable<CCNoStaticApply>
    {
        public CCNoStaticApply(string name, Func<int, string> get, Func<double, int> nToA)
        {
            this.name = name;
            this.get = get;
            this.nToA = nToA;
        }

        public override string ToString() => "CCNoStaticApply(" + "name: " + name + ", get: " + get + ", nToA: " + nToA + ")";
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ (name == null ? 0 : name.GetHashCode()); // System_String
                hashCode = (hashCode * 397) ^ (get == null ? 0 : get.GetHashCode()); // None
                hashCode = (hashCode * 397) ^ (nToA == null ? 0 : nToA.GetHashCode()); // None
                return hashCode;
            }
        }

        public bool Equals(CCNoStaticApply other) => string.Equals(name, other.name) && get.Equals(other.get) && nToA.Equals(other.nToA);
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is CCNoStaticApply && Equals((CCNoStaticApply)obj);
        }

        public static bool operator ==(CCNoStaticApply left, CCNoStaticApply right) => left.Equals(right);
        public static bool operator !=(CCNoStaticApply left, CCNoStaticApply right) => !left.Equals(right);
    }
}