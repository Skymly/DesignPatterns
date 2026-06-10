namespace DesignPatterns.Behavioral;

/// <summary>
/// A single handler entry in a <see cref="HandlerPipelineTrace"/>.
/// </summary>
/// <param name="Index">Zero-based position in registration order.</param>
/// <param name="Name">Display name for the handler (typically the handler type name).</param>
/// <param name="Status">Whether the handler completed, short-circuited, or was not reached.</param>
public readonly record struct HandlerPipelineStep(
    int Index,
    string Name,
    HandlerPipelineStepStatus Status);
