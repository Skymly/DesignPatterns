using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Declares one directed transition edge on a <see cref="StateMachineAttribute"/> holder class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TransitionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionAttribute"/> class.
    /// </summary>
    /// <param name="from">Source state enum member.</param>
    /// <param name="trigger">Trigger enum member.</param>
    /// <param name="to">Target state enum member.</param>
    public TransitionAttribute(object from, object trigger, object to)
    {
        From = from;
        Trigger = trigger;
        To = to;
    }

    /// <summary>
    /// Source state enum member.
    /// </summary>
    public object From { get; }

    /// <summary>
    /// Trigger enum member.
    /// </summary>
    public object Trigger { get; }

    /// <summary>
    /// Target state enum member.
    /// </summary>
    public object To { get; }

    /// <summary>
    /// Optional name of a static guard method on the holder class.
    /// When set, the method must have the signature
    /// <c>static bool Method(TState from, TTrigger trigger)</c>.
    /// The transition only fires when the guard returns <see langword="true"/>.
    /// </summary>
    public string? Guard { get; set; }
}
