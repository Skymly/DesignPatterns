using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.SourceGenerators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.CodeFixes;

/// <summary>
/// Adds a missing contract interface to the class declaration.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddContractImplementationCodeFixProvider)), Shared]
public sealed class AddContractImplementationCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(
            DiagnosticIds.RegisterStrategyContractMismatch,
            DiagnosticIds.HandlerOrderContractMismatch,
            DiagnosticIds.CompositePartContractMismatch);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        if (!CodeFixHelpers.TryGetClassDeclaration(root, diagnostic, out var classDeclaration))
        {
            return;
        }

        if (!CodeFixHelpers.TryGetContractTypeName(diagnostic, out var contractTypeName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Implement {contractTypeName}",
                createChangedDocument: cancellationToken =>
                    AddInterfaceAsync(context.Document, classDeclaration!, contractTypeName!, cancellationToken),
                equivalenceKey: nameof(AddContractImplementationCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddInterfaceAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string contractTypeName,
        CancellationToken cancellationToken)
    {
        var interfaceType = SyntaxFactory.ParseTypeName(contractTypeName);
        BaseListSyntax baseList;

        if (classDeclaration.BaseList is null)
        {
            baseList = SyntaxFactory.BaseList(
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                    SyntaxFactory.SimpleBaseType(interfaceType)));
        }
        else
        {
            baseList = classDeclaration.BaseList.AddTypes(
                SyntaxFactory.SimpleBaseType(interfaceType));
        }

        var newClass = classDeclaration.WithBaseList(baseList);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
