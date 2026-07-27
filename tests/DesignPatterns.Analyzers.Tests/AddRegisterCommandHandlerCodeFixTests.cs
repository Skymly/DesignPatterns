using System.Collections.Immutable;
using DesignPatterns.Analyzers;
using DesignPatterns.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.Analyzers.Tests;

public sealed class AddRegisterCommandHandlerCodeFixTests
{
    [Fact]
    public async Task FixesDp072ByAddingGenericRegisterCommandHandlerAttribute()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler<PingCommand>]
            public sealed class RegisteredPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class OrphanPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.Id == "DP072");

        var document = AnalyzerTestContext.CreateDocument(source);
        var fixes = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => fixes.Add(action),
            CancellationToken.None);
        await new AddRegisterCommandHandlerCodeFixProvider().RegisterCodeFixesAsync(context);

        var operation = await fixes[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = Assert.IsType<ApplyChangesOperation>(operation.Single());
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        await Verifier.Verify(fixedSource);
    }

    [Fact]
    public async Task FixesDp072ByAddingNonGenericRegisterCommandHandlerAttributeOnCSharp10()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler(typeof(PingCommand))]
            public sealed class RegisteredPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class OrphanPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.Id == "DP072");

        var document = CreateDocument(source, LanguageVersion.CSharp10);
        var fixes = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => fixes.Add(action),
            CancellationToken.None);
        await new AddRegisterCommandHandlerCodeFixProvider().RegisterCodeFixesAsync(context);

        var operation = await fixes[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = Assert.IsType<ApplyChangesOperation>(operation.Single());
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        await Verifier.Verify(fixedSource);
    }

    private static Document CreateDocument(string source, LanguageVersion languageVersion)
    {
        var workspace = new AdhocWorkspace(MefHostServices.Create(MefHostServices.DefaultAssemblies));
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "Test", "Test", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(languageVersion));

        foreach (var reference in GetSharedReferences())
        {
            solution = solution.AddMetadataReference(projectId, reference);
        }

        solution = solution.AddDocument(documentId, "Test.cs", SourceText.From(source));
        return solution.GetDocument(documentId)!;
    }

    private static ImmutableArray<MetadataReference> GetSharedReferences()
    {
        // Mirror AnalyzerTestContext references via a fresh Latest document's project.
        var probe = AnalyzerTestContext.CreateDocument("class Probe;");
        return probe.Project.MetadataReferences.ToImmutableArray();
    }
}
