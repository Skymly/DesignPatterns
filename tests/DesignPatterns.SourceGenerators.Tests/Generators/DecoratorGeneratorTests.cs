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
    public void GeneratesUniqueDecoratorOrderConstantsForCollidingTypeNames()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly
            {
                public interface IPaymentService
                {
                    int Pay(int amount);
                }
            }

            namespace First
            {
                [Decorator<TestAssembly.IPaymentService>(10)]
                public sealed class LoggingDecorator : TestAssembly.IPaymentService, IDecorator<TestAssembly.IPaymentService>
                {
                    public TestAssembly.IPaymentService Decorate(TestAssembly.IPaymentService inner) => inner;

                    public int Pay(int amount) => amount;
                }
            }

            namespace Second
            {
                [Decorator<TestAssembly.IPaymentService>(20)]
                public sealed class LoggingDecorator : TestAssembly.IPaymentService, IDecorator<TestAssembly.IPaymentService>
                {
                    public TestAssembly.IPaymentService Decorate(TestAssembly.IPaymentService inner) => inner;

                    public int Pay(int amount) => amount;
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<DecoratorGenerator>(
            ("Decorators.cs", source));

        var generated = SourceGeneratorTestContext.GetGeneratedSources(runResult);
        var orderSource = generated["IPaymentService.PaymentServiceDecoratorOrder.g.cs"];

        Assert.Contains("public const int LoggingDecorator = 10;", orderSource, StringComparison.Ordinal);
        Assert.Contains("public const int LoggingDecorator_1 = 20;", orderSource, StringComparison.Ordinal);
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
}
