using DesignPatterns.Creational;

namespace DesignPatterns.Tests.Integration.Factory;

public interface IIntegrationProductFactory
{
    string Produce();
}

[RegisterFactory<IIntegrationProductFactory>("standard")]
public sealed class StandardIntegrationFactory : IIntegrationProductFactory
{
    public string Produce() => "Standard";
}

[RegisterFactory<IIntegrationProductFactory>("premium")]
public sealed class PremiumIntegrationFactory : IIntegrationProductFactory
{
    public string Produce() => "Premium";
}

public sealed class FactoryRegistryIntegrationTests
{
    [Fact]
    public void GeneratedRegistry_CreatesRegisteredFactories()
    {
        var registry = IntegrationProductFactoryRegistry.Create();

        Assert.True(registry.TryCreate(IntegrationProductFactoryKeys.Standard, out var standard));
        Assert.True(registry.TryCreate(IntegrationProductFactoryKeys.Premium, out var premium));

        Assert.Equal("Standard", standard!.Produce());
        Assert.Equal("Premium", premium!.Produce());
    }

    [Fact]
    public void GeneratedRegistry_ReturnsFalseForUnknownKey()
    {
        var registry = IntegrationProductFactoryRegistry.Create();

        Assert.False(registry.TryCreate("missing", out _));
    }

    [Fact]
    public void GeneratedRegistry_ThrowsForUnknownKey()
    {
        var registry = IntegrationProductFactoryRegistry.Create();

        var ex = Assert.Throws<FactoryNotFoundException>(() => registry.Create("missing"));
        Assert.NotNull(ex);
    }

    [Fact]
    public void GeneratedKeys_ContainsAllKeys()
    {
        Assert.Equal("standard", IntegrationProductFactoryKeys.Standard);
        Assert.Equal("premium", IntegrationProductFactoryKeys.Premium);
    }
}
