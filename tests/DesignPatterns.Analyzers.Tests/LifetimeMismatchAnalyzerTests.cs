using DesignPatterns.Analyzers;
using VerifyTests;

namespace DesignPatterns.Analyzers.Tests;

public sealed class LifetimeMismatchAnalyzerTests
{
    [Fact]
    public async Task ReportsDp060WhenSingletonRegistryWithTransientImplementations()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Usage
            {
                public static void Run(IServiceCollection services)
                {
                    StrategyRegistryHolder.RegisterDi(
                        services,
                        implementationLifetime: ServiceLifetime.Transient,
                        registryLifetime: ServiceLifetime.Singleton);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new LifetimeMismatchAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP060", "DP061"));
    }

    [Fact]
    public async Task ReportsDp060WhenSingletonRegistryWithScopedImplementations()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public static class FactoryRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Transient,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Usage
            {
                public static void Run(IServiceCollection services)
                {
                    FactoryRegistryHolder.RegisterDi(
                        services,
                        implementationLifetime: ServiceLifetime.Scoped,
                        registryLifetime: ServiceLifetime.Singleton);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new LifetimeMismatchAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP060", "DP061"));
    }

    [Fact]
    public async Task ReportsDp061WhenSingletonImplementationsWithTransientRegistry()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Usage
            {
                public static void Run(IServiceCollection services)
                {
                    StrategyRegistryHolder.RegisterDi(
                        services,
                        implementationLifetime: ServiceLifetime.Singleton,
                        registryLifetime: ServiceLifetime.Transient);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new LifetimeMismatchAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP060", "DP061"));
    }

    [Fact]
    public async Task DoesNotReportWhenLifetimesMatch()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Usage
            {
                public static void Run(IServiceCollection services)
                {
                    StrategyRegistryHolder.RegisterDi(services);
                    StrategyRegistryHolder.RegisterDi(
                        services,
                        implementationLifetime: ServiceLifetime.Singleton,
                        registryLifetime: ServiceLifetime.Singleton);
                    StrategyRegistryHolder.RegisterDi(
                        services,
                        implementationLifetime: ServiceLifetime.Scoped,
                        registryLifetime: ServiceLifetime.Scoped);
                    StrategyRegistryHolder.RegisterDi(
                        services,
                        implementationLifetime: ServiceLifetime.Transient,
                        registryLifetime: ServiceLifetime.Transient);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new LifetimeMismatchAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP060", "DP061"));
    }

    [Fact]
    public async Task DoesNotReportForSingleLifetimeOverload()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public static class StateMachineHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime lifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Usage
            {
                public static void Run(IServiceCollection services)
                {
                    StateMachineHolder.RegisterDi(services, ServiceLifetime.Transient);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new LifetimeMismatchAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP060", "DP061"));
    }

    [Fact]
    public async Task DoesNotReportForNonRegisterDiMethods()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public static class Other
            {
                public static IServiceCollection AddSomething(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Transient,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Usage
            {
                public static void Run(IServiceCollection services)
                {
                    Other.AddSomething(services);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new LifetimeMismatchAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP060", "DP061"));
    }
}
