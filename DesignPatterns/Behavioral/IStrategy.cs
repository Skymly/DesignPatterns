namespace DesignPatterns.Behavioral;

/// <summary>
/// Optional strategy contract for synchronous algorithms.
/// </summary>
/// <typeparam name="TInput">Input type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public interface IStrategy<in TInput, out TOutput>
{
    /// <summary>
    /// Executes the strategy.
    /// </summary>
    TOutput Execute(TInput input);
}
