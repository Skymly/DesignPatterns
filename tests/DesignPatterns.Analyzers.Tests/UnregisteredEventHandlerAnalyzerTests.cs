using DesignPatterns.Analyzers;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers.Tests;

public sealed class UnregisteredEventHandlerAnalyzerTests
{
    [Fact]
    public async Task ReportsDp044WhenImplementationMissingRegisterEventHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record OrderPlacedEvent(string OrderId);

            [RegisterEventHandler<OrderPlacedEvent>]
            public sealed class LogOrderHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class NotifyOrderHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredEventHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP044"));
    }

    [Fact]
    public async Task DoesNotReportWhenRegisterEventHandlerPresent()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record OrderPlacedEvent(string OrderId);

            [RegisterEventHandler<OrderPlacedEvent>]
            public sealed class LogOrderHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredEventHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP044"));
    }

    [Fact]
    public async Task DoesNotReportWhenNoRegistrationInCompilation()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record OrphanEvent(int Value);

            public sealed class OrphanHandler : IEventHandler<OrphanEvent>
            {
                public ValueTask HandleAsync(OrphanEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredEventHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP044"));
    }

    [Fact]
    public async Task ReportsDp044WhenRegistrationExistsInReferencedAssembly()
    {
        const string registrationSource = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace Registrations;

            public record SharedEvent(int Value);

            [RegisterEventHandler<SharedEvent>]
            public sealed class RegisteredHandler : IEventHandler<SharedEvent>
            {
                public ValueTask HandleAsync(SharedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        const string implementationSource = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;
            using Registrations;

            namespace Implementations;

            public sealed class UnregisteredHandler : IEventHandler<SharedEvent>
            {
                public ValueTask HandleAsync(SharedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithReferencedAssemblyAsync(
            registrationSource,
            implementationSource,
            new UnregisteredEventHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP044"));
    }

    [Fact]
    public async Task DoesNotReportForAbstractHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record OrderPlacedEvent(string OrderId);

            [RegisterEventHandler<OrderPlacedEvent>]
            public sealed class RegisteredHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }

            public abstract class AbstractHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredEventHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP044"));
    }

    [Fact]
    public async Task DoesNotReportForPrivateNestedHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record OrderPlacedEvent(string OrderId);

            [RegisterEventHandler<OrderPlacedEvent>]
            public sealed class RegisteredHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class Outer
            {
                private sealed class PrivateNestedHandler : IEventHandler<OrderPlacedEvent>
                {
                    public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                        default;
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredEventHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP044"));
    }
}
