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
}
