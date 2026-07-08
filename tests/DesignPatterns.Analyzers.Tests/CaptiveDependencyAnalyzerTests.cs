using DesignPatterns.Analyzers;
using VerifyTests;

namespace DesignPatterns.Analyzers.Tests;

public sealed class CaptiveDependencyAnalyzerTests
{
    [Fact]
    public async Task ReportsDp062_WhenSingletonDependsOnScoped()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonService
            {
                public SingletonService(ScopedService scoped) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    services.AddSingleton<SingletonService>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenSingletonDependsOnTransient()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class TransientService { }
            public class SingletonService
            {
                public SingletonService(TransientService transient) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTransient<TransientService>();
                    services.AddSingleton<SingletonService>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenSingletonDependsOnSingleton()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class DependencyService { }
            public class SingletonService
            {
                public SingletonService(DependencyService dependency) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<DependencyService>();
                    services.AddSingleton<SingletonService>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenScopedDependsOnTransient()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class TransientService { }
            public class ScopedService
            {
                public ScopedService(TransientService transient) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTransient<TransientService>();
                    services.AddScoped<ScopedService>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenNoConstructorParameters()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class SingletonService
            {
                public SingletonService() { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<SingletonService>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenFactoryBasedRegistration()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonService
            {
                public SingletonService(ScopedService scoped) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    services.AddSingleton<SingletonService>(sp => new SingletonService(sp.GetRequiredService<ScopedService>()));
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WithTwoTypeArgs_WhenSingletonImplDependsOnScoped()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public interface IService { }
            public class ScopedDependency { }
            public class ServiceImpl : IService
            {
                public ServiceImpl(ScopedDependency dep) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedDependency>();
                    services.AddSingleton<IService, ServiceImpl>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WithTryAddServiceDescriptor_WhenSingletonImplDependsOnTransient()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class TransientDependency { }
            public class MyService
            {
                public MyService(TransientDependency dep) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTransient<TransientDependency>();
                    services.TryAdd(new ServiceDescriptor(typeof(MyService), typeof(MyService), ServiceLifetime.Singleton));
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_ForMultipleSingletonsWithSameScopedDependency()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonA
            {
                public SingletonA(ScopedService scoped) { }
            }
            public class SingletonB
            {
                public SingletonB(ScopedService scoped) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    services.AddSingleton<SingletonA>();
                    services.AddSingleton<SingletonB>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_ForOneScopedParamAmongMultipleParams()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonService { }
            public class MySingleton
            {
                public MySingleton(ScopedService scoped, SingletonService singleton) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    services.AddSingleton<SingletonService>();
                    services.AddSingleton<MySingleton>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenNoRegistrations()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class MyService
            {
                public MyService() { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    // No registrations
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenRegisterDiSingletonImplDependsOnScoped()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public class ScopedService { }

            [RegisterStrategy("fast", typeof(IPaymentStrategy))]
            public class FastStrategy : IPaymentStrategy
            {
                public FastStrategy(ScopedService scoped) { }
                public string Pay(decimal amount) => "fast";
            }

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    StrategyRegistryHolder.RegisterDi(services);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenRegisterDiTransientImplDependsOnScoped()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public class ScopedService { }

            [RegisterStrategy("fast", typeof(IPaymentStrategy))]
            public class FastStrategy : IPaymentStrategy
            {
                public FastStrategy(ScopedService scoped) { }
                public string Pay(decimal amount) => "fast";
            }

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    StrategyRegistryHolder.RegisterDi(services, implementationLifetime: ServiceLifetime.Transient);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenRegisterDiSingletonImplDependsOnTransient()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public class TransientService { }

            [RegisterStrategy("fast", typeof(IPaymentStrategy))]
            public class FastStrategy : IPaymentStrategy
            {
                public FastStrategy(TransientService transient) { }
                public string Pay(decimal amount) => "fast";
            }

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTransient<TransientService>();
                    StrategyRegistryHolder.RegisterDi(services);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenRegisterDiCalledButNoAttributedTypes()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    StrategyRegistryHolder.RegisterDi(services);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenRegisterDiSingletonImplDependsOnManualScoped()
    {
        // A [RegisterStrategy] implementation depends on a service that was
        // manually registered with AddScoped. The RegisterDi call registers
        // the strategy as Singleton (default). The Scoped dependency is captive.
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public class ScopedDbContext { }

            [RegisterStrategy("credit", typeof(IPaymentStrategy))]
            public class CreditCardStrategy : IPaymentStrategy
            {
                public CreditCardStrategy(ScopedDbContext db) { }
                public string Pay(decimal amount) => "credit";
            }

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedDbContext>();
                    StrategyRegistryHolder.RegisterDi(services, implementationLifetime: ServiceLifetime.Singleton);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenStrategyAndFactoryRegisterDiHaveDifferentLifetimes()
    {
        // Strategy RegisterDi registers strategies as Singleton.
        // Factory RegisterDi registers factories as Transient.
        // A strategy that depends on a factory implementation should NOT
        // trigger DP062 because the factory is Transient (shorter-lived),
        // and the strategy is Singleton — but the factory impl is NOT
        // registered as Scoped/Transient by the strategy's RegisterDi.
        // The fix ensures each RegisterDi only applies to its own category.
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using DesignPatterns.Behavioral;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [RegisterStrategy("fast", typeof(IPaymentStrategy))]
            public class FastStrategy : IPaymentStrategy
            {
                public FastStrategy() { }
                public string Pay(decimal amount) => "fast";
            }

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterFactory("widget", typeof(IWidget))]
            public class WidgetFactory : IWidget
            {
                public WidgetFactory() { }
                public IWidget Create() => this;
            }

            public interface IWidget
            {
                IWidget Create();
            }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class FactoryRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Transient,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    StrategyRegistryHolder.RegisterDi(services, implementationLifetime: ServiceLifetime.Singleton);
                    FactoryRegistryHolder.RegisterDi(services, implementationLifetime: ServiceLifetime.Transient);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenStrategyRegisterDiSingletonAndFactoryImplDependsOnScoped()
    {
        // A [RegisterFactory] implementation depends on a Scoped service.
        // The Factory RegisterDi call uses default Transient lifetime,
        // so no DP062 should fire for the factory.
        // But a [RegisterStrategy] implementation also depends on the same Scoped service,
        // and Strategy RegisterDi uses Singleton — DP062 should fire for the strategy only.
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using DesignPatterns.Behavioral;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public class ScopedService { }

            [RegisterStrategy("fast", typeof(IPaymentStrategy))]
            public class FastStrategy : IPaymentStrategy
            {
                public FastStrategy(ScopedService scoped) { }
                public string Pay(decimal amount) => "fast";
            }

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterFactory("widget", typeof(IWidget))]
            public class WidgetFactory : IWidget
            {
                public WidgetFactory(ScopedService scoped) { }
                public IWidget Create() => this;
            }

            public interface IWidget
            {
                IWidget Create();
            }

            public static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class FactoryRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Transient,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>();
                    StrategyRegistryHolder.RegisterDi(services);
                    FactoryRegistryHolder.RegisterDi(services);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenAutofacSingleInstanceDependsOnInstancePerLifetimeScope()
    {
        const string source = """
            using Autofac;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonService
            {
                public SingletonService(ScopedService scoped) { }
            }

            public static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.RegisterType<ScopedService>().InstancePerLifetimeScope();
                    builder.RegisterType<SingletonService>().SingleInstance();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenAutofacSingleInstanceDependsOnDefaultTransient()
    {
        // Autofac defaults to InstancePerDependency (Transient) when no
        // lifetime method is chained.
        const string source = """
            using Autofac;

            namespace TestAssembly;

            public class TransientService { }
            public class SingletonService
            {
                public SingletonService(TransientService transient) { }
            }

            public static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.RegisterType<TransientService>();
                    builder.RegisterType<SingletonService>().SingleInstance();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenAutofacChainHasIntermediateCalls()
    {
        // The lifetime method appears after intermediate fluent calls (As<T>()).
        const string source = """
            using Autofac;

            namespace TestAssembly;

            public interface IWorker { }
            public class ScopedService { }
            public class SingletonService : IWorker
            {
                public SingletonService(ScopedService scoped) { }
            }

            public static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.RegisterType<ScopedService>().InstancePerLifetimeScope();
                    builder.RegisterType<SingletonService>().As<IWorker>().SingleInstance();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task DoesNotReport_WhenAutofacSingleInstanceDependsOnSingleInstance()
    {
        const string source = """
            using Autofac;

            namespace TestAssembly;

            public class DependencyService { }
            public class SingletonService
            {
                public SingletonService(DependencyService dependency) { }
            }

            public static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.RegisterType<DependencyService>().SingleInstance();
                    builder.RegisterType<SingletonService>().SingleInstance();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }

    [Fact]
    public async Task ReportsDp062_WhenSingletonDependsOnScopedFactoryRegistration()
    {
        // Factory-based registrations now feed the lifetime map, so a
        // Singleton constructor-depending on a Scoped factory registration
        // is a captive dependency.
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonService
            {
                public SingletonService(ScopedService scoped) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>(sp => new ScopedService());
                    services.AddSingleton<SingletonService>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP062"));
    }
}
