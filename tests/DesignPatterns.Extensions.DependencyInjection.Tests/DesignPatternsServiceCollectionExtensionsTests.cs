using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Extensions.DependencyInjection.Tests;

public sealed class DesignPatternsServiceCollectionExtensionsTests
{
    #region Strategy Registry Tests

    [Fact]
    public void AddStrategyRegistry_RegistersAndResolves()
    {
        var services = new ServiceCollection();

        services.AddStrategyRegistry<string, ITestStrategy>(builder =>
        {
            builder.Register("a", new TestStrategy("A"));
            builder.Register("b", new TestStrategy("B"));
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IStrategyRegistry<string, ITestStrategy>>();

        Assert.Equal(2, registry.Keys.Count);
        Assert.Equal("A", registry.Get("a").Name);
        Assert.Equal("B", registry.Get("b").Name);
    }

    [Fact]
    public void AddStrategyRegistry_DefaultLifetime_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddStrategyRegistry<string, ITestStrategy>(builder =>
        {
            builder.Register("a", new TestStrategy("A"));
        });

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IStrategyRegistry<string, ITestStrategy>>();
        var second = provider.GetRequiredService<IStrategyRegistry<string, ITestStrategy>>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddStrategyRegistry_TransientLifetime_ReturnsNewInstance()
    {
        var services = new ServiceCollection();

        services.AddStrategyRegistry<string, ITestStrategy>(builder =>
        {
            builder.Register("a", new TestStrategy("A"));
        }, ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IStrategyRegistry<string, ITestStrategy>>();
        var second = provider.GetRequiredService<IStrategyRegistry<string, ITestStrategy>>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddStrategyRegistry_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>("services", () =>
            DesignPatternsServiceCollectionExtensions.AddStrategyRegistry<string, ITestStrategy>(
                null!, _ => { }));
    }

    [Fact]
    public void AddStrategyRegistry_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>("configure", () =>
            services.AddStrategyRegistry<string, ITestStrategy>(null!));
    }

    #endregion

    #region Factory Registry Tests

    [Fact]
    public void AddFactoryRegistry_RegistersAndResolves()
    {
        var services = new ServiceCollection();

        services.AddFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
            builder.Register("gadget", () => new TestProduct("Gadget"));
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IFactoryRegistry<string, ITestProduct>>();

        Assert.Equal(2, registry.Keys.Count);

        var widget = registry.Create("widget");
        Assert.Equal("Widget", widget.Name);

        var gadget = registry.Create("gadget");
        Assert.Equal("Gadget", gadget.Name);
    }

    [Fact]
    public void AddFactoryRegistry_CreatesNewInstancePerCall()
    {
        var services = new ServiceCollection();

        services.AddFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IFactoryRegistry<string, ITestProduct>>();

        var first = registry.Create("widget");
        var second = registry.Create("widget");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddFactoryRegistry_DefaultLifetime_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        });

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IFactoryRegistry<string, ITestProduct>>();
        var second = provider.GetRequiredService<IFactoryRegistry<string, ITestProduct>>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddFactoryRegistry_TransientLifetime_ReturnsNewInstance()
    {
        var services = new ServiceCollection();

        services.AddFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        }, ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IFactoryRegistry<string, ITestProduct>>();
        var second = provider.GetRequiredService<IFactoryRegistry<string, ITestProduct>>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddFactoryRegistry_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>("services", () =>
            DesignPatternsServiceCollectionExtensions.AddFactoryRegistry<string, ITestProduct>(
                null!, _ => { }));
    }

    [Fact]
    public void AddFactoryRegistry_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>("configure", () =>
            services.AddFactoryRegistry<string, ITestProduct>(null!));
    }

    #endregion

    #region Async Factory Registry Tests

    [Fact]
    public async Task AddAsyncFactoryRegistry_RegistersAndResolves()
    {
        var services = new ServiceCollection();

        services.AddAsyncFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
            builder.Register("gadget", () => new TestProduct("Gadget"));
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IAsyncFactoryRegistry<string, ITestProduct>>();

        Assert.Equal(2, registry.Keys.Count);

        var widget = await registry.CreateAsync("widget");
        Assert.Equal("Widget", widget.Name);

        var gadget = await registry.CreateAsync("gadget");
        Assert.Equal("Gadget", gadget.Name);
    }

    [Fact]
    public async Task AddAsyncFactoryRegistry_CreatesNewInstancePerCall()
    {
        var services = new ServiceCollection();

        services.AddAsyncFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IAsyncFactoryRegistry<string, ITestProduct>>();

        var first = await registry.CreateAsync("widget");
        var second = await registry.CreateAsync("widget");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddAsyncFactoryRegistry_DefaultLifetime_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAsyncFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        });

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IAsyncFactoryRegistry<string, ITestProduct>>();
        var second = provider.GetRequiredService<IAsyncFactoryRegistry<string, ITestProduct>>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddAsyncFactoryRegistry_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>("services", () =>
            DesignPatternsServiceCollectionExtensions.AddAsyncFactoryRegistry<string, ITestProduct>(
                null!, _ => { }));
    }

    [Fact]
    public void AddAsyncFactoryRegistry_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>("configure", () =>
            services.AddAsyncFactoryRegistry<string, ITestProduct>(null!));
    }

    #endregion

    #region Pooled Factory Registry Tests

    [Fact]
    public async Task AddPooledFactoryRegistry_RegistersAndResolves()
    {
        var services = new ServiceCollection();

        services.AddPooledFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        }, poolSize: 4);

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IPooledFactoryRegistry<string, ITestProduct>>();

        var widget = await registry.RentAsync("widget");
        Assert.Equal("Widget", widget.Name);
        registry.Return("widget", widget);
    }

    [Fact]
    public async Task AddPooledFactoryRegistry_RentAfterReturn_ReusesInstance()
    {
        var services = new ServiceCollection();

        services.AddPooledFactoryRegistry<string, ITestProduct>(builder =>
        {
            builder.Register("widget", () => new TestProduct("Widget"));
        }, poolSize: 4);

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IPooledFactoryRegistry<string, ITestProduct>>();

        var first = await registry.RentAsync("widget");
        registry.Return("widget", first);

        var second = await registry.RentAsync("widget");

        Assert.Same(first, second);
    }

    [Fact]
    public void AddPooledFactoryRegistry_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>("services", () =>
            DesignPatternsServiceCollectionExtensions.AddPooledFactoryRegistry<string, ITestProduct>(
                null!, _ => { }, 4));
    }

    [Fact]
    public void AddPooledFactoryRegistry_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>("configure", () =>
            services.AddPooledFactoryRegistry<string, ITestProduct>(null!, 4));
    }

    [Fact]
    public void AddPooledFactoryRegistry_InvalidPoolSize_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddPooledFactoryRegistry<string, ITestProduct>(
                builder => { }, 0));
    }

    #endregion

    #region Handler Pipeline Tests

    [Fact]
    public void AddHandlerPipeline_RegistersAndResolves()
    {
        var services = new ServiceCollection();

        services.AddHandlerPipeline<TestContext>(builder =>
        {
            builder.Use(new TestHandler("First"));
            builder.Use(new TestHandler("Second"));
        });

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<HandlerPipeline<TestContext>>();

        Assert.NotNull(pipeline);
    }

    [Fact]
    public async Task AddHandlerPipeline_ExecutesHandlersInOrder()
    {
        var services = new ServiceCollection();

        services.AddHandlerPipeline<TestContext>(builder =>
        {
            builder.Use(new TestHandler("First"));
            builder.Use(new TestHandler("Second"));
        });

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<HandlerPipeline<TestContext>>();

        var context = new TestContext();
        await pipeline.InvokeAsync(context);

        Assert.Equal(new[] { "First", "Second" }, context.ExecutedHandlers);
    }

    [Fact]
    public void AddHandlerPipeline_DefaultLifetime_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddHandlerPipeline<TestContext>(builder =>
        {
            builder.Use(new TestHandler("First"));
        });

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<HandlerPipeline<TestContext>>();
        var second = provider.GetRequiredService<HandlerPipeline<TestContext>>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddHandlerPipeline_TransientLifetime_ReturnsNewInstance()
    {
        var services = new ServiceCollection();

        services.AddHandlerPipeline<TestContext>(builder =>
        {
            builder.Use(new TestHandler("First"));
        }, ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<HandlerPipeline<TestContext>>();
        var second = provider.GetRequiredService<HandlerPipeline<TestContext>>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddHandlerPipeline_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>("services", () =>
            DesignPatternsServiceCollectionExtensions.AddHandlerPipeline<TestContext>(
                null!, _ => { }));
    }

    [Fact]
    public void AddHandlerPipeline_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>("configure", () =>
            services.AddHandlerPipeline<TestContext>(null!));
    }

    #endregion

    #region EventAggregator Tests

    [Fact]
    public void AddEventAggregator_RegistersAndResolves()
    {
        var services = new ServiceCollection();

        services.AddEventAggregator();

        var provider = services.BuildServiceProvider();
        var aggregator = provider.GetRequiredService<IEventAggregator>();

        Assert.NotNull(aggregator);
        Assert.IsType<EventAggregator>(aggregator);
    }

    [Fact]
    public void AddEventAggregator_DefaultLifetime_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddEventAggregator();

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IEventAggregator>();
        var second = provider.GetRequiredService<IEventAggregator>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddEventAggregator_TransientLifetime_ReturnsNewInstance()
    {
        var services = new ServiceCollection();

        services.AddEventAggregator(ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IEventAggregator>();
        var second = provider.GetRequiredService<IEventAggregator>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task AddEventAggregator_PublishesEvents()
    {
        var services = new ServiceCollection();

        services.AddEventAggregator();

        var provider = services.BuildServiceProvider();
        var aggregator = provider.GetRequiredService<IEventAggregator>();

        var received = new List<string>();
        aggregator.Subscribe(new TestEventHandler(received));

        await aggregator.PublishAsync(new TestEvent("Hello"));

        Assert.Single(received);
        Assert.Equal("Hello", received[0]);
    }

    [Fact]
    public void AddEventAggregator_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>("services", () =>
            DesignPatternsServiceCollectionExtensions.AddEventAggregator(null!));
    }

    #endregion

    #region Test Helpers

    private interface ITestStrategy
    {
        string Name { get; }
    }

    private sealed class TestStrategy : ITestStrategy
    {
        public TestStrategy(string name) => Name = name;
        public string Name { get; }
    }

    private interface ITestProduct
    {
        string Name { get; }
    }

    private sealed class TestProduct : ITestProduct
    {
        public TestProduct(string name) => Name = name;
        public string Name { get; }
    }

    private sealed class TestContext
    {
        public List<string> ExecutedHandlers { get; } = new();
    }

    private sealed class TestHandler : IHandler<TestContext>
    {
        private readonly string _name;

        public TestHandler(string name) => _name = name;

        public async ValueTask InvokeAsync(
            TestContext context,
            HandlerDelegate<TestContext> next,
            CancellationToken cancellationToken = default)
        {
            context.ExecutedHandlers.Add(_name);
            await next(context, cancellationToken);
        }
    }

    private sealed class TestEvent
    {
        public TestEvent(string message) => Message = message;
        public string Message { get; }
    }

    private sealed class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly List<string> _received;

        public TestEventHandler(List<string> received) => _received = received;

        public ValueTask HandleAsync(TestEvent evt, CancellationToken cancellationToken = default)
        {
            _received.Add(evt.Message);
            return default;
        }
    }

    #endregion
}
