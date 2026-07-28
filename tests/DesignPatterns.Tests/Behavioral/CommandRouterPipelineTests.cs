using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

/// <summary>
/// Seam: Command Router pipeline behaviors via <see cref="CommandRouterBuilder"/> /
/// <see cref="ICommandRouter"/> (issue #275).
/// </summary>
public sealed class CommandRouterPipelineTests
{
    [Fact]
    public async Task SendAsync_VoidBehavior_CallsNext_InvokesHandler()
    {
        var log = new List<string>();
        var router = new CommandRouterBuilder()
            .Register(new LoggingVoidHandler(log))
            .UseBehavior(new LoggingVoidBehavior(log, "behavior"), order: 0)
            .Build();

        await router.SendAsync(new PingCommand("x"));

        Assert.Equal(new[] { "behavior:in", "handler", "behavior:out" }, log);
    }

    [Fact]
    public async Task SendAsync_VoidBehavior_OmitsNext_ShortCircuitsHandler()
    {
        var handlerRan = false;
        var router = new CommandRouterBuilder()
            .Register(new CallbackVoidHandler(() => handlerRan = true))
            .UseBehavior(new ShortCircuitVoidBehavior(), order: 0)
            .Build();

        await router.SendAsync(new PingCommand("x"));

        Assert.False(handlerRan);
    }

    [Fact]
    public async Task SendAsync_ResultBehavior_CallsNext_ReturnsHandlerResult()
    {
        var log = new List<string>();
        var router = new CommandRouterBuilder()
            .Register(new LoggingAddHandler(log))
            .UseBehavior(new LoggingResultBehavior(log, "behavior"), order: 0)
            .Build();

        var result = await router.SendAsync<AddCommand, int>(new AddCommand(2, 3));

        Assert.Equal(5, result);
        Assert.Equal(new[] { "behavior:in", "handler", "behavior:out" }, log);
    }

    [Fact]
    public async Task SendAsync_ResultBehavior_OmitsNext_ReturnsOwnResult()
    {
        var handlerRan = false;
        var router = new CommandRouterBuilder()
            .Register(new CallbackAddHandler(() => handlerRan = true))
            .UseBehavior(new ShortCircuitResultBehavior(42), order: 0)
            .Build();

        var result = await router.SendAsync<AddCommand, int>(new AddCommand(2, 3));

        Assert.Equal(42, result);
        Assert.False(handlerRan);
    }

    [Fact]
    public async Task SendAsync_LowerOrderBehavior_RunsOutermostInbound()
    {
        var log = new List<string>();
        var router = new CommandRouterBuilder()
            .Register(new LoggingVoidHandler(log))
            .UseBehavior(new LoggingVoidBehavior(log, "inner"), order: 10)
            .UseBehavior(new LoggingVoidBehavior(log, "outer"), order: 0)
            .Build();

        await router.SendAsync(new PingCommand("x"));

        Assert.Equal(
            new[] { "outer:in", "inner:in", "handler", "inner:out", "outer:out" },
            log);
    }

    [Fact]
    public async Task SendAsync_Result_LowerOrderBehavior_RunsOutermostInbound()
    {
        var log = new List<string>();
        var router = new CommandRouterBuilder()
            .Register(new LoggingAddHandler(log))
            .UseBehavior(new LoggingResultBehavior(log, "inner"), order: 10)
            .UseBehavior(new LoggingResultBehavior(log, "outer"), order: 0)
            .Build();

        var result = await router.SendAsync<AddCommand, int>(new AddCommand(1, 1));

        Assert.Equal(2, result);
        Assert.Equal(
            new[] { "outer:in", "inner:in", "handler", "inner:out", "outer:out" },
            log);
    }

    private sealed record PingCommand(string Message);

    private sealed record AddCommand(int Left, int Right);

    private sealed class LoggingVoidHandler : ICommandHandler<PingCommand>
    {
        private readonly List<string> _log;

        public LoggingVoidHandler(List<string> log) => _log = log;

        public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
        {
            _log.Add("handler");
            return default;
        }
    }

    private sealed class CallbackVoidHandler : ICommandHandler<PingCommand>
    {
        private readonly Action _onHandle;

        public CallbackVoidHandler(Action onHandle) => _onHandle = onHandle;

        public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
        {
            _onHandle();
            return default;
        }
    }

    private sealed class ShortCircuitVoidBehavior : ICommandPipelineBehavior<PingCommand>
    {
        public ValueTask InvokeAsync(
            PingCommand command,
            CommandPipelineDelegate<PingCommand> next,
            CancellationToken cancellationToken = default) =>
            default;
    }

    private sealed class LoggingVoidBehavior : ICommandPipelineBehavior<PingCommand>
    {
        private readonly List<string> _log;
        private readonly string _name;

        public LoggingVoidBehavior(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public async ValueTask InvokeAsync(
            PingCommand command,
            CommandPipelineDelegate<PingCommand> next,
            CancellationToken cancellationToken = default)
        {
            _log.Add($"{_name}:in");
            await next(command, cancellationToken).ConfigureAwait(false);
            _log.Add($"{_name}:out");
        }
    }

    private sealed class LoggingAddHandler : ICommandHandler<AddCommand, int>
    {
        private readonly List<string> _log;

        public LoggingAddHandler(List<string> log) => _log = log;

        public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default)
        {
            _log.Add("handler");
            return new(command.Left + command.Right);
        }
    }

    private sealed class CallbackAddHandler : ICommandHandler<AddCommand, int>
    {
        private readonly Action _onHandle;

        public CallbackAddHandler(Action onHandle) => _onHandle = onHandle;

        public ValueTask<int> HandleAsync(AddCommand command, CancellationToken cancellationToken = default)
        {
            _onHandle();
            return new(command.Left + command.Right);
        }
    }

    private sealed class LoggingResultBehavior : ICommandPipelineBehavior<AddCommand, int>
    {
        private readonly List<string> _log;
        private readonly string _name;

        public LoggingResultBehavior(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public async ValueTask<int> InvokeAsync(
            AddCommand command,
            CommandPipelineDelegate<AddCommand, int> next,
            CancellationToken cancellationToken = default)
        {
            _log.Add($"{_name}:in");
            var result = await next(command, cancellationToken).ConfigureAwait(false);
            _log.Add($"{_name}:out");
            return result;
        }
    }

    private sealed class ShortCircuitResultBehavior : ICommandPipelineBehavior<AddCommand, int>
    {
        private readonly int _result;

        public ShortCircuitResultBehavior(int result) => _result = result;

        public ValueTask<int> InvokeAsync(
            AddCommand command,
            CommandPipelineDelegate<AddCommand, int> next,
            CancellationToken cancellationToken = default) =>
            new(_result);
    }
}
