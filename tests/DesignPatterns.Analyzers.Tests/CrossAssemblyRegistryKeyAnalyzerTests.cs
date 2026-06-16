using DesignPatterns.Analyzers;

namespace DesignPatterns.Analyzers.Tests;

public sealed class CrossAssemblyRegistryKeyAnalyzerTests
{
    private const string HostSource = """
        namespace Plugin.Host;

        public static class Program
        {
            public static void Main()
            {
            }
        }
        """;

    [Fact]
    public async Task ReportsDp033WhenSameStrategyKeyExistsInTwoReferencedAssemblies()
    {
        const string alphaProviderSource = """
            using DesignPatterns.Behavioral;

            namespace Plugin.Contracts
            {
                public interface ICardMotion
                {
                    string Name { get; }
                }
            }

            namespace Plugin.Providers.Alpha
            {
                using Plugin.Contracts;

                [RegisterStrategy<ICardMotion>("alpha")]
                public sealed class AlphaCard : ICardMotion
                {
                    public string Name => "alpha";
                }
            }
            """;

        const string betaProviderSource = """
            using DesignPatterns.Behavioral;

            namespace Plugin.Contracts
            {
                public interface ICardMotion
                {
                    string Name { get; }
                }
            }

            namespace Plugin.Providers.Beta
            {
                using Plugin.Contracts;

                [RegisterStrategy<ICardMotion>("alpha")]
                public sealed class BetaCard : ICardMotion
                {
                    public string Name => "alpha-beta";
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithTwoReferencedAssembliesAndHostAsync(
            alphaProviderSource,
            betaProviderSource,
            HostSource,
            new CrossAssemblyRegistryKeyAnalyzer());

        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == "DP033"));
        Assert.All(
            diagnostics.Where(diagnostic => diagnostic.Id == "DP033"),
            diagnostic =>
            {
                Assert.Contains("alpha", diagnostic.GetMessage(), StringComparison.Ordinal);
                Assert.Contains("ICardMotion", diagnostic.GetMessage(), StringComparison.Ordinal);
                Assert.Contains("ProviderAlpha", diagnostic.GetMessage(), StringComparison.Ordinal);
                Assert.Contains("ProviderBeta", diagnostic.GetMessage(), StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task DoesNotReportDp033WhenReferencedAssembliesUseDistinctKeys()
    {
        const string alphaProviderSource = """
            using DesignPatterns.Behavioral;

            namespace Plugin.Contracts
            {
                public interface ICardMotion
                {
                    string Name { get; }
                }
            }

            namespace Plugin.Providers.Alpha
            {
                using Plugin.Contracts;

                [RegisterStrategy<ICardMotion>("alpha")]
                public sealed class AlphaCard : ICardMotion
                {
                    public string Name => "alpha";
                }
            }
            """;

        const string betaProviderSource = """
            using DesignPatterns.Behavioral;

            namespace Plugin.Contracts
            {
                public interface ICardMotion
                {
                    string Name { get; }
                }
            }

            namespace Plugin.Providers.Beta
            {
                using Plugin.Contracts;

                [RegisterStrategy<ICardMotion>("beta")]
                public sealed class BetaCard : ICardMotion
                {
                    public string Name => "beta";
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithTwoReferencedAssembliesAndHostAsync(
            alphaProviderSource,
            betaProviderSource,
            HostSource,
            new CrossAssemblyRegistryKeyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP033");
    }

    [Fact]
    public async Task DoesNotReportDp033WhenDuplicateKeyExistsInSingleAssembly()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface ICardMotion
            {
                string Name { get; }
            }

            [RegisterStrategy<ICardMotion>("alpha")]
            public sealed class AlphaCard : ICardMotion
            {
                public string Name => "alpha";
            }

            [RegisterStrategy<ICardMotion>("alpha")]
            public sealed class BetaCard : ICardMotion
            {
                public string Name => "beta";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CrossAssemblyRegistryKeyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP033");
    }

    [Fact]
    public async Task DoesNotReportDp033WhenSameKeyIsUsedForDifferentContracts()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IControl
            {
                void Run();
            }

            public interface IErrorHandler
            {
                string Describe();
            }

            [RegisterStrategy<IControl>("gamma")]
            public sealed class GammaControl : IControl
            {
                public void Run() { }
            }

            [RegisterStrategy<IErrorHandler>("gamma")]
            public sealed class GammaError : IErrorHandler
            {
                public string Describe() => "gamma";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CrossAssemblyRegistryKeyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP033");
    }
}
