using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

/// <summary>
/// Seam: Verify of generated command↔handler registry glue and bijection diagnostics (issue #259).
/// </summary>
public sealed class RegisterCommandHandlerGeneratorTests
{
    [Fact]
    public Task GeneratesHandlerRegistryWithGenericAttribute()
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
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesHandlerRegistryWithNonGenericAttribute()
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
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task EmitsRegisterDiWhenDiIntegrationEnabled()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record InvoiceCommand(string InvoiceId);

            [RegisterCommandHandler<InvoiceCommand>]
            public sealed class AuditInvoiceHandler : ICommandHandler<InvoiceCommand>
            {
                public ValueTask HandleAsync(InvoiceCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            enableDiIntegration: true,
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp073DuplicateCommand()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record DuplicateCommand(int Value);

            [RegisterCommandHandler<DuplicateCommand>]
            public sealed class FirstHandler : ICommandHandler<DuplicateCommand>
            {
                public ValueTask HandleAsync(DuplicateCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            [RegisterCommandHandler<DuplicateCommand>]
            public sealed class SecondHandler : ICommandHandler<DuplicateCommand>
            {
                public ValueTask HandleAsync(DuplicateCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp074ContractMismatch()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record MismatchCommand(int Value);

            [RegisterCommandHandler<MismatchCommand>]
            public sealed class BrokenHandler
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task GeneratesRegistriesForMultipleCommands()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record PingCommand;
            public sealed record AddCommand(int Left, int Right);

            [RegisterCommandHandler<PingCommand>]
            public sealed class PingHandler : ICommandHandler<PingCommand>
            {
                public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            [RegisterCommandHandler<AddCommand>]
            public sealed class AddHandler : ICommandHandler<AddCommand, int>
            {
                public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default) =>
                    new(command.Left + command.Right);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesRegistryForGlobalNamespaceCommand()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            public sealed record GlobalCommand(int Value);

            [RegisterCommandHandler<GlobalCommand>]
            public sealed class GlobalHandler : ICommandHandler<GlobalCommand>
            {
                public ValueTask HandleAsync(GlobalCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp073WhenTwoHandlersClaimSameCommandEvenIfOneIsDiOnly()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record MixedCommand(int Value);

            [RegisterCommandHandler<MixedCommand>]
            public sealed class ParameterlessHandler : ICommandHandler<MixedCommand>
            {
                public ValueTask HandleAsync(MixedCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            [RegisterCommandHandler<MixedCommand>]
            public sealed class InjectedHandler : ICommandHandler<MixedCommand>
            {
                private readonly string _dep;
                public InjectedHandler(string dep) => _dep = dep;
                public ValueTask HandleAsync(MixedCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            enableDiIntegration: true,
            ("Handlers.cs", source));

        // Command Router is 1:1 — two handlers for one command is DP073 (unlike Event Aggregator 1:N).
        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task SeparatesStaticAndDiPathsAcrossDistinctCommands()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record StaticCommand(int Value);
            public sealed record InjectedCommand(int Value);

            [RegisterCommandHandler<StaticCommand>]
            public sealed class ParameterlessHandler : ICommandHandler<StaticCommand>
            {
                public ValueTask HandleAsync(StaticCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }

            [RegisterCommandHandler<InjectedCommand>]
            public sealed class InjectedHandler : ICommandHandler<InjectedCommand>
            {
                private readonly string _dep;
                public InjectedHandler(string dep) => _dep = dep;
                public ValueTask HandleAsync(InjectedCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            enableDiIntegration: true,
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task DoesNotGenerateRegistryWhenContractMismatch()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record AllBrokenCommand(int Value);

            [RegisterCommandHandler<AllBrokenCommand>]
            public sealed class BrokenHandler
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task DedupesSameHandlerDualAttributeWithoutDuplicateDiagnostic()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed record DualAttrCommand(int Value);

            [RegisterCommandHandler<DualAttrCommand>]
            [RegisterCommandHandler(typeof(DualAttrCommand))]
            public sealed class DualAttrHandler : ICommandHandler<DualAttrCommand>
            {
                public ValueTask HandleAsync(DualAttrCommand command, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterCommandHandlerGenerator>(
            ("Handlers.cs", source));

        Assert.Empty(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }
}
