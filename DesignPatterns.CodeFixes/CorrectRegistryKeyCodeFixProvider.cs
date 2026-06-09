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
/// Suggests the closest registered registry key for an unknown literal key lookup.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CorrectRegistryKeyCodeFixProvider)), Shared]
public sealed class CorrectRegistryKeyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.RegistryKeyNotRegistered);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        if (!CodeFixHelpers.TryGetSuggestedRegistryKey(diagnostic, out var suggestedKey))
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var literalExpression = FindStringLiteral(root, diagnostic);
        if (literalExpression is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Change to \"{suggestedKey}\"",
                createChangedDocument: cancellationToken =>
                    ReplaceLiteralAsync(context.Document, literalExpression, suggestedKey!, cancellationToken),
                equivalenceKey: nameof(CorrectRegistryKeyCodeFixProvider)),
            diagnostic);
    }

    private static LiteralExpressionSyntax? FindStringLiteral(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is LiteralExpressionSyntax literal)
        {
            return literal;
        }

        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (token.Parent is LiteralExpressionSyntax literalFromToken)
        {
            return literalFromToken;
        }

        return node.AncestorsAndSelf().OfType<LiteralExpressionSyntax>().FirstOrDefault();
    }

    private static async Task<Document> ReplaceLiteralAsync(
        Document document,
        LiteralExpressionSyntax literalExpression,
        string suggestedKey,
        CancellationToken cancellationToken)
    {
        var newLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(
                literalExpression.Token.LeadingTrivia,
                suggestedKey,
                suggestedKey,
                literalExpression.Token.TrailingTrivia));

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(literalExpression, newLiteral);
        return document.WithSyntaxRoot(newRoot);
    }
}
