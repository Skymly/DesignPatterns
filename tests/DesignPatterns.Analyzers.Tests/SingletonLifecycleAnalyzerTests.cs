using System.Linq;
using DesignPatterns.Analyzers;

namespace DesignPatterns.Analyzers.Tests;

public sealed class SingletonLifecycleAnalyzerTests
{
    [Fact]
    public async Task ReportsDp068AndDp069_ForGeneratedNonThreadSafeDiSingleton()
    {
        const string source = """
            using DesignPatterns.Creational;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            [GenerateSingleton(ThreadSafe = false)]
            public partial class Settings
            {
                private int _version;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<Settings>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new SingletonLifecycleAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DP068");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DP069");
    }

    [Fact]
    public async Task ReportsDp070AndDp071_ForMutableStaticDiSingleton()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace TestAssembly;

            public class Settings
            {
                public static Settings Instance = new Settings();
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<Settings>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new SingletonLifecycleAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DP070");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DP071");
    }

    [Fact]
    public async Task DoesNotReportDp069_ForConstInstanceData()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton(ThreadSafe = false)]
            public partial class Settings
            {
                private const int Version = 1;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new SingletonLifecycleAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP069");
    }
}
