using DesignPatterns;
using DesignPatterns.Creational;

namespace DesignPatterns.Tests.Creational;

public sealed class FactoryRegistryTests
{
    private interface IProduct
    {
        string Name { get; }
    }

    private sealed class NamedProduct : IProduct
    {
        public NamedProduct(string name) => Name = name;

        public string Name { get; }
    }

    [Fact]
    public void Create_ReturnsNewInstanceEachTime()
    {
        var registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var first = registry.Create("a");
        var second = registry.Create("a");

        Assert.NotSame(first, second);
        Assert.Equal("A", first.Name);
    }

    [Fact]
    public void TryCreate_ReturnsFalseForMissingKey()
    {
        var registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        Assert.False(registry.TryCreate("missing", out _));
    }

    [Fact]
    public void Create_ThrowsWhenKeyMissing()
    {
        var registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var ex = Assert.Throws<FactoryNotFoundException>(() => registry.Create("missing"));
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_DuplicateKey_Throws()
    {
        var builder = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"));

        Assert.Throws<ArgumentException>(() => builder.Register("a", () => new NamedProduct("B")));
    }

    [Fact]
    public void Register_WithKeyFactory_PassesKeyToFactory()
    {
        var registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", key => new NamedProduct(key))
            .Build();

        var product = registry.Create("a");
        Assert.Equal("a", product.Name);
    }

    [Fact]
    public void Keys_ReturnsAllRegisteredKeys()
    {
        var registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Register("b", () => new NamedProduct("B"))
            .Build();

        Assert.Equal(2, registry.Keys.Count);
        Assert.Contains("a", registry.Keys);
        Assert.Contains("b", registry.Keys);
    }

    [Fact]
    public void TryCreate_ReturnsTrueWithProduct()
    {
        var registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        Assert.True(registry.TryCreate("a", out var product));
        Assert.Equal("A", product.Name);
    }

    [Fact]
    public void Register_NullKey_Throws()
    {
        var builder = new FactoryRegistryBuilder<string, IProduct>();

        Assert.Throws<ArgumentNullException>(() => builder.Register(null!, () => new NamedProduct("A")));
    }

    [Fact]
    public void Register_NullFactory_Throws()
    {
        var builder = new FactoryRegistryBuilder<string, IProduct>();

        Assert.Throws<ArgumentNullException>(() => builder.Register("a", (Func<IProduct>)null!));
    }

    [Fact]
    public void TryGet_ImplementsIReadOnlyRegistry()
    {
        IReadOnlyRegistry<string, IProduct> registry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        Assert.True(registry.TryGet("a", out var product));
        Assert.Equal("A", product.Name);
        Assert.False(registry.TryGet("missing", out _));
    }

    [Fact]
    public void FactoryRegistry_NullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FactoryRegistry<string, IProduct>(null!));
    }
}
