using DesignPatterns.Analyzers;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers.Tests;

public sealed class UnregisteredHandlerAnalyzerTests
{
    [Fact]
    public async Task ReportsDp024WhenImplementationMissingHandlerOrder()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
            }

            [HandlerOrder<RequestContext>(10)]
            public sealed class LoggingHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class AuditHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredHandlerAnalyzer());

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "DP024" &&
                diagnostic.GetMessage().Contains("AuditHandler", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoesNotReportWhenHandlerOrderPresent()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
            }

            [HandlerOrder<RequestContext>(10)]
            public sealed class LoggingHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredHandlerAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP024");
    }

    [Fact]
    public async Task DoesNotReportWhenNoHandlerOrderInCompilation()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
            }

            public sealed class OrphanHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredHandlerAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP024");
    }
}
