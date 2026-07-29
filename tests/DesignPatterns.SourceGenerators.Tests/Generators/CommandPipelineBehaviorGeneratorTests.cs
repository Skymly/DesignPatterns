using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

/// <summary>
/// Seam: Verify of generated UseBehavior composition around terminal handlers
/// and DP075/DP076/DP077 generator diagnostics (issue #264).
/// </summary>
public sealed class CommandPipelineBehaviorGeneratorTests
{
    [Fact]
    public Task GeneratesOrderedBehaviorsAroundTerminalHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record PingCommand;

            [RegisterCommandHandler<PingCommand>]
            public sealed class PingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            [CommandPipelineBehavior<PingCommand>(10)]
            public sealed class OuterBehavior : ICommandPipelineBehavior<PingCommand>
            {
                public ValueTask InvokeAsync(
                    PingCommand command,
                    CommandPipelineDelegate<PingCommand> next,
                    CancellationToken cancellationToken = default) =>
                    next(command, cancellationToken);
            }

            [CommandPipelineBehavior<PingCommand>(20)]
            public sealed class InnerBehavior : ICommandPipelineBehavior<PingCommand>
            {
                public ValueTask InvokeAsync(
                    PingCommand command,
                    CommandPipelineDelegate<PingCommand> next,
                    CancellationToken cancellationToken = default) =>
                    next(command, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesResultBehaviorsWithNonGenericAttribute()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record AddCommand(int Left, int Right);

            [RegisterCommandHandler(typeof(AddCommand))]
            public sealed class AddHandler : ICommandHandler<AddCommand, int>
            {
                public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default) =>
                    new(command.Left + command.Right);
            }

            [CommandPipelineBehavior(5, typeof(AddCommand))]
            public sealed class LoggingBehavior : ICommandPipelineBehavior<AddCommand, int>
            {
                public ValueTask<int> InvokeAsync(
                    AddCommand command,
                    CommandPipelineDelegate<AddCommand, int> next,
                    CancellationToken cancellationToken = default) =>
                    next(command, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp075DuplicateOrder()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record PingCommand;

            [RegisterCommandHandler<PingCommand>]
            public sealed class PingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            [CommandPipelineBehavior<PingCommand>(10)]
            public sealed class FirstBehavior : ICommandPipelineBehavior<PingCommand>
            {
                public ValueTask InvokeAsync(
                    PingCommand command,
                    CommandPipelineDelegate<PingCommand> next,
                    CancellationToken cancellationToken = default) =>
                    next(command, cancellationToken);
            }

            [CommandPipelineBehavior<PingCommand>(10)]
            public sealed class SecondBehavior : ICommandPipelineBehavior<PingCommand>
            {
                public ValueTask InvokeAsync(
                    PingCommand command,
                    CommandPipelineDelegate<PingCommand> next,
                    CancellationToken cancellationToken = default) =>
                    next(command, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp076OrphanBehavior()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record OrphanCommand;

            [CommandPipelineBehavior<OrphanCommand>(1)]
            public sealed class OrphanBehavior : ICommandPipelineBehavior<OrphanCommand>
            {
                public ValueTask InvokeAsync(
                    OrphanCommand command,
                    CommandPipelineDelegate<OrphanCommand> next,
                    CancellationToken cancellationToken = default) =>
                    next(command, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp077ContractMismatch()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record PingCommand;

            [RegisterCommandHandler<PingCommand>]
            public sealed class PingHandler : ICommandHandler<PingCommand>
            {
                public System.Threading.Tasks.ValueTask HandleAsync(
                    PingCommand command,
                    System.Threading.CancellationToken cancellationToken = default) =>
                    default;
            }

            [CommandPipelineBehavior<PingCommand>(1)]
            public sealed class BrokenBehavior
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
