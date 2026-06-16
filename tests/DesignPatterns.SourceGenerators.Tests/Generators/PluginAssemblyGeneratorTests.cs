using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class PluginAssemblyGeneratorTests
{
    [Fact]
    public Task GeneratesRegistryInProviderAssemblyWhenContractIsExternal()
    {
        const string contractSource = """
            namespace Plugin.Contracts;

            public interface ICardMotion
            {
                string Name { get; }
            }
            """;

        const string providerSource = """
            using DesignPatterns.Behavioral;
            using Plugin.Contracts;

            namespace Plugin.Providers.Alpha;

            [RegisterStrategy<ICardMotion>("alpha")]
            public sealed class AlphaCard : ICardMotion
            {
                public string Name => "alpha";
            }
            """;

        var runResult = SourceGeneratorTestContext.RunWithReferencedAssembly<RegisterStrategyGenerator>(
            ("Contracts.cs", contractSource),
            ("AlphaCard.cs", providerSource),
            referencedAssemblyName: "Plugin.Contracts",
            implementationAssemblyName: "Plugin.Providers.Alpha");

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public void ProviderAssemblyDoesNotIncludeKeysFromOtherProviderCompilation()
    {
        const string contractSource = """
            namespace Plugin.Contracts;

            public interface ICardMotion
            {
                string Name { get; }
            }
            """;

        const string alphaProviderSource = """
            using DesignPatterns.Behavioral;
            using Plugin.Contracts;

            namespace Plugin.Providers.Alpha;

            [RegisterStrategy<ICardMotion>("alpha")]
            public sealed class AlphaCard : ICardMotion
            {
                public string Name => "alpha";
            }
            """;

        const string betaProviderSource = """
            using DesignPatterns.Behavioral;
            using Plugin.Contracts;

            namespace Plugin.Providers.Beta;

            [RegisterStrategy<ICardMotion>("beta")]
            public sealed class BetaCard : ICardMotion
            {
                public string Name => "beta";
            }
            """;

        var alphaResult = SourceGeneratorTestContext.RunWithReferencedAssembly<RegisterStrategyGenerator>(
            ("Contracts.cs", contractSource),
            ("AlphaCard.cs", alphaProviderSource),
            referencedAssemblyName: "Plugin.Contracts",
            implementationAssemblyName: "Plugin.Providers.Alpha");

        var betaResult = SourceGeneratorTestContext.RunWithReferencedAssembly<RegisterStrategyGenerator>(
            ("Contracts.cs", contractSource),
            ("BetaCard.cs", betaProviderSource),
            referencedAssemblyName: "Plugin.Contracts",
            implementationAssemblyName: "Plugin.Providers.Beta");

        var alphaSources = SourceGeneratorTestContext.GetGeneratedSources(alphaResult);
        var betaSources = SourceGeneratorTestContext.GetGeneratedSources(betaResult);

        Assert.Contains("alpha", alphaSources.Values.First(), StringComparison.Ordinal);
        Assert.DoesNotContain("beta", alphaSources.Values.First(), StringComparison.Ordinal);
        Assert.Contains("beta", betaSources.Values.First(), StringComparison.Ordinal);
        Assert.DoesNotContain("alpha", betaSources.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public Task CompanionContractsShareProviderKeyInSameAssembly()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace Plugin.Providers.Gamma;

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

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("GammaProvider.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }
}
