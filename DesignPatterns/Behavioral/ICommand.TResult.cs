namespace DesignPatterns.Behavioral;

/// <summary>
/// Marker for a command that produces a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The result type produced when the command is handled.</typeparam>
public interface ICommand<out TResult>
{
}
