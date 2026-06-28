using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesignPatterns;
using DesignPatterns.Creational;

namespace DesignPatterns.Tests.Creational;

public sealed class FactoryRegistryAsyncTests
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

    private sealed class ResettableProduct : IProduct, IResettable
    {
        public ResettableProduct(string name) => Name = name;

        public string Name { get; private set; }

        public bool WasReset { get; private set; }

        public void Reset()
        {
            WasReset = true;
            Name = "reset";
        }
    }

    private sealed class AsyncProductFactory : IAsyncFactory<IProduct>
    {
        private readonly string _name;

        public AsyncProductFactory(string name) => _name = name;

        public ValueTask<IProduct> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IProduct>(new NamedProduct(_name));
        }
    }

    private sealed class CancelAwareAsyncFactory : IAsyncFactory<IProduct>
    {
        public ValueTask<IProduct> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IProduct>(new NamedProduct("cancelled-check"));
        }
    }

    // --- AsyncFactoryRegistry (non-pooled) ---

    [Fact]
    public async Task CreateAsync_ReturnsNewInstanceEachTime()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var first = await registry.CreateAsync("a");
        var second = await registry.CreateAsync("a");

        Assert.NotSame(first, second);
        Assert.Equal("A", first.Name);
    }

    [Fact]
    public async Task TryCreateAsync_ReturnsFalseForMissingKey()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var (success, product) = await registry.TryCreateAsync("missing");

        Assert.False(success);
        Assert.Null(product);
    }

    [Fact]
    public async Task TryCreateAsync_ReturnsTrueWithProduct()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var (success, product) = await registry.TryCreateAsync("a");

        Assert.True(success);
        Assert.NotNull(product);
        Assert.Equal("A", product!.Name);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenKeyMissing()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var ex = await Assert.ThrowsAsync<FactoryNotFoundException>(
            () => registry.CreateAsync("missing").AsTask());

        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_ForwardsCancellationToken()
    {
        var factory = new CancelAwareAsyncFactory();
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", factory)
            .Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => registry.CreateAsync("a", cts.Token).AsTask());
    }

    [Fact]
    public async Task CreateAsync_WithAsyncFactoryDelegate()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", async ct =>
            {
                await Task.Delay(10, ct);
                return new NamedProduct("async");
            })
            .Build();

        var product = await registry.CreateAsync("a");

        Assert.Equal("async", product.Name);
    }

    [Fact]
    public void Register_DuplicateKey_Throws()
    {
        var builder = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"));

        Assert.Throws<ArgumentException>(() => builder.Register("a", () => new NamedProduct("B")));
    }

    [Fact]
    public void Register_NullKey_Throws()
    {
        var builder = new AsyncFactoryRegistryBuilder<string, IProduct>();

        Assert.Throws<ArgumentNullException>(() => builder.Register(null!, () => new NamedProduct("A")));
    }

    [Fact]
    public void Register_NullFactory_Throws()
    {
        var builder = new AsyncFactoryRegistryBuilder<string, IProduct>();

        Assert.Throws<ArgumentNullException>(() => builder.Register("a", (Func<IProduct>)null!));
    }

    [Fact]
    public void WithPooling_NegativePoolSize_Throws()
    {
        var builder = new AsyncFactoryRegistryBuilder<string, IProduct>();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithPooling(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithPooling(-1));
    }

    [Fact]
    public void Keys_ReturnsAllRegisteredKeys()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Register("b", () => new NamedProduct("B"))
            .Build();

        Assert.Equal(2, registry.Keys.Count);
    }

    // --- PooledAsyncFactoryRegistry ---

    [Fact]
    public async Task RentAsync_FirstCall_CreatesNewInstance()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 4)
            .Build();

        var product = await registry.CreateAsync("a");

        Assert.NotNull(product);
        Assert.Equal("A", product.Name);
    }

    [Fact]
    public async Task RentAsync_AfterReturn_ReusesPooledInstance()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 4)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        var first = await pooled.RentAsync("a");
        pooled.Return("a", first);

        var second = await pooled.RentAsync("a");

        Assert.Same(first, second);
    }

    [Fact]
    public async Task RentAsync_PoolEmpty_CreatesNewInstance()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 4)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        var first = await pooled.RentAsync("a");
        var second = await pooled.RentAsync("a");

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Return_CallsResetWhenIResettable()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new ResettableProduct("original"))
            .WithPooling(poolSize: 4)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        var first = (ResettableProduct)await pooled.RentAsync("a");
        Assert.False(first.WasReset);

        pooled.Return("a", first);

        Assert.True(first.WasReset);
        Assert.Equal("reset", first.Name);
    }

    [Fact]
    public async Task Return_PoolFull_DiscardsProduct()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 1)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        var first = await pooled.RentAsync("a");
        var second = await pooled.RentAsync("a");

        pooled.Return("a", first);
        pooled.Return("a", second);

        // Pool size is 1, so only one was retained. Rent should return one of them.
        var rented = await pooled.RentAsync("a");
        Assert.NotNull(rented);

        // Next rent should create new (pool is now empty).
        var newOne = await pooled.RentAsync("a");
        Assert.NotSame(rented, newOne);
    }

    [Fact]
    public async Task Return_UnregisteredKey_DiscardsProduct()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 4)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        // Should not throw.
        pooled.Return("unknown", new NamedProduct("orphan"));
    }

    [Fact]
    public async Task RentAsync_ThrowsForUnregisteredKey()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 4)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        await Assert.ThrowsAsync<FactoryNotFoundException>(
            () => pooled.RentAsync("missing").AsTask());
    }

    [Fact]
    public async Task RentAsync_ConcurrentAccess_ThreadSafe()
    {
        var counter = 0;
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () =>
            {
                Interlocked.Increment(ref counter);
                return new NamedProduct("A");
            })
            .WithPooling(poolSize: 10)
            .Build();

        var pooled = (IPooledFactoryRegistry<string, IProduct>)registry;

        // Rent 10 concurrently.
        var rented = new List<IProduct>();
        var tasks = new List<Task<IProduct>>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(pooled.RentAsync("a").AsTask());
        }
        rented.AddRange(await Task.WhenAll(tasks));

        Assert.Equal(10, rented.Count);

        // Return all.
        foreach (var product in rented)
        {
            pooled.Return("a", product);
        }

        // Rent again — should all come from pool (no new creations).
        var beforeCount = counter;
        tasks.Clear();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(pooled.RentAsync("a").AsTask());
        }
        var rerented = await Task.WhenAll(tasks);

        Assert.Equal(10, rerented.Length);
        Assert.Equal(beforeCount, counter); // No new factory calls.
    }

    [Fact]
    public async Task PooledRegistry_SupportsCreateAsync()
    {
        var registry = new AsyncFactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .WithPooling(poolSize: 4)
            .Build();

        // CreateAsync always creates new (doesn't use pool).
        var first = await registry.CreateAsync("a");
        var second = await registry.CreateAsync("a");

        Assert.NotSame(first, second);
    }

    // --- FactoryRegistryAsyncExtensions (sync→async adapter) ---

    [Fact]
    public async Task AsAsync_AdaptsSyncRegistry()
    {
        var syncRegistry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var asyncRegistry = syncRegistry.AsAsync();

        var product = await asyncRegistry.CreateAsync("a");

        Assert.Equal("A", product.Name);
    }

    [Fact]
    public async Task AsAsync_TryCreateAsync_ReturnsFalseForMissingKey()
    {
        var syncRegistry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var asyncRegistry = syncRegistry.AsAsync();

        var (success, product) = await asyncRegistry.TryCreateAsync("missing");

        Assert.False(success);
        Assert.Null(product);
    }

    [Fact]
    public async Task AsAsync_CreateAsync_ThrowsForMissingKey()
    {
        var syncRegistry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Build();

        var asyncRegistry = syncRegistry.AsAsync();

        await Assert.ThrowsAsync<FactoryNotFoundException>(
            () => asyncRegistry.CreateAsync("missing").AsTask());
    }

    [Fact]
    public void AsAsync_NullRegistry_Throws()
    {
        IFactoryRegistry<string, IProduct>? registry = null;

        Assert.Throws<ArgumentNullException>(() => registry!.AsAsync());
    }

    [Fact]
    public async Task AsAsync_Keys_MatchesSyncRegistry()
    {
        var syncRegistry = new FactoryRegistryBuilder<string, IProduct>()
            .Register("a", () => new NamedProduct("A"))
            .Register("b", () => new NamedProduct("B"))
            .Build();

        var asyncRegistry = syncRegistry.AsAsync();

        Assert.Equal(2, asyncRegistry.Keys.Count);
    }
}
