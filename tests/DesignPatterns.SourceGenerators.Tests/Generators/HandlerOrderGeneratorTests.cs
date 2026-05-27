using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class HandlerOrderGeneratorTests
{
    [Fact]
    public Task GeneratesOrderedPipeline()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
                public string? Response { get; set; }
            }

            [HandlerOrder<RequestContext>(20)]
            public sealed class AuthorizationHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);
            }

            [HandlerOrder<RequestContext>(10)]
            public sealed class LoggingHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesPipelineWithNonGenericAttribute()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
                public string? Response { get; set; }
            }

            [HandlerOrder(10, typeof(RequestContext))]
            public sealed class LoggingHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp005DuplicateOrder()
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
            public sealed class FirstHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }

            [HandlerOrder<RequestContext>(10)]
            public sealed class SecondHandler : IHandler<RequestContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp008ContractMismatch()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
            }

            [HandlerOrder<RequestContext>(10)]
            public sealed class BrokenHandler
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp009MissingParameterlessConstructor()
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
            public sealed class CustomHandler : IHandler<RequestContext>
            {
                public CustomHandler(string name) => Name = name;

                public string Name { get; }

                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
