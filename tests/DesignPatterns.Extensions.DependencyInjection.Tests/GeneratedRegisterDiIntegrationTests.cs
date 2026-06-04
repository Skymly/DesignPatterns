using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using DesignPatterns.Extensions.DependencyInjection.Tests.Factories;
using DesignPatterns.Extensions.DependencyInjection.Tests.Handlers;
using DesignPatterns.Extensions.DependencyInjection.Tests.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Extensions.DependencyInjection.Tests;

public sealed class GeneratedRegisterDiIntegrationTests
{
    [Fact]
    public void PaymentStrategyRegistry_RegisterDi_ResolvesFromContainer()
    {
        var services = new ServiceCollection();
        PaymentStrategyRegistry.RegisterDi(services);

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IStrategyRegistry<string, IPaymentStrategy>>();

        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var alipay));
        Assert.True(registry.TryGet(PaymentStrategyKeys.Wechat, out var wechat));
        Assert.Equal("Alipay:10", alipay!.Pay(10m));
        Assert.Equal("Wechat:20", wechat!.Pay(20m));
    }

    [Fact]
    public void PaymentStrategyRegistry_TransientImplementation_ReturnsNewInstancePerLookup()
    {
        var services = new ServiceCollection();
        PaymentStrategyRegistry.RegisterDi(
            services,
            implementationLifetime: ServiceLifetime.Transient,
            registryLifetime: ServiceLifetime.Singleton);

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IStrategyRegistry<string, IPaymentStrategy>>();

        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var first));
        Assert.True(registry.TryGet(PaymentStrategyKeys.Alipay, out var second));
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ProductFactoryRegistry_RegisterDi_CreatesProductsFromContainer()
    {
        var services = new ServiceCollection();
        ProductFactoryRegistry.RegisterDi(services, implementationLifetime: ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IFactoryRegistry<string, IProductFactory>>();

        var standard = registry.Create(ProductFactoryKeys.Standard);
        var premium = registry.Create(ProductFactoryKeys.Premium);

        Assert.Equal("Standard", standard.Create());
        Assert.Equal("Premium", premium.Create());
        Assert.NotSame(
            registry.Create(ProductFactoryKeys.Standard),
            registry.Create(ProductFactoryKeys.Standard));
    }

    [Fact]
    public async Task RequestContextHandlerPipeline_RegisterDi_ExecutesHandlersInOrder()
    {
        var services = new ServiceCollection();
        RequestContextHandlerPipeline.RegisterDi(services);

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<HandlerPipeline<RequestContext>>();

        var context = new RequestContext();
        await pipeline.InvokeAsync(context);

        Assert.Equal("Logging,Authorization", context.Response);
    }

    [Fact]
    public async Task RequestContextHandlerPipeline_TransientPipeline_RebuildsHandlersEachResolve()
    {
        var services = new ServiceCollection();
        RequestContextHandlerPipeline.RegisterDi(
            services,
            implementationLifetime: ServiceLifetime.Singleton,
            registryLifetime: ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<HandlerPipeline<RequestContext>>();
        var second = provider.GetRequiredService<HandlerPipeline<RequestContext>>();

        Assert.NotSame(first, second);
    }
}
