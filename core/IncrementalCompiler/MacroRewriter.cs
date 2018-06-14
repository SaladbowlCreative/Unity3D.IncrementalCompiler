using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IncrementalCompiler
{
    public class MacroRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;

        public delegate SyntaxNode PropertyMacro(SemanticModel model, SyntaxNode syntax);
        private readonly ImmutableDictionary<ISymbol, PropertyMacro> _memberAccessMacros;

        public bool ChangesMade { get; private set; }

        public MacroRewriter(SemanticModel model, ImmutableDictionary<ISymbol, PropertyMacro> memberAccessMacros) {
            _model = model;
            _memberAccessMacros = memberAccessMacros;
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol;
            if (symbol != null && _memberAccessMacros.TryGetValue(symbol, out var fn))
            {
                ChangesMade = true;
                return fn(_model, node);
            }
            return base.VisitMemberAccessExpression(node);
        }
    }
}
