using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NLog;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace IncrementalCompiler
{
    static class Extensions
    {
        public static bool Has(this BasePropertyDeclarationSyntax decl, SyntaxKind kind)
            => decl.Modifiers.Has(kind);

        public static bool Has(this BaseMethodDeclarationSyntax decl, SyntaxKind kind)
            => decl.Modifiers.Has(kind);

        public static bool Has(this SyntaxTokenList tokens, SyntaxKind kind)
            => tokens.Any(m => m.IsKind(kind));

        public static bool HasNot(this BasePropertyDeclarationSyntax decl, SyntaxKind kind)
            => decl.Modifiers.HasNot(kind);

        public static bool HasNot(this BaseMethodDeclarationSyntax decl, SyntaxKind kind)
            => decl.Modifiers.HasNot(kind);

        public static bool HasNot(this SyntaxTokenList tokens, SyntaxKind kind)
            => tokens.All(m => !m.IsKind(kind));

        public static PropertyDeclarationSyntax Remove(this PropertyDeclarationSyntax decl, SyntaxKind kind)
            => decl.WithModifiers(decl.Modifiers.Remove(kind));

        public static MethodDeclarationSyntax Remove(this MethodDeclarationSyntax decl, SyntaxKind kind)
            => decl.WithModifiers(decl.Modifiers.Remove(kind));

        public static SyntaxTokenList Remove(this SyntaxTokenList tokens, SyntaxKind kind)
            => tokens.Remove(SF.Token(kind));

        public static SyntaxTokenList RemoveOfKind(this SyntaxTokenList tokens, SyntaxKind kind) {
            var i = tokens.IndexOfKind(kind);
            return i != -1 ? tokens.RemoveAt(i) : tokens;
        }

        public static int IndexOfKind(this SyntaxTokenList tokens, SyntaxKind kind) {
            var i = 0;
            foreach (var t in tokens) {
                if (t.IsKind(kind)) return i;
                else i++;
            }

            return -1;
        }

        public static SyntaxTokenList Add(this SyntaxTokenList modifiers, SyntaxKind kind)
            => modifiers.Any(m => m.IsKind(kind)) ? modifiers : modifiers.Add(SF.Token(kind));

        public static SyntaxTokenList Modifiers(this MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case FieldDeclarationSyntax s1:
                    return s1.Modifiers;
                case PropertyDeclarationSyntax s2:
                    return s2.Modifiers;
                case MethodDeclarationSyntax s:
                    return s.Modifiers;
            }
            return SF.TokenList();
        }

        public static MemberDeclarationSyntax WithModifiers(this MemberDeclarationSyntax member, SyntaxTokenList modifiers)
        {
            switch (member)
            {
                case FieldDeclarationSyntax s1:
                    return s1.WithModifiers(modifiers);
                case PropertyDeclarationSyntax s2:
                    return s2.WithModifiers(modifiers);
                case MethodDeclarationSyntax s:
                    return s.WithModifiers(modifiers);
            }
            return member;
        }

        public static LiteralExpressionSyntax StringLiteral(this string value) =>
            SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal(value));

        public static readonly BaseListSyntax EmptyBaseList = null, NoTypeArguments = null;
        public static SyntaxList<AttributeListSyntax> EmptyAttributeList = SyntaxFactory.List<AttributeListSyntax>();
        public static SyntaxTriviaList EmptyTriviaList = SyntaxFactory.TriviaList();

        public static B tap<A, B>(this A a, Func<A, B> func) => func(a);
        public static void voidTap<A>(this A a, Action<A> act) => act(a);
        public static void ForEach<A>(this IEnumerable<A> enumerable, Action<A> act) {
            foreach (var e in enumerable) act(e);
        }
    }
}
