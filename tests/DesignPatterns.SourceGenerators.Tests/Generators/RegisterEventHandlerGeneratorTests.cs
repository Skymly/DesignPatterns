using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class RegisterEventHandlerGeneratorTests
{
    [Fact]
    public Task GeneratesHandlerRegistryWithGenericAttribute()
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

            [RegisterEventHandler<OrderPlacedEvent>]
            public sealed class NotifyOrderHandler : IEventHandler<OrderPlacedEvent>
            {
                public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterEventHandlerGenerator>(
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

            public record UserCreatedEvent(string UserId);

            [RegisterEventHandler(typeof(UserCreatedEvent))]
            public sealed class WelcomeEmailHandler : IEventHandler<UserCreatedEvent>
            {
                public ValueTask HandleAsync(UserCreatedEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterEventHandlerGenerator>(
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

            public record InvoicePaidEvent(string InvoiceId);

            [RegisterEventHandler<InvoicePaidEvent>]
            public sealed class AuditInvoiceHandler : IEventHandler<InvoicePaidEvent>
            {
                public ValueTask HandleAsync(InvoicePaidEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterEventHandlerGenerator>(
            enableDiIntegration: true,
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp045DuplicateOnSameClass()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record DuplicateEvent(int Value);

            [RegisterEventHandler<DuplicateEvent>]
            [RegisterEventHandler(typeof(DuplicateEvent))]
            public sealed class DuplicateHandler : IEventHandler<DuplicateEvent>
            {
                public ValueTask HandleAsync(DuplicateEvent evt, CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterEventHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp046ContractMismatch()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public record MismatchEvent(int Value);

            [RegisterEventHandler<MismatchEvent>]
            public sealed class BrokenHandler
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterEventHandlerGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
