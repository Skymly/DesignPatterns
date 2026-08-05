#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a holder type that names a work graph and fixes its shared context type
/// to <typeparamref name="TContext"/>. Prefer a <c>static class</c> holder.
/// </summary>
/// <typeparam name="TContext">The shared context type for steps in this graph.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WorkGraphAttribute<TContext> : Attribute
{
}

#endif
