using Autofac;
using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using DesignPatterns.Extensions.Autofac;
using DesignPatterns.Extensions.Autofac.Tests.Factories;
using DesignPatterns.Extensions.Autofac.Tests.Handlers;
using DesignPatterns.Extensions.Autofac.Tests.StateMachines;
using DesignPatterns.Extensions.Autofac.Tests.Strategies;

namespace DesignPatterns.Extensions.Autofac.Tests;

public sealed class GeneratedRegisterAutofacIntegrationTests
{
    [Fact]
    public void PaymentStrategyRegistry_RegisterAutofac_ResolvesFromContainer()
    {
        var builder = new ContainerBuilder();
        PaymentStrategyRegistry.RegisterAutofac(builder);

        using var container = builder.Build();
        var registry = container.Resolve<IStrategyRegistry<string, IPaymentStrategy>>();

        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var alipay));
        Assert.True(registry.TryGet(PaymentStrategyKeys.Wechat, out var wechat));
        Assert.Equal("Alipay:10", alipay!.Pay(10m));
        Assert.Equal("Wechat:20", wechat!.Pay(20m));
    }

    [Fact]
    public void PaymentStrategyRegistry_NoneSharing_ReturnsNewInstancePerLookup()
    {
        var builder = new ContainerBuilder();
        PaymentStrategyRegistry.RegisterAutofac(builder, InstanceSharing.None);

        using var container = builder.Build();
        var registry = container.Resolve<IStrategyRegistry<string, IPaymentStrategy>>();

        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var first));
        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var second));
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task TextProcessorRegistry_RegisterAutofac_ExecuteAsyncViaExtension()
    {
        var builder = new ContainerBuilder();
        TextProcessorRegistry.RegisterAutofac(builder);

        using var container = builder.Build();
        var registry = container.Resolve<IStrategyRegistry<string, ITextProcessor>>();

        var length = await registry.ExecuteAsync<ITextProcessor, int, string>(TextProcessorKeys.Length, "hello");
        var doubleLength = await registry.ExecuteAsync<ITextProcessor, int, string>(TextProcessorKeys.DoubleLength, "hi");

        Assert.Equal(5, length);
        Assert.Equal(4, doubleLength);
    }

    [Fact]
    public async Task TextProcessorRegistry_Create_ExecuteAsyncViaExtension()
    {
        var builder = new ContainerBuilder();
        TextProcessorRegistry.RegisterAutofac(builder);

        using var container = builder.Build();
        var registry = TextProcessorRegistry.Create(container);

        var result = await registry.ExecuteAsync<ITextProcessor, int, string>(TextProcessorKeys.Length, "abc");

        Assert.Equal(3, result);
    }

    [Fact]
    public void ProductFactoryRegistry_RegisterAutofac_CreatesProductsFromContainer()
    {
        var builder = new ContainerBuilder();
        ProductFactoryRegistry.RegisterAutofac(builder, InstanceSharing.None);

        using var container = builder.Build();
        var registry = container.Resolve<IFactoryRegistry<string, IProductFactory>>();

        var standard = registry.Create(ProductFactoryKeys.Standard);
        var premium = registry.Create(ProductFactoryKeys.Premium);

        Assert.Equal("Standard", standard.Create());
        Assert.Equal("Premium", premium.Create());
        Assert.NotSame(
            registry.Create(ProductFactoryKeys.Standard),
            registry.Create(ProductFactoryKeys.Standard));
    }

    [Fact]
    public async Task RequestContextHandlerPipeline_RegisterAutofac_ExecutesHandlersInOrder()
    {
        var builder = new ContainerBuilder();
        RequestContextHandlerPipeline.RegisterAutofac(builder);

        using var container = builder.Build();
        var pipeline = container.Resolve<HandlerPipeline<RequestContext>>();

        var context = new RequestContext();
        await pipeline.InvokeAsync(context);

        Assert.Equal("Logging,Authorization", context.Response);
    }

    [Fact]
    public void PaymentStrategyRegistry_KeyedService_ResolvesWithServiceKey()
    {
        var builder = new ContainerBuilder();
        PaymentStrategyRegistry.RegisterAutofac(builder, serviceKey: "payment-registry");

        using var container = builder.Build();
        var registry = container.ResolveKeyed<IStrategyRegistry<string, IPaymentStrategy>>("payment-registry");

        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var alipay));
        Assert.Equal("Alipay:1", alipay!.Pay(1m));
    }

    [Fact]
    public void OrderStateMachine_RegisterAutofac_ResolvesTableAndMachine()
    {
        var builder = new ContainerBuilder();
        OrderStatusStateMachine.RegisterAutofac(builder);

        using var container = builder.Build();
        var table = container.Resolve<ITransitionTable<OrderStatus, OrderTrigger>>();
        var machine = container.Resolve<IStateMachine<OrderStatus, OrderTrigger>>();

        Assert.Equal(OrderStatus.Draft, table.InitialState);
        Assert.Equal(OrderStatus.Draft, machine.CurrentState);
        Assert.True(machine.TryTransition(OrderTrigger.Submit, out _));
        Assert.Equal(OrderStatus.Submitted, machine.CurrentState);
    }

    [Fact]
    public void OrderStateMachine_RegisterAutofac_NoneSharing_ReturnsNewMachinePerResolve()
    {
        var builder = new ContainerBuilder();
        OrderStatusStateMachine.RegisterAutofac(builder, InstanceSharing.None);

        using var container = builder.Build();
        var first = container.Resolve<IStateMachine<OrderStatus, OrderTrigger>>();
        var second = container.Resolve<IStateMachine<OrderStatus, OrderTrigger>>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void OrderStateMachine_RegisterAutofac_SharedSharing_ReturnsSameMachine()
    {
        var builder = new ContainerBuilder();
        OrderStatusStateMachine.RegisterAutofac(builder, InstanceSharing.Shared);

        using var container = builder.Build();
        var first = container.Resolve<IStateMachine<OrderStatus, OrderTrigger>>();
        var second = container.Resolve<IStateMachine<OrderStatus, OrderTrigger>>();

        Assert.Same(first, second);
    }

    [Fact]
    public void RegisterTransitionTable_ManualRegistration_ResolvesTable()
    {
        var builder = new ContainerBuilder();
        var table = OrderStatusTransitionTable.Instance;
        builder.RegisterTransitionTable(table);

        using var container = builder.Build();
        var resolved = container.Resolve<ITransitionTable<OrderStatus, OrderTrigger>>();

        Assert.Same(table.GetAllowedTriggers(OrderStatus.Draft), resolved.GetAllowedTriggers(OrderStatus.Draft));
    }

    [Fact]
    public void RegisterStateMachine_ManualRegistration_ResolvesMachineFromTable()
    {
        var builder = new ContainerBuilder();
        builder.RegisterTransitionTable(OrderStatusTransitionTable.Instance);
        builder.RegisterStateMachine<OrderStatus, OrderTrigger>(InstanceSharing.None);

        using var container = builder.Build();
        var machine = container.Resolve<IStateMachine<OrderStatus, OrderTrigger>>();

        Assert.Equal(OrderStatus.Draft, machine.CurrentState);
        Assert.True(machine.TryTransition(OrderTrigger.Submit, out _));
        Assert.Equal(OrderStatus.Submitted, machine.CurrentState);
    }

    [Fact]
    public async Task AsyncProductFactoryAsyncRegistry_RegisterAutofac_CreatesProductsFromContainer()
    {
        var builder = new ContainerBuilder();
        AsyncProductFactoryAsyncRegistry.RegisterAutofac(builder, InstanceSharing.None);

        using var container = builder.Build();
        var registry = container.Resolve<IAsyncFactoryRegistry<string, IAsyncProductFactory>>();

        var standard = await registry.CreateAsync(AsyncProductFactoryKeys.Standard);
        var premium = await registry.CreateAsync(AsyncProductFactoryKeys.Premium);

        Assert.Equal("Standard", standard.Create());
        Assert.Equal("Premium", premium.Create());
    }

    [Fact]
    public async Task PooledProductFactoryPooledRegistry_RegisterAutofac_RentsAndReturnsFromPool()
    {
        var builder = new ContainerBuilder();
        PooledProductFactoryPooledRegistry.RegisterAutofac(builder, InstanceSharing.None);

        using var container = builder.Build();
        var registry = container.Resolve<IPooledFactoryRegistry<string, IPooledProductFactory>>();

        var first = await registry.RentAsync(PooledProductFactoryKeys.Standard);
        registry.Return(PooledProductFactoryKeys.Standard, first);

        var second = await registry.RentAsync(PooledProductFactoryKeys.Standard);

        Assert.Same(first, second);
    }
}
