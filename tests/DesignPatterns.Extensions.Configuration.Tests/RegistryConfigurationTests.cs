using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DesignPatterns.Behavioral;
using DesignPatterns.Extensions.Configuration;

namespace DesignPatterns.Extensions.Configuration.Tests;

public sealed class RegistryConfigurationTests
{
    private static IStrategyRegistry<string, IProviderStrategy> CreateRegistry() =>
        new StrategyRegistry<string, IProviderStrategy>(new Dictionary<string, IProviderStrategy>
        {
            ["alpha"] = new AlphaProvider(),
            ["beta"] = new BetaProvider(),
        });

    [Fact]
    public void ResolveConfigured_ReturnsImplementation_ForValidConfigurationKey()
    {
        var registry = CreateRegistry();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ValidProvider"] = "alpha",
            })
            .Build();

        var provider = RegistryConfiguration.ResolveConfigured(registry, config, "ValidProvider");

        Assert.Equal("alpha", provider.Name);
    }

    [Fact]
    public void TryResolveConfigured_ReturnsFalse_WhenConfigurationKeyIsMissing()
    {
        var registry = CreateRegistry();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var resolved = RegistryConfiguration.TryResolveConfigured(
            registry,
            config,
            "MissingProvider",
            out var provider);

        Assert.False(resolved);
        Assert.Null(provider);
    }

    [Fact]
    public void ResolveConfigured_Throws_WhenConfigurationValueIsUnknown()
    {
        var registry = CreateRegistry();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UnknownProvider"] = "not-registered",
            })
            .Build();

        var exception = Assert.Throws<RegistryConfigurationException>(() =>
            RegistryConfiguration.ResolveConfigured(registry, config, "UnknownProvider"));

        Assert.Contains("UnknownProvider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains("alpha", exception.Message, StringComparison.Ordinal);
        Assert.Contains("beta", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveConfigured_UsesDefaultKey_WhenConfigurationKeyIsMissing()
    {
        var registry = CreateRegistry();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var resolved = RegistryConfiguration.TryResolveConfigured(
            registry,
            config,
            "MissingProvider",
            out var provider,
            defaultKey: "beta");

        Assert.True(resolved);
        Assert.Equal("beta", provider!.Name);
    }

    [Fact]
    public void ResolveConfigured_UsesDefaultKey_WhenConfigurationValueIsEmpty()
    {
        var registry = CreateRegistry();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmptyProvider"] = string.Empty,
            })
            .Build();

        var provider = RegistryConfiguration.ResolveConfigured(registry, config, "EmptyProvider", defaultKey: "alpha");

        Assert.Equal("alpha", provider.Name);
    }
}
