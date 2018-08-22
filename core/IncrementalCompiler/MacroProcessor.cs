using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GenerationAttributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace IncrementalCompiler
{
    public static class MacroProcessor
    {
        public static CSharpCompilation Run(CSharpCompilation compilation, ImmutableArray<SyntaxTree> trees, Dictionary<string, SyntaxTree> sourceMap)
        {
            var macrosType = typeof(Macros).FullName;
            var macros = compilation.GetTypeByMetadataName(macrosType);

            if (macros == null) {
                throw new Exception($"Could not find type {macrosType} in project.");
            }

            var builder = ImmutableDictionary.CreateBuilder<ISymbol, MacroRewriter.PropertyMacro>();

            ISymbol macroSymbol(string name) => macros.GetMembers(name).First();

            builder.Add(
                macroSymbol(nameof(Macros.className)),
                (model, syntax) =>
                {
                    var enclosingSymbol = model.GetEnclosingSymbol(syntax.SpanStart);
                    return enclosingSymbol.ContainingType.ToDisplayString().StringLiteral();
                });

            builder.Add(
                macroSymbol(nameof(Macros.classAndMethodName)),
                (model, syntax) =>
                {
                    var enclosingSymbol = model.GetEnclosingSymbol(syntax.SpanStart);
                    return enclosingSymbol.ToDisplayString().StringLiteral();
                });

            var allMacros = builder.ToImmutable();

//            var namedTypeSymbols = CustomSymbolFinder.GetAllSymbols(compilation);

            /*
            foreach (var typeSymbol in namedTypeSymbols)
            {
                if (typeSymbol == null)
                {
                    Console.WriteLine("null");
                    continue;
                }
                var members = typeSymbol.GetMembers();
                foreach (var member in members)
                {
                    Console.WriteLine(member.Name);
                }
            }
            */

            var oldCompilation = compilation;

            var treeEdits = trees.AsParallel().SelectMany(tree =>
            {
//                var walker = new Walker();
                var root = tree.GetCompilationUnitRoot();
                var model = oldCompilation.GetSemanticModel(tree);

                var opFinder = new RootOperationsFinder(model);

                opFinder.Visit(root);

                var changes = new Dictionary<SyntaxNode, SyntaxNode>();

                foreach (var operation in opFinder.results)
                {
//                    Console.WriteLine("Found Operation: " + operation);
//                    Console.WriteLine(operation.Syntax);

                    var props = operation.DescendantsAndSelf().OfType<IPropertyReferenceOperation>();

                    foreach (var prop in props) {
                        if (allMacros.TryGetValue(prop.Property, out var fn)) {
                            changes.Add(prop.Syntax, fn(model, prop.Syntax));
                        }
                    }

//                    Console.WriteLine(op?.Type?.ToString() ?? "null");
//                    var symbol = model.GetDeclaredSymbol(classDecl);
//                    var op = symbol.GetRootOperation();
//                    Console.WriteLine(op?.Type.ToString() ?? "null");
//                    Console.WriteLine(op?.Type);

//                    Console.WriteLine(op?.Type);

//                    var members = symbol.GetMembers();
//                    foreach (var member in members)
//                    {
//
//                        var op = model.GetOperation(member.DeclaringSyntaxReferences[0].GetSyntax().);
//                        Console.WriteLine(op?.Type);
//                    }
                }

//                var rewriter = new MacroRewriter(model, builder.ToImmutable());
//                var newRoot = (CompilationUnitSyntax)rewriter.VisitCompilationUnit(root);
//                return rewriter.ChangesMade
//                    ? new[] {(tree, newRoot)}
//                    : Enumerable.Empty<(SyntaxTree, CompilationUnitSyntax)>();

                if (changes.Any())
                {
                    var updatedTree = root.ReplaceNodes(changes.Keys, (a, b) => changes[a]);
//                    Console.WriteLine(updatedTree.GetText());
                    return new[] {(tree, updatedTree)};
                }
                return Enumerable.Empty<(SyntaxTree, CompilationUnitSyntax)>();
            }).ToArray();
            compilation = EditTrees(compilation, sourceMap, treeEdits);
            return compilation;
        }

        public static CSharpCompilation EditTrees(
            CSharpCompilation compilation,
            Dictionary<string, SyntaxTree> sourceMap,
            IEnumerable<(SyntaxTree, CompilationUnitSyntax)> treeEdits
        ) {
            foreach (var (tree, syntax) in treeEdits)
            {
                var newTree = tree.WithRootAndOptions(syntax, tree.Options);
                sourceMap[newTree.FilePath] = newTree;
                compilation = compilation.ReplaceSyntaxTree(tree, newTree);
                var editedFilePath = Path.Combine(CodeGeneration.GENERATED_FOLDER, "compile-time", newTree.FilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(editedFilePath));
                File.WriteAllText(editedFilePath, newTree.GetText().ToString());
            }
            return compilation;
        }
    }

    public class RootOperationsFinder : CSharpSyntaxWalker
    {
        private readonly SemanticModel model;
        public List<IOperation> results = new List<IOperation>();

        public RootOperationsFinder(SemanticModel model) {
            this.model = model;
        }

        public override void Visit(SyntaxNode node) {
            var operation = model.GetOperation(node);
            if (operation == null) base.Visit(node);
            else results.Add(operation);
        }
    }

    public class Walker : OperationWalker
    {
        private int ident;
        public override void Visit(IOperation operation) {
            for (var i = 0; i < ident; i++) {
                Console.Write("  ");
            }
            Console.WriteLine(operation?.Kind.ToString());
            ident++;
            base.Visit(operation);
            ident--;
        }
    }

    public class CustomSymbolFinder
    {
        public static List<INamedTypeSymbol> GetAllSymbols(Compilation compilation)
        {
            var visitor = new FindAllSymbolsVisitor();
            visitor.Visit(compilation.GlobalNamespace);
            return visitor.AllTypeSymbols;
        }

        private class FindAllSymbolsVisitor : SymbolVisitor
        {
            public List<INamedTypeSymbol> AllTypeSymbols { get; } = new List<INamedTypeSymbol>();

            public override void VisitNamespace(INamespaceSymbol symbol)
            {
                Parallel.ForEach(symbol.GetMembers(), s => s.Accept(this));
            }

            public override void VisitNamedType(INamedTypeSymbol symbol)
            {
                AllTypeSymbols.Add(symbol);
                foreach (var childSymbol in symbol.GetTypeMembers())
                {
                    base.Visit(childSymbol);
                }
            }
        }
    }
}
