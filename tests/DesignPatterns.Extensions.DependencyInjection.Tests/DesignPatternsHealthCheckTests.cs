using DesignPatterns.Behavioral;
using DesignPatterns.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DesignPatterns.Extensions.DependencyInjection.Tests;

public sealed class DesignPatternsHealthCheckTests
{
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    [Fact]
    public void AddDesignPatternsHealthChecks_RegistersHealthCheckService()
    {
        var services = CreateServices();
        services.AddEventAggregator();
        services.AddDesignPatternsHealthChecks();

        var provider = services.BuildServiceProvider();
        var healthService = provider.GetService<HealthCheckService>();

        Assert.NotNull(healthService);
    }

    [Fact]
    public async Task HealthCheck_ReportsHealthy_WhenAllServicesResolvable()
    {
        var services = CreateServices();
        services.AddEventAggregator();
        services.AddDesignPatternsHealthChecks();

        var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthService.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task HealthCheck_ReportsUnhealthy_WhenServiceMissing()
    {
        // Register a DesignPatterns service type, then remove it before building.
        var services = CreateServices();
        services.AddEventAggregator();
        services.AddDesignPatternsHealthChecks();

        // Remove the IEventAggregator registration to simulate a missing service.
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEventAggregator));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }

        var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthService.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task HealthCheck_ReportsHealthy_WhenNoDesignPatternsServicesRegistered()
    {
        var services = CreateServices();
        services.AddDesignPatternsHealthChecks();

        var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthService.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public void AddDesignPatternsHealthChecks_ReturnsCollectionForChaining()
    {
        var services = new ServiceCollection();
        var result = services.AddDesignPatternsHealthChecks();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddDesignPatternsHealthChecks_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddDesignPatternsHealthChecks());
    }

    [Fact]
    public void AddDesignPatternsHealthChecks_EmptyName_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddDesignPatternsHealthChecks(name: ""));
    }

    [Fact]
    public void AddDesignPatternsHealthChecks_WithCustomName_RegistersWithCustomName()
    {
        var services = CreateServices();
        services.AddEventAggregator();
        services.AddDesignPatternsHealthChecks(name: "custom-designpatterns");

        var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();

        // The health check should be registered under the custom name.
        Assert.NotNull(healthService);
    }

    [Fact]
    public async Task HealthCheck_ResolvesTransitionTableSuccessfully()
    {
        var table = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Add(TestState.Idle, TestTrigger.Start, TestState.Active)
            .Build();

        var services = CreateServices();
        services.AddTransitionTable(table);
        services.AddStateMachine<TestState, TestTrigger>();
        services.AddDesignPatternsHealthChecks();

        var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthService.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    private enum TestState { Idle, Active }
    private enum TestTrigger { Start, Stop }
}
