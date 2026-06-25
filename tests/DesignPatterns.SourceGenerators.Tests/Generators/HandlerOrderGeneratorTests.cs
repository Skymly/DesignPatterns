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
    public Task GeneratesPipelinesWhenSingleClassHasMultipleContexts()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class RequestContext
            {
            }

            public sealed class AuditContext
            {
            }

            [HandlerOrder<RequestContext>(10)]
            [HandlerOrder<AuditContext>(10)]
            public sealed class SharedLoggingHandler :
                IHandler<RequestContext>,
                IHandler<AuditContext>
            {
                public ValueTask InvokeAsync(
                    RequestContext context,
                    HandlerDelegate<RequestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);

                public ValueTask InvokeAsync(
                    AuditContext context,
                    HandlerDelegate<AuditContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesPipelinesForSameNamedContextsInDifferentNamespaces()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace PublicApi
            {
                public sealed class RequestContext
                {
                }

                [HandlerOrder<RequestContext>(10)]
                public sealed class PublicHandler : IHandler<RequestContext>
                {
                    public ValueTask InvokeAsync(
                        RequestContext context,
                        HandlerDelegate<RequestContext> next,
                        CancellationToken cancellationToken = default) =>
                        next(context, cancellationToken);
                }
            }

            namespace AdminApi
            {
                public sealed class RequestContext
                {
                }

                [HandlerOrder<RequestContext>(10)]
                public sealed class AdminHandler : IHandler<RequestContext>
                {
                    public ValueTask InvokeAsync(
                        RequestContext context,
                        HandlerDelegate<RequestContext> next,
                        CancellationToken cancellationToken = default) =>
                        next(context, cancellationToken);
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp005DuplicateOrderOnSameClass()
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
            [HandlerOrder<RequestContext>(10)]
            public sealed class DuplicateOrderHandler : IHandler<RequestContext>
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
    public Task GeneratesPipelineWithGuard()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class TestContext
            {
                public bool Flag { get; set; }
            }

            [HandlerOrder(1, typeof(TestContext), Guard = "CanHandle")]
            public sealed class FirstHandler : IHandler<TestContext>
            {
                public ValueTask InvokeAsync(
                    TestContext context,
                    HandlerDelegate<TestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);

                public static bool CanHandle(TestContext context) => context.Flag;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp050GuardMethodNotFound()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class TestContext
            {
            }

            [HandlerOrder(1, typeof(TestContext), Guard = "Missing")]
            public sealed class FirstHandler : IHandler<TestContext>
            {
                public ValueTask InvokeAsync(
                    TestContext context,
                    HandlerDelegate<TestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp051GuardMethodNotStatic()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class TestContext
            {
            }

            [HandlerOrder(1, typeof(TestContext), Guard = "CanHandle")]
            public sealed class FirstHandler : IHandler<TestContext>
            {
                public ValueTask InvokeAsync(
                    TestContext context,
                    HandlerDelegate<TestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);

                public bool CanHandle(TestContext context) => true;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<HandlerOrderGenerator>(
            ("Handlers.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp052GuardMethodWrongSignature()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class TestContext
            {
            }

            [HandlerOrder(1, typeof(TestContext), Guard = "CanHandle")]
            public sealed class FirstHandler : IHandler<TestContext>
            {
                public ValueTask InvokeAsync(
                    TestContext context,
                    HandlerDelegate<TestContext> next,
                    CancellationToken cancellationToken = default) =>
                    next(context, cancellationToken);

                public static bool CanHandle(string context) => true;
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
