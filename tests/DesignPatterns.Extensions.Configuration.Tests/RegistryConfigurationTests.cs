using System.Collections.Generic;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Configuration.Tests;

public sealed class RegistryConfigurationTests : IClassFixture<AppSettingsFixture>
{
    public RegistryConfigurationTests(AppSettingsFixture _)
    {
    }

    private static IStrategyRegistry<string, IProviderStrategy> CreateRegistry() =>
        new StrategyRegistry<string, IProviderStrategy>(new Dictionary<string, IProviderStrategy>
        {
            ["alpha"] = new AlphaProvider(),
            ["beta"] = new BetaProvider(),
        });

    [Fact]
    public void ResolveConfigured_ReturnsImplementation_ForValidAppSettingsKey()
    {
        var registry = CreateRegistry();

        var provider = RegistryConfiguration.ResolveConfigured(registry, "ValidProvider");

        Assert.Equal("alpha", provider.Name);
    }

    [Fact]
    public void TryResolveConfigured_ReturnsFalse_WhenAppSettingsEntryIsMissing()
    {
        var registry = CreateRegistry();

        var resolved = RegistryConfiguration.TryResolveConfigured(
            registry,
            "MissingProvider",
            out var provider);

        Assert.False(resolved);
        Assert.Null(provider);
    }

    [Fact]
    public void ResolveConfigured_Throws_WhenAppSettingsEntryIsMissing()
    {
        var registry = CreateRegistry();

        var exception = Assert.Throws<RegistryConfigurationException>(() =>
            RegistryConfiguration.ResolveConfigured(registry, "MissingProvider"));

        Assert.Contains("MissingProvider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("alpha", exception.Message, StringComparison.Ordinal);
        Assert.Contains("beta", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveConfigured_Throws_WhenConfiguredKeyIsUnknown()
    {
        var registry = CreateRegistry();

        var exception = Assert.Throws<RegistryConfigurationException>(() =>
            RegistryConfiguration.ResolveConfigured(registry, "UnknownProvider"));

        Assert.Contains("UnknownProvider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains("alpha", exception.Message, StringComparison.Ordinal);
        Assert.Contains("beta", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveConfigured_UsesDefaultKey_WhenAppSettingsEntryIsMissing()
    {
        var registry = CreateRegistry();

        var resolved = RegistryConfiguration.TryResolveConfigured(
            registry,
            "MissingProvider",
            out var provider,
            defaultKey: "beta");

        Assert.True(resolved);
        Assert.Equal("beta", provider!.Name);
    }

    [Fact]
    public void ResolveConfigured_UsesDefaultKey_WhenAppSettingsValueIsEmpty()
    {
        var registry = CreateRegistry();

        var provider = RegistryConfiguration.ResolveConfigured(registry, "EmptyProvider", defaultKey: "alpha");

        Assert.Equal("alpha", provider.Name);
    }
}
