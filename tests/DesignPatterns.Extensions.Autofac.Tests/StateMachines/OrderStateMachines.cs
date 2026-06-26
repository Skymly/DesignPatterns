using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac.Tests.StateMachines;

public enum OrderStatus
{
    Draft,
    Submitted,
    Paid,
}

public enum OrderTrigger
{
    Submit,
    Pay,
}

[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
public static partial class OrderStateMachine;
