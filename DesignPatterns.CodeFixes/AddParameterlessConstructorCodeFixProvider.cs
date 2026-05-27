using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.CodeFixes;

/// <summary>
/// Adds a public parameterless constructor when required by generated registration.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddParameterlessConstructorCodeFixProvider)), Shared]
public sealed class AddParameterlessConstructorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(
            DiagnosticIds.RegisterStrategyMissingParameterlessConstructor,
            DiagnosticIds.HandlerOrderMissingParameterlessConstructor,
            DiagnosticIds.CompositePartMissingParameterlessConstructor);

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

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add public parameterless constructor",
                createChangedDocument: cancellationToken =>
                    AddConstructorAsync(context.Document, classDeclaration!, cancellationToken),
                equivalenceKey: nameof(AddParameterlessConstructorCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddConstructorAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken)
    {
        var constructor = SyntaxFactory.ConstructorDeclaration(classDeclaration.Identifier)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithBody(SyntaxFactory.Block());

        var newClass = classDeclaration.AddMembers(constructor);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
