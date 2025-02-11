﻿using Dunet.UnionExtensionsGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Dunet.UnionGeneration;

[Generator]
public sealed class UnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node.IsDecoratedRecord(),
                transform: static (ctx, _) => GetGenerationTarget(ctx)
            )
            .Flatten()
            .Collect();

        var compilation = context.CompilationProvider.Combine(targets);

        context.RegisterSourceOutput(
            compilation,
            static (spc, source) => Execute(source.Left, source.Right, spc)
        );
    }

    private static RecordDeclarationSyntax? GetGenerationTarget(GeneratorSyntaxContext context) =>
        context.Node is RecordDeclarationSyntax record
        && record.IsDecoratedWithUnionAttribute(context.SemanticModel)
            ? record
            : null;

    private static void Execute(
        Compilation compilation,
        ImmutableArray<RecordDeclarationSyntax> recordDeclarations,
        SourceProductionContext context
    )
    {
        if (recordDeclarations.IsDefaultOrEmpty)
        {
            return;
        }

        var unionRecords = GetCodeToGenerate(
            compilation,
            recordDeclarations,
            context.CancellationToken
        );

        foreach (var unionRecord in unionRecords)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var union = UnionSourceBuilder.Build(unionRecord);
            context.AddSource(
                $"{unionRecord.Namespace}.{unionRecord.Name}.g.cs",
                SourceText.From(union, Encoding.UTF8)
            );

            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (unionRecord.SupportsAsyncMatchExtensionMethods())
            {
                var matchExtensions = UnionExtensionsSourceBuilder.GenerateExtensions(unionRecord);
                context.AddSource(
                    $"{unionRecord.Namespace}.{unionRecord.Name}MatchExtensions.g.cs",
                    SourceText.From(matchExtensions, Encoding.UTF8)
                );
            }
        }
    }

    private static IEnumerable<UnionDeclaration> GetCodeToGenerate(
        Compilation compilation,
        IEnumerable<RecordDeclarationSyntax> declarations,
        CancellationToken cancellation
    )
    {
        foreach (var declaration in declarations)
        {
            if (cancellation.IsCancellationRequested)
            {
                yield break;
            }

            var semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
            var recordSymbol = semanticModel.GetDeclaredSymbol(declaration);

            if (recordSymbol is null)
            {
                continue;
            }

            var imports = declaration
                .GetImports()
                .Where(static usingDirective => !usingDirective.IsImporting("Dunet"))
                .Select(static usingDirective => usingDirective.ToString());
            var @namespace = recordSymbol.GetNamespace();
            var typeParameters = declaration.GetTypeParameters();
            var typeParameterConstraints = declaration.GetTypeParameterConstraints();
            var variants = declaration.GetNestedRecordDeclarations(semanticModel);
            var parentTypes = declaration.GetParentTypes(semanticModel);
            var properties = declaration.GetProperties(semanticModel);

            yield return new UnionDeclaration(
                Imports: imports.ToList(),
                Namespace: @namespace,
                Accessibility: recordSymbol.DeclaredAccessibility,
                Name: recordSymbol.Name,
                TypeParameters: typeParameters?.ToList() ?? new(),
                TypeParameterConstraints: typeParameterConstraints?.ToList() ?? new(),
                Variants: variants.ToList(),
                ParentTypes: parentTypes,
                Properties: properties.ToList()
            );
        }
    }
}
