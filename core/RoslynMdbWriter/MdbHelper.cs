using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGen;

namespace Mono.CompilerServices.SymbolWriter
{
    public static class MdbHelper
    {
        public static EmitResult EmitWithMdb(this Compilation compilation, Stream peStream, Stream pdbStream = null,
            Stream xmlDocumentationStream = null, Stream win32Resources = null,
            IEnumerable<ResourceDescription> manifestResources = null, EmitOptions options = null,
            IMethodSymbol debugEntryPoint = null, Stream sourceLinkStream = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (peStream == null)
            {
                throw new ArgumentNullException(nameof(peStream));
            }

            if (!peStream.CanWrite)
            {
                throw new ArgumentException(CodeAnalysisResources.StreamMustSupportWrite, nameof(peStream));
            }

            if (pdbStream != null && !pdbStream.CanWrite)
            {
                throw new ArgumentException(CodeAnalysisResources.StreamMustSupportWrite, nameof(pdbStream));
            }

            var testData = new CompilationTestData
            {
                SymWriterFactory = () => new MdbWriter()
            };

            // internal EmitResult Emit(Stream peStream, Stream metadataPEStream, Stream pdbStream, Stream xmlDocumentationStream, Stream win32Resources,
            // IEnumerable<ResourceDescription> manifestResources, EmitOptions options, IMethodSymbol debugEntryPoint, Stream sourceLinkStream, IEnumerable<EmbeddedText> embeddedTexts,
            // CompilationTestData testData, CancellationToken cancellationToken)

            return compilation.Emit(
                peStream,
                null,
                pdbStream,
                xmlDocumentationStream,
                win32Resources,
                manifestResources,
                options,
                debugEntryPoint,
                sourceLinkStream,
                Enumerable.Empty<EmbeddedText>(),
                testData,
                cancellationToken);
        }
    }
}
