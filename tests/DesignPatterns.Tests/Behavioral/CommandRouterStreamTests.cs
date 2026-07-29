using System.Runtime.CompilerServices;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

/// <summary>
/// Seam: public <see cref="ICommandRouter"/> / <see cref="CommandRouterBuilder"/> stream dispatch (issue #282).
/// </summary>
public sealed class CommandRouterStreamTests
{
    [Fact]
    public async Task SendStreamAsync_YieldsProgressiveResults()
    {
        var router = new CommandRouterBuilder()
            .Register(new RangeStreamHandler())
            .Build();

        var items = new List<int>();
        await foreach (var item in router.SendStreamAsync<RangeCommand, int>(new RangeCommand(1, 3)))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3 }, items);
    }

    [Fact]
    public async Task SendStreamAsync_MissingHandler_ThrowsCommandHandlerNotFound()
    {
        var router = new CommandRouterBuilder().Build();

        var ex = await Assert.ThrowsAsync<CommandHandlerNotFoundException>(async () =>
        {
            await foreach (var _ in router.SendStreamAsync<RangeCommand, int>(new RangeCommand(1, 1)))
            {
            }
        });

        Assert.Contains(nameof(RangeCommand), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrySendStreamAsync_ReturnsSuccessAndStreamWhenHandlerExists()
    {
        var router = new CommandRouterBuilder()
            .Register(new RangeStreamHandler())
            .Build();

        var attempt = router.TrySendStreamAsync<RangeCommand, int>(new RangeCommand(10, 11));

        Assert.True(attempt.Success);
        Assert.NotNull(attempt.Result);

        var items = new List<int>();
        await foreach (var item in attempt.Result!)
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 10, 11 }, items);
    }

    [Fact]
    public void TrySendStreamAsync_ReturnsFailureWhenMissing()
    {
        var router = new CommandRouterBuilder().Build();

        var attempt = router.TrySendStreamAsync<RangeCommand, int>(new RangeCommand(1, 1));

        Assert.False(attempt.Success);
        Assert.Null(attempt.Result);
    }

    [Fact]
    public async Task SendStreamAsync_RegisteredAsNonStream_ThrowsContractMismatch()
    {
        var router = new CommandRouterBuilder()
            .Register(new NonStreamRangeHandler())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in router.SendStreamAsync<RangeCommand, int>(new RangeCommand(1, 1)))
            {
            }
        });

        Assert.Contains(nameof(RangeCommand), ex.Message, StringComparison.Ordinal);
        Assert.Contains("IStreamCommandHandler", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_RegisteredAsStream_ThrowsContractMismatch()
    {
        var router = new CommandRouterBuilder()
            .Register(new RangeStreamHandler())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.SendAsync<RangeCommand, int>(new RangeCommand(1, 1)).AsTask());

        Assert.Contains(nameof(RangeCommand), ex.Message, StringComparison.Ordinal);
        Assert.Contains("ICommandHandler", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_StreamThenNonStream_ThrowsDuplicate()
    {
        var builder = new CommandRouterBuilder()
            .Register(new RangeStreamHandler());

        var ex = Assert.Throws<ArgumentException>(
            () => builder.Register(new NonStreamRangeHandler()));

        Assert.Contains(nameof(RangeCommand), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_NonStreamThenStream_ThrowsDuplicate()
    {
        var builder = new CommandRouterBuilder()
            .Register(new NonStreamRangeHandler());

        Assert.Throws<ArgumentException>(
            () => builder.Register(new RangeStreamHandler()));
    }

    [Fact]
    public void Register_VoidThenStream_ThrowsDuplicate()
    {
        var builder = new CommandRouterBuilder()
            .Register(new TrackingVoidHandler());

        var ex = Assert.Throws<ArgumentException>(
            () => builder.Register(new PingStreamHandler()));

        Assert.Contains(nameof(PingCommand), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_NullStreamHandler_Throws()
    {
        var builder = new CommandRouterBuilder();

        Assert.Throws<ArgumentNullException>(
            () => builder.Register<RangeCommand, int>((IStreamCommandHandler<RangeCommand, int>)null!));
    }

    [Fact]
    public void TrySendStreamAsync_RegisteredAsNonStream_ThrowsContractMismatch()
    {
        var router = new CommandRouterBuilder()
            .Register(new NonStreamRangeHandler())
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => router.TrySendStreamAsync<RangeCommand, int>(new RangeCommand(1, 1)));

        Assert.Contains(nameof(RangeCommand), ex.Message, StringComparison.Ordinal);
        Assert.Contains("IStreamCommandHandler", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendStreamAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var router = new CommandRouterBuilder()
            .Register(new CancellationObservingStreamHandler())
            .Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in router.SendStreamAsync<RangeCommand, int>(new RangeCommand(1, 1), cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task SendAsync_VoidCommand_StillWorksAlongsideStreamCommands()
    {
        var voidHandler = new TrackingVoidHandler();
        var router = new CommandRouterBuilder()
            .Register(voidHandler)
            .Register(new RangeStreamHandler())
            .Build();

        await router.SendAsync(new PingCommand("ok"));

        var items = new List<int>();
        await foreach (var item in router.SendStreamAsync<RangeCommand, int>(new RangeCommand(1, 2)))
        {
            items.Add(item);
        }

        Assert.Single(voidHandler.Received);
        Assert.Equal(new[] { 1, 2 }, items);
    }

    private sealed record RangeCommand(int From, int To) : ICommand;

    private sealed record PingCommand(string Message) : ICommand;

    private sealed class RangeStreamHandler : IStreamCommandHandler<RangeCommand, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            RangeCommand command,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var value = command.From; value <= command.To; value++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return value;
                await Task.Yield();
            }
        }
    }

    private sealed class PingStreamHandler : IStreamCommandHandler<PingCommand, string>
    {
        public async IAsyncEnumerable<string> HandleAsync(
            PingCommand command,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return command.Message;
            await Task.Yield();
        }
    }

    private sealed class NonStreamRangeHandler : ICommandHandler<RangeCommand, int>
    {
        public ValueTask<int> HandleAsync(RangeCommand command, CancellationToken cancellationToken = default) =>
            new(command.From);
    }

    private sealed class CancellationObservingStreamHandler : IStreamCommandHandler<RangeCommand, int>
    {
        public IAsyncEnumerable<int> HandleAsync(
            RangeCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Empty(cancellationToken);
        }

        private static async IAsyncEnumerable<int> Empty(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }
    }

    private sealed class TrackingVoidHandler : ICommandHandler<PingCommand>
    {
        public List<PingCommand> Received { get; } = new();

        public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
        {
            Received.Add(command);
            return default;
        }
    }
}
