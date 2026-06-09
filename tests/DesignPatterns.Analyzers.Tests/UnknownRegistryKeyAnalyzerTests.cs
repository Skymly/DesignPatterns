using System.Collections.Immutable;
using DesignPatterns.Analyzers;
using DesignPatterns.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Operations;

namespace DesignPatterns.Analyzers.Tests;

public sealed class UnknownRegistryKeyAnalyzerTests
{
    [Fact]
    public async Task ReportsDp025WhenStrategyRegistryKeyIsUnknown()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            [RegisterStrategy("wechat", typeof(IPaymentStrategy))]
            public class WechatPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "wechat";
            }

            public static class Usage
            {
                public static void Run(IStrategyRegistry<string, IPaymentStrategy> registry)
                {
                    registry.Get("alipy");
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnknownRegistryKeyAnalyzer());

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "DP025" &&
                diagnostic.GetMessage().Contains("alipy", StringComparison.Ordinal) &&
                diagnostic.GetMessage().Contains("alipay", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReportsDp025WhenFactoryRegistryKeyIsUnknown()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Produce();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public class StandardFactory : IProductFactory
            {
                public string Produce() => "standard";
            }

            [RegisterFactory("premium", typeof(IProductFactory))]
            public class PremiumFactory : IProductFactory
            {
                public string Produce() => "premium";
            }

            public static class Usage
            {
                public static void Run(IFactoryRegistry<string, IProductFactory> registry)
                {
                    registry.Create("standrd");
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnknownRegistryKeyAnalyzer());

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "DP025" &&
                diagnostic.GetMessage().Contains("standrd", StringComparison.Ordinal) &&
                diagnostic.GetMessage().Contains("standard", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoesNotReportWhenKeyIsRegistered()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            public static class Usage
            {
                public static void Run(IStrategyRegistry<string, IPaymentStrategy> registry)
                {
                    registry.Get("alipay");
                    registry.TryGet("alipay", out _);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnknownRegistryKeyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP025");
    }

    [Fact]
    public async Task DoesNotReportWhenContractIsNotGeneratorManaged()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IManualStrategy
            {
                string Execute();
            }

            public sealed class ManualStrategy : IManualStrategy
            {
                public string Execute() => "manual";
            }

            public static class Usage
            {
                public static void Run()
                {
                    var registry = new StrategyRegistryBuilder<string, IManualStrategy>()
                        .Register("manual", new ManualStrategy())
                        .Build();

                    registry.Get("missing");
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnknownRegistryKeyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP025");
    }

    [Fact]
    public async Task DoesNotReportWhenKeyIsNotACompileTimeConstant()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            public static class Usage
            {
                public static void Run(IStrategyRegistry<string, IPaymentStrategy> registry, string key)
                {
                    registry.Get(key);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnknownRegistryKeyAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "DP025");
    }

    [Fact]
    public async Task ReportsDp025WhenRegisteredKeysExistInReferencedAssembly()
    {
        const string registrationSource = """
            using DesignPatterns.Behavioral;

            namespace Registrations;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }
            """;

        const string usageSource = """
            using DesignPatterns.Behavioral;
            using Registrations;

            namespace Implementations;

            public static class Usage
            {
                public static void Run(IStrategyRegistry<string, IPaymentStrategy> registry)
                {
                    registry.Get("alipy");
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithReferencedAssemblyAsync(
            registrationSource,
            usageSource,
            new UnknownRegistryKeyAnalyzer());

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "DP025" &&
                diagnostic.GetMessage().Contains("alipy", StringComparison.Ordinal));
    }
}

public sealed class CorrectRegistryKeyCodeFixTests
{
    [Fact]
    public async Task FixesDp025ByReplacingLiteralWithClosestRegisteredKey()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                string Pay(decimal amount);
            }

            [RegisterStrategy("alipay", typeof(IPaymentStrategy))]
            public class AlipayPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "alipay";
            }

            [RegisterStrategy("wechat", typeof(IPaymentStrategy))]
            public class WechatPayment : IPaymentStrategy
            {
                public string Pay(decimal amount) => "wechat";
            }

            public static class Usage
            {
                public static void Run(IStrategyRegistry<string, IPaymentStrategy> registry)
                {
                    registry.Get("alipy");
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnknownRegistryKeyAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "DP025");
        Assert.True(
            diagnostic.Properties.TryGetValue("SuggestedRegistryKey", out var suggested),
            diagnostic.GetMessage());
        Assert.Equal("alipay", suggested);
        var document = AnalyzerTestContext.CreateDocument(source);
        var fixes = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => fixes.Add(action),
            CancellationToken.None);
        await new CorrectRegistryKeyCodeFixProvider().RegisterCodeFixesAsync(context);

        Assert.NotEmpty(fixes);
        var operation = await fixes[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = Assert.IsType<ApplyChangesOperation>(operation.Single());
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        Assert.Contains("\"alipay\"", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"alipy\"", fixedSource, StringComparison.Ordinal);
    }
}
