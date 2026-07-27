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
/// Adds <c>[RegisterCommandHandler]</c> to an unregistered command handler implementation.
/// Prefers the generic attribute form when the project language and referenced TFM support it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddRegisterCommandHandlerCodeFixProvider)), Shared]
public sealed class AddRegisterCommandHandlerCodeFixProvider : CodeFixProvider
{
    private const string GenericAttributeMetadataName = "DesignPatterns.Behavioral.RegisterCommandHandlerAttribute`1";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.CommandHandlerUnregisteredImplementation);

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

        if (!CodeFixHelpers.TryGetContractTypeName(diagnostic, out var commandTypeName))
        {
            return;
        }

        var useGeneric = ShouldUseGenericAttribute(context.Document, semanticModel);
        var title = useGeneric
            ? $"Add [RegisterCommandHandler<{commandTypeName}>]"
            : $"Add [RegisterCommandHandler(typeof({commandTypeName}))]";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: cancellationToken =>
                    AddAttributeAsync(
                        context.Document,
                        classDeclaration!,
                        commandTypeName!,
                        useGeneric,
                        cancellationToken),
                equivalenceKey: nameof(AddRegisterCommandHandlerCodeFixProvider)),
            diagnostic);
    }

    private static bool ShouldUseGenericAttribute(Document document, SemanticModel semanticModel)
    {
        if (document.Project.ParseOptions is not CSharpParseOptions parseOptions ||
            parseOptions.LanguageVersion < LanguageVersion.CSharp11)
        {
            return false;
        }

        return semanticModel.Compilation.GetTypeByMetadataName(GenericAttributeMetadataName) is not null;
    }

    private static async Task<Document> AddAttributeAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string commandTypeName,
        bool useGeneric,
        CancellationToken cancellationToken)
    {
        AttributeSyntax attribute;
        if (useGeneric)
        {
            attribute = SyntaxFactory.Attribute(
                SyntaxFactory.GenericName("RegisterCommandHandler")
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.ParseTypeName(commandTypeName)))));
        }
        else
        {
            attribute = SyntaxFactory.Attribute(
                    SyntaxFactory.ParseName("RegisterCommandHandler"))
                .WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(commandTypeName))))));
        }

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
        var newClass = classDeclaration.AddAttributeLists(attributeList);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(classDeclaration, newClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
