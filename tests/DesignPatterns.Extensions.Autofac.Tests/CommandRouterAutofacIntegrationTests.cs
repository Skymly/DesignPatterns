using System.Threading.Tasks;
using Autofac;
using DesignPatterns.Behavioral;
using DesignPatterns.Extensions.Autofac;
using DesignPatterns.Extensions.Autofac.Tests.CommandHandlers;

namespace DesignPatterns.Extensions.Autofac.Tests;

public sealed class CommandRouterAutofacIntegrationTests
{
    [Fact]
    public async Task RegisterCommandRouter_RegisterAutofac_SendAsync_InvokesHandler()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new HandledCommandsCollector()).AsSelf().SingleInstance();
        PingCommandHandlerRegistry.RegisterAutofac(builder);
        builder.RegisterCommandRouter((routerBuilder, scope) =>
            PingCommandHandlerRegistry.RegisterAll(routerBuilder, scope));

        using var container = builder.Build();
        var router = container.Resolve<ICommandRouter>();

        await router.SendAsync(new PingCommand());

        var collector = container.Resolve<HandledCommandsCollector>();
        Assert.Contains("Ping", collector.Commands);
    }

    [Fact]
    public async Task RegisterCommandRouter_RegisterAutofac_SendAsync_ReturnsResult()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new HandledCommandsCollector()).AsSelf().SingleInstance();
        AddNumbersCommandHandlerRegistry.RegisterAutofac(builder);
        builder.RegisterCommandRouter((routerBuilder, scope) =>
            AddNumbersCommandHandlerRegistry.RegisterAll(routerBuilder, scope));

        using var container = builder.Build();
        var router = container.Resolve<ICommandRouter>();

        var sum = await router.SendAsync<AddNumbersCommand, int>(new AddNumbersCommand(2, 3));

        Assert.Equal(5, sum);
        var collector = container.Resolve<HandledCommandsCollector>();
        Assert.Contains("Add:2+3", collector.Commands);
    }

    [Fact]
    public async Task RegisterCommandRouter_MissingHandler_SendAsync_Throws()
    {
        var builder = new ContainerBuilder();
        builder.RegisterCommandRouter((_, _) => { });

        using var container = builder.Build();
        var router = container.Resolve<ICommandRouter>();

        await Assert.ThrowsAsync<CommandHandlerNotFoundException>(
            async () => await router.SendAsync(new PingCommand()));
    }

    [Fact]
    public void RegisterCommandRouter_DefaultSharing_IsSingleInstance()
    {
        var builder = new ContainerBuilder();
        builder.RegisterCommandRouter((_, _) => { });

        using var container = builder.Build();
        var first = container.Resolve<ICommandRouter>();
        var second = container.Resolve<ICommandRouter>();

        Assert.Same(first, second);
    }
}
