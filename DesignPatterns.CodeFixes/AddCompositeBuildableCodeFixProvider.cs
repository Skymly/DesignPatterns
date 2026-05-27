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
/// Adds <c>ICompositeBuildable&lt;TContract&gt;</c> to a composite part class.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddCompositeBuildableCodeFixProvider)), Shared]
public sealed class AddCompositeBuildableCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.CompositePartMissingBuildable);

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

        var buildableTypeName = $"ICompositeBuildable<{contractTypeName}>";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Implement {buildableTypeName}",
                createChangedDocument: cancellationToken =>
                    AddBuildableAsync(context.Document, classDeclaration!, buildableTypeName, cancellationToken),
                equivalenceKey: nameof(AddCompositeBuildableCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddBuildableAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string buildableTypeName,
        CancellationToken cancellationToken)
    {
        var interfaceType = SyntaxFactory.ParseTypeName(buildableTypeName);
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
