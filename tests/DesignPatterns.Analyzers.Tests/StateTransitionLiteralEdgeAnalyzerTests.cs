using DesignPatterns.Analyzers;
using VerifyTests;

namespace DesignPatterns.Analyzers.Tests;

public sealed class StateTransitionLiteralEdgeAnalyzerTests
{
    [Fact]
    public async Task ReportsDp036WhenLiteralEdgeIsNotDeclared()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted, Cancelled }
            public enum OrderTrigger { Submit, Cancel }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            [Transition(OrderStatus.Submitted, OrderTrigger.Cancel, OrderStatus.Cancelled)]
            public static partial class OrderStatusMachine;

            public static class Usage
            {
                public static void Run(ITransitionTable<OrderStatus, OrderTrigger> table)
                {
                    // (Cancelled, Submit) is not a declared edge
                    table.TryTransition(OrderStatus.Cancelled, OrderTrigger.Submit, out _);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new StateTransitionLiteralEdgeAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP036"));
    }

    [Fact]
    public async Task DoesNotReportWhenLiteralEdgeIsDeclared()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted, Cancelled }
            public enum OrderTrigger { Submit, Cancel }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            [Transition(OrderStatus.Submitted, OrderTrigger.Cancel, OrderStatus.Cancelled)]
            public static partial class OrderStatusMachine;

            public static class Usage
            {
                public static void Run(ITransitionTable<OrderStatus, OrderTrigger> table)
                {
                    table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out _);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new StateTransitionLiteralEdgeAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP036"));
    }

    [Fact]
    public async Task DoesNotReportWhenArgumentsAreNotConstants()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted, Cancelled }
            public enum OrderTrigger { Submit, Cancel }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;

            public static class Usage
            {
                public static void Run(ITransitionTable<OrderStatus, OrderTrigger> table, OrderStatus state, OrderTrigger trigger)
                {
                    table.TryTransition(state, trigger, out _);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new StateTransitionLiteralEdgeAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP036"));
    }

    [Fact]
    public async Task DoesNotReportForNonTransitionTableInvocations()
    {
        const string source = """
            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            public static class FakeTable
            {
                public static bool TryTransition(OrderStatus current, OrderTrigger trigger, out OrderStatus next)
                {
                    next = current;
                    return false;
                }
            }

            public static class Usage
            {
                public static void Run()
                {
                    FakeTable.TryTransition(OrderStatus.Submitted, OrderTrigger.Submit, out _);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new StateTransitionLiteralEdgeAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP036"));
    }

    [Fact]
    public async Task ReportsDp036ForMultipleInvalidEdges()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted, Cancelled }
            public enum OrderTrigger { Submit, Cancel }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;

            public static class Usage
            {
                public static void Run(ITransitionTable<OrderStatus, OrderTrigger> table)
                {
                    table.TryTransition(OrderStatus.Cancelled, OrderTrigger.Submit, out _);
                    table.TryTransition(OrderStatus.Submitted, OrderTrigger.Cancel, out _);
                }
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new StateTransitionLiteralEdgeAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP036"));
    }
}
