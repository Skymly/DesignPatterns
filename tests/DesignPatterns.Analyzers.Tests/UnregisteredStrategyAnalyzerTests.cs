using System.Collections.Immutable;
using DesignPatterns.Analyzers;
using DesignPatterns.CodeFixes;
using DesignPatterns.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    internal static async Task<ImmutableArray<Diagnostic>> RunAnalyzersWithReferencedAssemblyAsync(
        string referencedAssemblySource,
        string implementationAssemblySource,
        params DiagnosticAnalyzer[] analyzers)
    {
        var compilation = CreateCompilationWithReferencedAssembly(
            referencedAssemblySource,
            implementationAssemblySource);
        return await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzers))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
    }

    internal static async Task<string> ApplyGeneratorCodeFixAsync<TGenerator>(
        string source,
        string diagnosticId,
        CodeFixProvider codeFixProvider)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator());
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

    private static CSharpCompilation CreateCompilationWithReferencedAssembly(
        string referencedAssemblySource,
        string implementationAssemblySource)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var referencedTree = CSharpSyntaxTree.ParseText(
            referencedAssemblySource,
            parseOptions,
            path: "Registrations.cs");
        var referencedCompilation = CSharpCompilation.Create(
            "Registrations",
            new[] { referencedTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var referencedStream = new MemoryStream();
        var emitResult = referencedCompilation.Emit(referencedStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        referencedStream.Position = 0;
        var referencedAssembly = MetadataReference.CreateFromStream(referencedStream);

        var implementationTree = CSharpSyntaxTree.ParseText(
            implementationAssemblySource,
            parseOptions,
            path: "Implementations.cs");
        return CSharpCompilation.Create(
            "Implementations",
            new[] { implementationTree },
            References.Add(referencedAssembly),
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

    [Fact]
    public async Task ReportsDp006WhenRegistrationExistsInReferencedAssembly()
    {
        const string registrationSource = """
            using DesignPatterns.Behavioral;

            namespace Registrations;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }
            """;

        const string implementationSource = """
            using DesignPatterns.Behavioral;
            using Registrations;

            namespace Implementations;

            public class WechatPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "wechat";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithReferencedAssemblyAsync(
            registrationSource,
            implementationSource,
            new UnregisteredStrategyAnalyzer());

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "DP006" &&
                diagnostic.GetMessage().Contains("WechatPayment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoesNotReportWhenNoRegistrationInCompilationOrReferences()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            public class OrphanStrategy : IPaymentStrategy
            {
                public string Pay(decimal amount) => "orphan";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredStrategyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP006");
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

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<RegisterStrategyGenerator>(
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

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<RegisterStrategyGenerator>(
            source,
            "DP004",
            new AddContractImplementationCodeFixProvider());

        Assert.Contains("IPaymentStrategy", fixedSource, StringComparison.Ordinal);
    }
}

public sealed class HandlerCodeFixTests
{
    [Fact]
    public async Task FixesDp009ByAddingParameterlessConstructor()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext { }

            [HandlerOrder<RequestContext>(1)]
            public class AuthHandler : IHandler<RequestContext>
            {
                public AuthHandler(string ignored) { }

                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) => default;
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<HandlerOrderGenerator>(
            source,
            "DP009",
            new AddParameterlessConstructorCodeFixProvider());

        Assert.Contains("public AuthHandler()", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FixesDp008ByAddingIHandlerImplementation()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext { }

            [HandlerOrder<RequestContext>(1)]
            public class AuthHandler
            {
                public AuthHandler(string ignored) { }

                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) => default;
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<HandlerOrderGenerator>(
            source,
            "DP008",
            new AddContractImplementationCodeFixProvider());

        Assert.Contains("IHandler<", fixedSource, StringComparison.Ordinal);
        Assert.Contains("RequestContext>", fixedSource, StringComparison.Ordinal);
    }
}

public sealed class CompositeCodeFixTests
{
    [Fact]
    public async Task FixesDp013ByAddingContractInterface()
    {
        const string source = """
            using System.Collections.Generic;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IMenuNode : ICompositeNode<IMenuNode> { string Title { get; } }

            [CompositePart<IMenuNode>("root")]
            public class RootMenu
            {
                public string Title => "root";

                public IReadOnlyList<IMenuNode> Children => System.Array.Empty<IMenuNode>();
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<CompositePartGenerator>(
            source,
            "DP013",
            new AddContractImplementationCodeFixProvider());

        Assert.Contains("IMenuNode", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FixesDp014ByAddingParameterlessConstructor()
    {
        const string source = """
            using System.Collections.Generic;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IMenuNode : ICompositeNode<IMenuNode> { string Title { get; } }

            [CompositePart<IMenuNode>("root")]
            public class RootMenu : IMenuNode
            {
                public RootMenu(string ignored) { }

                public string Title => "root";

                public IReadOnlyList<IMenuNode> Children => System.Array.Empty<IMenuNode>();
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<CompositePartGenerator>(
            source,
            "DP014",
            new AddParameterlessConstructorCodeFixProvider());

        Assert.Contains("public RootMenu()", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FixesDp015ByAddingCompositeBuildable()
    {
        const string source = """
            using System.Collections.Generic;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IMenuNode : ICompositeNode<IMenuNode> { string Title { get; } }

            [CompositePart<IMenuNode>("root")]
            public class RootMenu : IMenuNode
            {
                public string Title => "root";

                public IReadOnlyList<IMenuNode> Children => System.Array.Empty<IMenuNode>();
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<CompositePartGenerator>(
            source,
            "DP015",
            new AddCompositeBuildableCodeFixProvider());

        Assert.Contains("ICompositeBuildable<", fixedSource, StringComparison.Ordinal);
        Assert.Contains("IMenuNode>", fixedSource, StringComparison.Ordinal);
    }
}

public sealed class SingletonCodeFixTests
{
    [Fact]
    public async Task FixesDp001ByAddingPartialModifier()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public class AppSettings
            {
                public string AppName { get; set; } = "DesignPatterns";
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<GenerateSingletonGenerator>(
            source,
            "DP001",
            new AddPartialModifierCodeFixProvider());

        Assert.Contains("public partial class AppSettings", fixedSource, StringComparison.Ordinal);
    }
}

public sealed class DecoratorCodeFixTests
{
    [Fact]
    public async Task FixesDp018ByAddingDecoratorInterface()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public class LoggingPaymentDecorator : IPaymentService
            {
                public int Pay(int amount) => amount;
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<DecoratorGenerator>(
            source,
            "DP018",
            new AddContractImplementationCodeFixProvider());

        Assert.Contains("IDecorator<TestAssembly.IPaymentService>", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FixesDp017ByAddingContractInterface()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public class LoggingPaymentDecorator : IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner;
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<DecoratorGenerator>(
            source,
            "DP017",
            new AddContractImplementationCodeFixProvider());

        Assert.Contains("IPaymentService", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FixesDp019ByAddingParameterlessConstructor()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public LoggingPaymentDecorator(string ignored) { }

                public IPaymentService Decorate(IPaymentService inner) => inner;
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<DecoratorGenerator>(
            source,
            "DP019",
            new AddParameterlessConstructorCodeFixProvider());

        Assert.Contains("public LoggingPaymentDecorator()", fixedSource, StringComparison.Ordinal);
    }
}
