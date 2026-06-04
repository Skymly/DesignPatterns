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
/// Adds <c>[HandlerOrder]</c> to an unregistered handler implementation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddHandlerOrderCodeFixProvider)), Shared]
public sealed class AddHandlerOrderCodeFixProvider : CodeFixProvider
{
    private const int DefaultOrder = 10;

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.HandlerOrderUnregisteredImplementation);

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

        if (!CodeFixHelpers.TryGetContractTypeName(diagnostic, out var contextTypeName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add [HandlerOrder({DefaultOrder}, typeof({contextTypeName}))]",
                createChangedDocument: cancellationToken =>
                    AddAttributeAsync(context.Document, classDeclaration!, contextTypeName!, cancellationToken),
                equivalenceKey: nameof(AddHandlerOrderCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddAttributeAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string contextTypeName,
        CancellationToken cancellationToken)
    {
        var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName("HandlerOrder"))
            .WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList<AttributeArgumentSyntax>(
                        new[]
                        {
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    SyntaxFactory.Literal(DefaultOrder))),
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(contextTypeName))),
                        })));

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
        var newClass = classDeclaration.AddAttributeLists(attributeList);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
