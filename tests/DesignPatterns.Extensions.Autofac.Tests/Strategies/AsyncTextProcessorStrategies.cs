using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac.Tests.Strategies;

public interface ITextProcessor : IAsyncStrategy<string, int>
{
}

[RegisterStrategy<ITextProcessor>("length")]
public sealed class LengthTextProcessor : ITextProcessor
{
    public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
        new ValueTask<int>(input.Length);
}

[RegisterStrategy<ITextProcessor>("double-length")]
public sealed class DoubleLengthTextProcessor : ITextProcessor
{
    public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
        new ValueTask<int>(input.Length * 2);
}
