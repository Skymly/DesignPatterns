using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a step type as a member of a work graph holder with an explicit string id
/// and optional readiness dependencies.
/// </summary>
/// <remarks>
/// Example: <c>[WorkStep(typeof(Holder), Id = "auth", DependsOn = new[] { "config" })]</c>.
/// Membership is explicit via <see cref="Graph"/> so multiple graphs may share the same
/// context type without silent aggregation.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class WorkStepAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkStepAttribute"/> class.
    /// </summary>
    /// <param name="graph">The holder type annotated with <see cref="WorkGraphAttribute"/>.</param>
    public WorkStepAttribute(Type graph)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    /// <summary>
    /// The holder type that names the graph this step belongs to.
    /// </summary>
    public Type Graph { get; }

    /// <summary>
    /// Stable string id for this step within the graph.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Step ids that must complete before this step becomes ready.
    /// </summary>
    public string[] DependsOn { get; set; } = Array.Empty<string>();
}
