using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

/// <summary>
/// Seam: public <see cref="ICommandRouter"/> / <see cref="CommandRouterBuilder"/> dispatch behavior (issue #258).
/// </summary>
public sealed class CommandRouterTests
{
    [Fact]
    public async Task SendAsync_VoidCommand_InvokesRegisteredHandler()
    {
        var handler = new TrackingVoidHandler();
        var router = new CommandRouterBuilder()
            .Register(handler)
            .Build();

        await router.SendAsync(new PingCommand("hello"));

        Assert.Single(handler.Received);
        Assert.Equal("hello", handler.Received[0].Message);
    }

    [Fact]
    public async Task SendAsync_CommandWithResult_ReturnsHandlerResult()
    {
        var router = new CommandRouterBuilder()
            .Register(new AddHandler())
            .Build();

        var result = await router.SendAsync<AddCommand, int>(new AddCommand(2, 3));

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task SendAsync_MissingHandler_ThrowsCommandHandlerNotFound()
    {
        var router = new CommandRouterBuilder().Build();

        var ex = await Assert.ThrowsAsync<CommandHandlerNotFoundException>(
            () => router.SendAsync(new PingCommand("x")).AsTask());

        Assert.Contains(nameof(PingCommand), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_MissingHandlerWithResult_ThrowsAndDoesNotReturnDefaultSilently()
    {
        var router = new CommandRouterBuilder().Build();

        await Assert.ThrowsAsync<CommandHandlerNotFoundException>(
            () => router.SendAsync<AddCommand, int>(new AddCommand(1, 1)).AsTask());
    }

    [Fact]
    public async Task TrySendAsync_VoidCommand_ReturnsTrueWhenHandlerExists()
    {
        var handler = new TrackingVoidHandler();
        var router = new CommandRouterBuilder()
            .Register(handler)
            .Build();

        var sent = await router.TrySendAsync(new PingCommand("ok"));

        Assert.True(sent);
        Assert.Single(handler.Received);
    }

    [Fact]
    public async Task TrySendAsync_VoidCommand_ReturnsFalseWhenMissing()
    {
        var router = new CommandRouterBuilder().Build();

        var sent = await router.TrySendAsync(new PingCommand("x"));

        Assert.False(sent);
    }

    [Fact]
    public async Task TrySendAsync_WithResult_ReturnsSuccessAndValue()
    {
        var router = new CommandRouterBuilder()
            .Register(new AddHandler())
            .Build();

        var attempt = await router.TrySendAsync<AddCommand, int>(new AddCommand(4, 6));

        Assert.True(attempt.Success);
        Assert.Equal(10, attempt.Result);
    }

    [Fact]
    public async Task TrySendAsync_WithResult_ReturnsFailureWhenMissing()
    {
        var router = new CommandRouterBuilder().Build();

        var attempt = await router.TrySendAsync<AddCommand, int>(new AddCommand(1, 1));

        Assert.False(attempt.Success);
        Assert.Equal(0, attempt.Result);
    }

    [Fact]
    public async Task SendAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var router = new CommandRouterBuilder()
            .Register(new CancellationObservingHandler())
            .Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.SendAsync(new PingCommand("x"), cts.Token).AsTask());
    }

    [Fact]
    public async Task SendAsync_StructCommandAndResult_Works()
    {
        var router = new CommandRouterBuilder()
            .Register(new StructAddHandler())
            .Build();

        var result = await router.SendAsync<StructAddCommand, int>(new StructAddCommand(7, 8));

        Assert.Equal(15, result);
    }

    [Fact]
    public async Task SendAsync_RegisteredAsVoid_WrongResultOverload_ThrowsContractMismatch()
    {
        var router = new CommandRouterBuilder()
            .Register(new TrackingVoidHandler())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.SendAsync<PingCommand, string>(new PingCommand("x")).AsTask());

        Assert.Contains(nameof(PingCommand), ex.Message, StringComparison.Ordinal);
        Assert.Contains("ICommandHandler", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrySendAsync_RegisteredAsVoid_WrongResultOverload_ThrowsContractMismatch()
    {
        var router = new CommandRouterBuilder()
            .Register(new TrackingVoidHandler())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.TrySendAsync<PingCommand, string>(new PingCommand("x")).AsTask());

        Assert.Contains(nameof(PingCommand), ex.Message, StringComparison.Ordinal);
        Assert.Contains("ICommandHandler", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_DuplicateCommandType_Throws()
    {
        var builder = new CommandRouterBuilder()
            .Register(new TrackingVoidHandler());

        var ex = Assert.Throws<ArgumentException>(
            () => builder.Register(new TrackingVoidHandler()));

        Assert.Contains(nameof(PingCommand), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_DuplicateAcrossVoidAndResult_Throws()
    {
        var builder = new CommandRouterBuilder()
            .Register(new TrackingVoidHandler());

        Assert.Throws<ArgumentException>(
            () => builder.Register(new PingWithResultHandler()));
    }

    [Fact]
    public void Register_NullHandler_Throws()
    {
        var builder = new CommandRouterBuilder();

        Assert.Throws<ArgumentNullException>(
            () => builder.Register<PingCommand>(null!));
        Assert.Throws<ArgumentNullException>(
            () => builder.Register<AddCommand, int>((ICommandHandler<AddCommand, int>)null!));
    }

    [Fact]
    public async Task ConcurrentSend_AfterBuild_DoesNotThrow()
    {
        const int iterations = 100;
        var router = new CommandRouterBuilder()
            .Register(new TrackingVoidHandler())
            .Register(new AddHandler())
            .Build();

        var start = new Barrier(participantCount: 3);

        var voidSend = Task.Run(async () =>
        {
            start.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                await router.SendAsync(new PingCommand("x"));
            }
        });

        var resultSend = Task.Run(async () =>
        {
            start.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                _ = await router.SendAsync<AddCommand, int>(new AddCommand(i, 1));
            }
        });

        start.SignalAndWait();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(voidSend, resultSend));
        Assert.Null(exception);
    }

    [Fact]
    public void RegisterCommandHandlerAttribute_StoresCommandType()
    {
        var attribute = new RegisterCommandHandlerAttribute(typeof(PingCommand));

        Assert.Equal(typeof(PingCommand), attribute.For);
    }

    [Fact]
    public void RegisterCommandHandlerAttribute_NullFor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterCommandHandlerAttribute(null!));
    }

    [Fact]
    public void CommandPipelineBehaviorAttribute_StoresOrderAndCommandType()
    {
        var attribute = new CommandPipelineBehaviorAttribute(10, typeof(PingCommand));

        Assert.Equal(10, attribute.Order);
        Assert.Equal(typeof(PingCommand), attribute.For);
    }

    [Fact]
    public void CommandPipelineBehaviorAttribute_NullFor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CommandPipelineBehaviorAttribute(0, null!));
    }

    private sealed record PingCommand(string Message) : ICommand;

    private sealed record AddCommand(int Left, int Right) : ICommand<int>;

    private readonly struct StructAddCommand : ICommand<int>
    {
        public StructAddCommand(int left, int right)
        {
            Left = left;
            Right = right;
        }

        public int Left { get; }
        public int Right { get; }
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

    private sealed class AddHandler : ICommandHandler<AddCommand, int>
    {
        public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default) =>
            new(command.Left + command.Right);
    }

    private sealed class StructAddHandler : ICommandHandler<StructAddCommand, int>
    {
        public ValueTask<int> HandleAsync(StructAddCommand command, CancellationToken cancellationToken = default) =>
            new(command.Left + command.Right);
    }

    private sealed class PingWithResultHandler : ICommandHandler<PingCommand, string>
    {
        public ValueTask<string> HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
            new(command.Message);
    }

    private sealed class CancellationObservingHandler : ICommandHandler<PingCommand>
    {
        public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }
    }
}
