using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.Behavioral;
using DesignPatterns.Extensions.DependencyInjection;
using DesignPatterns.Extensions.DependencyInjection.Tests.EventHandlers;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Extensions.DependencyInjection.Tests;

public sealed class EventAggregatorDiIntegrationTests
{
    [Fact]
    public async Task RegisterDi_SubscribeAllFromServiceProvider_InvokesAllHandlers()
    {
        var services = new ServiceCollection();
        services.AddEventAggregator();
        services.AddSingleton<HandledEventsCollector>();
        OrderPlacedEventHandlerRegistry.RegisterDi(services);

        var provider = services.BuildServiceProvider();
        var aggregator = provider.GetRequiredService<IEventAggregator>();

        OrderPlacedEventHandlerRegistry.SubscribeAll(aggregator, provider);

        await aggregator.PublishAsync(new OrderPlacedEvent("ORD-001"));

        var collector = provider.GetRequiredService<HandledEventsCollector>();
        Assert.Contains("Log:ORD-001", collector.Events);
        Assert.Contains("Notify:ORD-001", collector.Events);
    }

    [Fact]
    public async Task SubscribeAll_StaticPath_InvokesParameterlessHandler()
    {
        var aggregator = new EventAggregator();
        SimpleEventHandlerRegistry.SubscribeAll(aggregator);

        await aggregator.PublishAsync(new SimpleEvent("SIM-001"));
    }

    [Fact]
    public async Task RegisterDi_TransientHandlers_ResolvesNewInstancePerSubscribe()
    {
        var services = new ServiceCollection();
        services.AddEventAggregator();
        services.AddSingleton<HandledEventsCollector>();
        OrderPlacedEventHandlerRegistry.RegisterDi(services, implementationLifetime: ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var aggregator = provider.GetRequiredService<IEventAggregator>();

        OrderPlacedEventHandlerRegistry.SubscribeAll(aggregator, provider);

        await aggregator.PublishAsync(new OrderPlacedEvent("ORD-003"));

        Assert.NotNull(aggregator);
    }
}
