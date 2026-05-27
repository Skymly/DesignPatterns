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
/// Adds <c>[RegisterStrategy]</c> to an unregistered strategy implementation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddRegisterStrategyCodeFixProvider)), Shared]
public sealed class AddRegisterStrategyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.RegisterStrategyUnregisteredImplementation);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null || root is null)
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

        var className = classDeclaration!.Identifier.Text;
        var key = CodeFixHelpers.ToSnakeCaseKey(className);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add [RegisterStrategy(\"{key}\", typeof({contractTypeName}))]",
                createChangedDocument: cancellationToken =>
                    AddAttributeAsync(context.Document, classDeclaration, key, contractTypeName!, cancellationToken),
                equivalenceKey: nameof(AddRegisterStrategyCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddAttributeAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string key,
        string contractTypeName,
        CancellationToken cancellationToken)
    {
        var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName("RegisterStrategy"))
            .WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList<AttributeArgumentSyntax>(
                        new[]
                        {
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(key))),
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(contractTypeName))),
                        })));

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
        var newClass = classDeclaration.AddAttributeLists(attributeList);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
