using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class DecoratorGeneratorTests
{
    [Fact]
    public Task GeneratesDecoratorStack()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(20)]
            public sealed class MetricsPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => new Impl(inner);

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }

            [Decorator<IPaymentService>(10)]
            public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => new Impl(inner);

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesDecoratorOrderConstants()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(20)]
            public sealed class MetricsPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner;

                public int Pay(int amount) => amount;
            }

            [Decorator<IPaymentService>(10)]
            public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner;

                public int Pay(int amount) => amount;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesStackWithNonGenericAttribute()
    {
        const string source = """
            using System;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator(10, typeof(IPaymentService))]
            public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner;

                public int Pay(int amount) => amount;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp016DuplicateOrder()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class FirstDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner;

                public int Pay(int amount) => amount;
            }

            [Decorator<IPaymentService>(10)]
            public sealed class SecondDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner;

                public int Pay(int amount) => amount;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp017ContractMismatch()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class BrokenDecorator : IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => inner!;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp018MissingDecoratorInterface()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class BrokenDecorator : IPaymentService
            {
                public int Pay(int amount) => 0;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp019MissingParameterlessConstructor()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class BrokenDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public BrokenDecorator(int ignored) { }

                public IPaymentService Decorate(IPaymentService inner) => inner;

                public int Pay(int amount) => amount;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task GeneratesAsyncDecoratorStack()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class AsyncLoggingDecorator : IPaymentService, IAsyncDecorator<IPaymentService>
            {
                public ValueTask<IPaymentService> DecorateAsync(IPaymentService inner, CancellationToken cancellationToken = default)
                    => new ValueTask<IPaymentService>(new Impl(inner));

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesMixedSyncAndAsyncDecoratorStack()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => new Impl(inner);

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }

            [Decorator<IPaymentService>(20)]
            public sealed class MetricsPaymentDecorator : IPaymentService, IAsyncDecorator<IPaymentService>
            {
                public ValueTask<IPaymentService> DecorateAsync(IPaymentService inner, CancellationToken cancellationToken = default)
                    => new ValueTask<IPaymentService>(new Impl(inner));

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task EmitsRegisterDiWhenDiIntegrationEnabled()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
            {
                public IPaymentService Decorate(IPaymentService inner) => new Impl(inner);

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            enableDiIntegration: true,
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task EmitsRegisterDiWithAsyncDecoratorWhenDiIntegrationEnabled()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IPaymentService
            {
                int Pay(int amount);
            }

            [Decorator<IPaymentService>(10)]
            public sealed class AsyncLoggingDecorator : IPaymentService, IAsyncDecorator<IPaymentService>
            {
                public ValueTask<IPaymentService> DecorateAsync(IPaymentService inner, CancellationToken cancellationToken = default)
                    => new ValueTask<IPaymentService>(new Impl(inner));

                public int Pay(int amount) => throw new System.NotSupportedException();

                private sealed class Impl(IPaymentService inner) : IPaymentService
                {
                    public int Pay(int amount) => inner.Pay(amount);
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            enableDiIntegration: true,
            ("Decorators.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }
}
