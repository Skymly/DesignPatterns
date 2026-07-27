using System.Threading.Tasks;
using DesignPatterns.Behavioral;
using DesignPatterns.Extensions.DependencyInjection.Tests.CommandHandlers;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Extensions.DependencyInjection.Tests;

public sealed class CommandRouterDiIntegrationTests
{
    [Fact]
    public async Task AddCommandRouter_RegisterDi_SendAsync_InvokesHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HandledCommandsCollector>();
        PingCommandHandlerRegistry.RegisterDi(services);
        services.AddCommandRouter((builder, sp) =>
            PingCommandHandlerRegistry.RegisterAll(builder, sp));

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<ICommandRouter>();

        await router.SendAsync(new PingCommand());

        var collector = provider.GetRequiredService<HandledCommandsCollector>();
        Assert.Contains("Ping", collector.Commands);
    }

    [Fact]
    public async Task AddCommandRouter_RegisterDi_SendAsync_ReturnsResult()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HandledCommandsCollector>();
        AddNumbersCommandHandlerRegistry.RegisterDi(services);
        services.AddCommandRouter((builder, sp) =>
            AddNumbersCommandHandlerRegistry.RegisterAll(builder, sp));

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<ICommandRouter>();

        var sum = await router.SendAsync<AddNumbersCommand, int>(new AddNumbersCommand(2, 3));

        Assert.Equal(5, sum);
        var collector = provider.GetRequiredService<HandledCommandsCollector>();
        Assert.Contains("Add:2+3", collector.Commands);
    }

    [Fact]
    public void RegisterDi_DefaultImplementationLifetime_IsTransient()
    {
        var services = new ServiceCollection();
        PingCommandHandlerRegistry.RegisterDi(services);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(PingCommandHandler));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public async Task AddCommandRouter_MissingHandler_SendAsync_Throws()
    {
        var services = new ServiceCollection();
        services.AddCommandRouter((_, _) => { });

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<ICommandRouter>();

        await Assert.ThrowsAsync<CommandHandlerNotFoundException>(
            async () => await router.SendAsync(new PingCommand()));
    }
}
