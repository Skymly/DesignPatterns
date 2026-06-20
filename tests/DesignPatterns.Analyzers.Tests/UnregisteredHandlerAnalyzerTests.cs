using DesignPatterns.Analyzers;
using Microsoft.CodeAnalysis;
using VerifyTests;

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

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP024"));
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

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP024"));
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

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP024"));
    }

    [Fact]
    public async Task ReportsDp024WhenHandlerOrderExistsInReferencedAssembly()
    {
        const string registrationSource = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace Registrations;

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

        const string implementationSource = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;
            using Registrations;

            namespace Implementations;

            public sealed class AuditHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithReferencedAssemblyAsync(
            registrationSource,
            implementationSource,
            new UnregisteredHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP024"));
    }
}
