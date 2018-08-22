// Exceprt from Mono.Cecil.Tests

// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.

using System;
using System.Linq;
using SR = System.Reflection;

namespace Mono.Cecil
{
    public static class Extensions
    {
        public static MethodDefinition GetMethod(this TypeDefinition self, string name)
        {
            return self.Methods.First(m => m.Name == name);
        }

        public static FieldDefinition GetField(this TypeDefinition self, string name)
        {
            return self.Fields.First(f => f.Name == name);
        }

        public static TypeDefinition ToDefinition(this Type self)
        {
            var module = ModuleDefinition.ReadModule(self.Module.FullyQualifiedName);
            return (TypeDefinition)module.LookupToken(self.MetadataToken);
        }

        public static MethodDefinition ToDefinition(this SR.MethodBase method)
        {
            var declaring_type = method.DeclaringType.ToDefinition();
            return (MethodDefinition)declaring_type.Module.LookupToken(method.MetadataToken);
        }

        public static FieldDefinition ToDefinition(this SR.FieldInfo field)
        {
            var declaring_type = field.DeclaringType.ToDefinition();
            return (FieldDefinition)declaring_type.Module.LookupToken(field.MetadataToken);
        }

        public static TypeReference MakeGenericType(this TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        public static MethodReference MakeGenericMethod(this MethodReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceMethod(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        public static MethodReference MakeGeneric(this MethodReference self, params TypeReference[] arguments)
        {
            var reference = new MethodReference(self.Name, self.ReturnType, self.DeclaringType.MakeGenericType(arguments))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention,
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

        public static FieldReference MakeGeneric(this FieldReference self, params TypeReference[] arguments)
        {
            return new FieldReference(self.Name, self.FieldType, self.DeclaringType.MakeGenericType(arguments));
        }
    }
}
