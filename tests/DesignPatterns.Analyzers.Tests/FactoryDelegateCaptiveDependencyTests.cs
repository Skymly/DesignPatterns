using DesignPatterns.Analyzers;
using VerifyTests;

namespace DesignPatterns.Analyzers.Tests;

/// <summary>
/// DP066: Singleton factory delegates that resolve Scoped/Transient services.
/// </summary>
public sealed class FactoryDelegateCaptiveDependencyTests
{
    [Fact]
    public async Task ReportsDp066_WhenMsdiSingletonFactoryResolvesScoped()
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

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP066"));
    }

    [Fact]
    public async Task ReportsDp066_WhenMsdiSingletonFactoryResolvesTransientViaGetService()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class TransientService { }
            public class SingletonService
            {
                public SingletonService(TransientService? transient) { }
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTransient<TransientService>();
                    services.AddSingleton<SingletonService>(sp => new SingletonService(sp.GetService<TransientService>()));
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP066"));
    }

    [Fact]
    public async Task ReportsDp066_WhenAutofacRegisterDelegateSingleInstanceResolvesScoped()
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
                    builder.Register(c => new SingletonService(c.Resolve<ScopedService>())).SingleInstance();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP066"));
    }

    [Fact]
    public async Task DoesNotReport_WhenSingletonFactoryResolvesSingleton()
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
                    services.AddSingleton<SingletonService>(sp => new SingletonService(sp.GetRequiredService<DependencyService>()));
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP066"));
    }

    [Fact]
    public async Task DoesNotReport_WhenScopedFactoryResolvesTransient()
    {
        // Only Singleton factory delegates capture; a Scoped factory resolving
        // Transient is not analyzed.
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
                    services.AddScoped<ScopedService>(sp => new ScopedService(sp.GetRequiredService<TransientService>()));
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP066"));
    }

    [Fact]
    public async Task DoesNotReport_WhenResolveCallIsNotContainerMethod()
    {
        // A method named Resolve/GetRequiredService outside MSDI/Autofac
        // namespaces must not trigger DP066.
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class ScopedService { }
            public class SingletonService { }

            public class CustomLocator
            {
                public T Resolve<T>() where T : new() => new T();
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services, CustomLocator locator)
                {
                    services.AddScoped<ScopedService>();
                    services.AddSingleton<SingletonService>(sp => locator.Resolve<SingletonService>());
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new CaptiveDependencyAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP066"));
    }
}
