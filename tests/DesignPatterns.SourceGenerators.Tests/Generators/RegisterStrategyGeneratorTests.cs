using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class RegisterStrategyGeneratorTests
{
    [Fact]
    public Task GeneratesKeysAndRegistry()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy<IPaymentStrategy>("alipay")]
            public sealed class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => $"Alipay: {amount:C}";
            }

            [RegisterStrategy<IPaymentStrategy>("wechat")]
            public sealed class WechatPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => $"Wechat: {amount:C}";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("Strategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesKeysAndRegistryForAsyncStrategyContract()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IProcessor : IAsyncStrategy<string, int>
            {
            }

            [RegisterStrategy<IProcessor>("fast")]
            public sealed class FastProcessor : IProcessor
            {
                public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
                    new ValueTask<int>(input.Length);
            }

            [RegisterStrategy<IProcessor>("slow")]
            public sealed class SlowProcessor : IProcessor
            {
                public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
                    new ValueTask<int>(input.Length * 2);
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("AsyncStrategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesKeysAndRegistryWithNonGenericAttribute()
    {
        const string source = """
            using System;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public sealed class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => $"Alipay: {amount:C}";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("Strategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesSourcesForSameNamedContractsInDifferentNamespaces()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace Sales
            {
                public interface IPaymentStrategy
                {
                    string Pay();
                }

                [RegisterStrategy<IPaymentStrategy>("card")]
                public sealed class CardPayment : IPaymentStrategy
                {
                    public string Pay() => "card";
                }
            }

            namespace Billing
            {
                public interface IPaymentStrategy
                {
                    string Pay();
                }

                [RegisterStrategy<IPaymentStrategy>("invoice")]
                public sealed class InvoicePayment : IPaymentStrategy
                {
                    public string Pay() => "invoice";
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("Strategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp003DuplicateKey()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy<IPaymentStrategy>("alipay")]
            public sealed class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            [RegisterStrategy<IPaymentStrategy>("alipay")]
            public sealed class DuplicatePayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "duplicate";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("Strategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp004ContractMismatch()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy<IPaymentStrategy>("broken")]
            public sealed class BrokenPayment
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("Strategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp007MissingParameterlessConstructor()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy<IPaymentStrategy>("custom")]
            public sealed class CustomPayment : IPaymentStrategy
            {
                public CustomPayment(string endpoint) => Endpoint = endpoint;

                public string Endpoint { get; }

                public string Pay(decimal amount) => Endpoint;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(
            ("Strategies.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
