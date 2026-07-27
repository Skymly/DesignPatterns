using DesignPatterns.Analyzers;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers.Tests;

public sealed class UnregisteredCommandHandlerAnalyzerTests
{
    [Fact]
    public async Task ReportsDp072WhenImplementationMissingRegisterCommandHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler<PingCommand>]
            public sealed class RegisteredPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class OrphanPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task ReportsDp072ForResultHandlerMissingRegisterCommandHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class AddCommand : ICommand<int>
            {
                public int Left { get; init; }
                public int Right { get; init; }
            }

            [RegisterCommandHandler<AddCommand>]
            public sealed class RegisteredAddHandler : ICommandHandler<AddCommand, int>
            {
                public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default) =>
                    new(command.Left + command.Right);
            }

            public sealed class OrphanAddHandler : ICommandHandler<AddCommand, int>
            {
                public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default) =>
                    new(command.Left + command.Right);
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task DoesNotReportWhenRegisterCommandHandlerPresent()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler<PingCommand>]
            public sealed class RegisteredPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task DoesNotReportWhenNoRegistrationInCompilation()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class OrphanCommand : ICommand
            {
            }

            public sealed class OrphanHandler : ICommandHandler<OrphanCommand>
            {
                public ValueTask HandleAsync(OrphanCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task ReportsDp072WhenRegistrationExistsInReferencedAssembly()
    {
        const string registrationSource = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace Registrations;

            public sealed class SharedCommand : ICommand
            {
            }

            [RegisterCommandHandler<SharedCommand>]
            public sealed class RegisteredHandler : ICommandHandler<SharedCommand>
            {
                public ValueTask HandleAsync(SharedCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        const string implementationSource = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;
            using Registrations;

            namespace Implementations;

            public sealed class UnregisteredHandler : ICommandHandler<SharedCommand>
            {
                public ValueTask HandleAsync(SharedCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithReferencedAssemblyAsync(
            registrationSource,
            implementationSource,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task DoesNotReportForAbstractHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler<PingCommand>]
            public sealed class RegisteredHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            public abstract class AbstractHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task DoesNotReportForPrivateNestedHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler<PingCommand>]
            public sealed class RegisteredHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            public sealed class Outer
            {
                private sealed class PrivateNestedHandler : ICommandHandler<PingCommand>
                {
                    public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                        default;
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }

    [Fact]
    public async Task DoesNotReportWhenNonGenericRegisterCommandHandlerPresent()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PingCommand : ICommand
            {
            }

            [RegisterCommandHandler(typeof(PingCommand))]
            public sealed class RegisteredPingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredCommandHandlerAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP072"));
    }
}
