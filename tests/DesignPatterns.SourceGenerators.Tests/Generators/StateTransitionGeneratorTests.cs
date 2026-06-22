using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class StateTransitionGeneratorTests
{
    [Fact]
    public Task GeneratesTransitionTableAndHolderPartial()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus
            {
                Draft,
                Submitted,
                Paid,
                Cancelled,
            }

            public enum OrderTrigger
            {
                Submit,
                Pay,
                Cancel,
            }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            [Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
            [Transition(OrderStatus.Draft, OrderTrigger.Cancel, OrderStatus.Cancelled)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesSourcesInHolderNamespaceWhenStateEnumDiffers()
    {
        const string enumSource = """
            namespace TestAssembly.Domain;

            public enum OrderStatus
            {
                Draft,
                Submitted,
            }

            public enum OrderTrigger
            {
                Submit,
            }
            """;

        const string holderSource = """
            using DesignPatterns.Behavioral;
            using TestAssembly.Domain;

            namespace TestAssembly.Machines;

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("States.cs", enumSource),
            ("OrderMachine.cs", holderSource));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp026DuplicateEdge()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Draft)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp027InvalidStateMember()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition((OrderStatus)99, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp028InvalidTriggerMember()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, (OrderTrigger)5, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp029InvalidInitialState()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = (OrderStatus)42)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp030InvalidHolder()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public sealed class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp031IsolatedState()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted, Paid }
            public enum OrderTrigger { Submit, Pay }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            [Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task EmitsGuardWhenTransitionHasGuardProperty()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, Guard = nameof(CanSubmit))]
            public static partial class OrderStatusMachine
            {
                public static bool CanSubmit(OrderStatus from, OrderTrigger trigger) => true;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp032GuardMethodNotFound()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, Guard = "Nonexistent")]
            public static partial class OrderStatusMachine;
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp035GuardMethodWrongSignature()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, Guard = nameof(CanSubmit))]
            public static partial class OrderStatusMachine
            {
                public static bool CanSubmit(OrderStatus from) => true;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<StateTransitionGenerator>(
            ("OrderMachine.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
