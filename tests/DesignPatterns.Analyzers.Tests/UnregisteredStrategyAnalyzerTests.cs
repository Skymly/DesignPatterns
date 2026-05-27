using System.Collections.Immutable;
using DesignPatterns.Analyzers;
using DesignPatterns.Analyzers.CodeFixes;
using DesignPatterns.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.Analyzers.Tests;

internal static class AnalyzerTestContext
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    internal static async Task<ImmutableArray<Diagnostic>> RunAnalyzersAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        var compilation = CreateCompilation(source);
        return await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzers))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
    }

    internal static async Task<string> ApplyGeneratorCodeFixAsync(
        string source,
        string diagnosticId,
        CodeFixProvider codeFixProvider)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new RegisterStrategyGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var diagnostic = diagnostics.First(d => d.Id == diagnosticId);
        var document = CreateDocument(source);
        var fixes = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => fixes.Add(action),
            CancellationToken.None);
        await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        Assert.NotEmpty(fixes);
        var operation = await fixes[0].GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyChanges = Assert.IsType<ApplyChangesOperation>(operation.Single());
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        return (await fixedDocument.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    internal static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace(MefHostServices.Create(MefHostServices.DefaultAssemblies));
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "Test", "Test", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Latest));

        foreach (var reference in References)
        {
            solution = solution.AddMetadataReference(projectId, reference);
        }

        solution = solution.AddDocument(documentId, "Test.cs", SourceText.From(source));
        return solution.GetDocument(documentId)!;
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
        return CSharpCompilation.Create(
            "AnalyzerTests",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Behavioral.RegisterStrategyAttribute).Assembly.Location),
        };

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            foreach (var assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (assemblyPath.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                    assemblyPath.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                    assemblyPath.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase))
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
            }
        }

        return references.ToImmutableArray();
    }
}

public sealed class UnregisteredStrategyAnalyzerTests
{
    [Fact]
    public async Task ReportsDp006WhenImplementationMissingRegisterStrategy()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            public class WechatPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "wechat";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredStrategyAnalyzer());

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "DP006" &&
                diagnostic.GetMessage().Contains("WechatPayment", StringComparison.Ordinal));
    }
}

public sealed class AddParameterlessConstructorCodeFixTests
{
    [Fact]
    public async Task FixesDp007ByAddingParameterlessConstructor()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public AlipayPayment(string ignored) { }

                public string Pay(decimal amount) => "alipay";
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync(
            source,
            "DP007",
            new AddParameterlessConstructorCodeFixProvider());

        Assert.Contains("public AlipayPayment()", fixedSource, StringComparison.Ordinal);
    }
}

public sealed class AddRegisterStrategyCodeFixTests
{
    [Fact]
    public async Task FixesDp006ByAddingRegisterStrategyAttribute()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            public class WechatPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "wechat";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredStrategyAnalyzer());

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.Id == "DP006");

        var document = AnalyzerTestContext.CreateDocument(source);
        var fixes = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => fixes.Add(action),
            CancellationToken.None);
        await new AddRegisterStrategyCodeFixProvider().RegisterCodeFixesAsync(context);

        var operation = await fixes[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = Assert.IsType<ApplyChangesOperation>(operation.Single());
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        Assert.Contains("[RegisterStrategy(\"wechat-payment\", typeof(IPaymentStrategy))]", fixedSource, StringComparison.Ordinal);
    }
}

public sealed class AddContractImplementationCodeFixTests
{
    [Fact]
    public async Task FixesDp004ByAddingContractInterface()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment
            {
                public string Pay(decimal amount) => "alipay";
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync(
            source,
            "DP004",
            new AddContractImplementationCodeFixProvider());

        Assert.Contains("IPaymentStrategy", fixedSource, StringComparison.Ordinal);
    }
}
