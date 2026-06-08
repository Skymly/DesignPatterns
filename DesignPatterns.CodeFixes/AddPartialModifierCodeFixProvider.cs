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
/// Adds the <c>partial</c> modifier required by <c>[GenerateSingleton]</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddPartialModifierCodeFixProvider)), Shared]
public sealed class AddPartialModifierCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.GenerateSingletonNotPartial);

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

        if (classDeclaration!.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add partial modifier",
                createChangedDocument: cancellationToken =>
                    AddPartialModifierAsync(context.Document, classDeclaration, cancellationToken),
                equivalenceKey: nameof(AddPartialModifierCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddPartialModifierAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken)
    {
        var newClass = classDeclaration.WithModifiers(AddPartialModifier(classDeclaration.Modifiers));
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxTokenList AddPartialModifier(SyntaxTokenList modifiers)
    {
        var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);

        if (modifiers.Count == 0)
        {
            return SyntaxFactory.TokenList(partialToken);
        }

        var insertIndex = 0;
        while (insertIndex < modifiers.Count &&
               modifiers[insertIndex] is { RawKind: (int)SyntaxKind.PublicKeyword or (int)SyntaxKind.PrivateKeyword or (int)SyntaxKind.ProtectedKeyword or (int)SyntaxKind.InternalKeyword })
        {
            insertIndex++;
        }

        return modifiers.Insert(insertIndex, partialToken);
    }
}
