using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class GenerateSingletonGeneratorTests
{
    [Fact]
    public Task GeneratesThreadSafeSingleton()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public partial class AppSettings
            {
                public string AppName { get; set; } = "DesignPatterns";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateSingletonGenerator>(
            ("AppSettings.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesNonThreadSafeSingleton()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton(ThreadSafe = false)]
            public partial class FastCache
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateSingletonGenerator>(
            ("FastCache.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp001WhenNotPartial()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public class NotPartial
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateSingletonGenerator>(
            ("NotPartial.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp002WhenAppliedToStaticClass()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public static class InvalidStatic
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateSingletonGenerator>(
            ("InvalidStatic.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public void ReportsDp067WhenAsyncInitializerSignatureIsInvalid()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton(InitializeAsync = nameof(InitializeAsync))]
            public partial class InvalidAsyncInitializer
            {
                public void InitializeAsync(
                    InvalidAsyncInitializer instance,
                    System.Threading.CancellationToken cancellationToken)
                {
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateSingletonGenerator>(
            ("InvalidAsyncInitializer.cs", source));

        var diagnostics = SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DP067");
    }
}
