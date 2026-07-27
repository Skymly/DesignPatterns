using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac.Tests.CommandHandlers;

public sealed class HandledCommandsCollector
{
    public List<string> Commands { get; } = new();
}

public sealed class PingCommand : ICommand;

[RegisterCommandHandler<PingCommand>]
public sealed class PingCommandHandler : ICommandHandler<PingCommand>
{
    private readonly HandledCommandsCollector _collector;

    public PingCommandHandler(HandledCommandsCollector collector) => _collector = collector;

    public ValueTask HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
    {
        _collector.Commands.Add("Ping");
        return default;
    }
}

public sealed class AddNumbersCommand : ICommand<int>
{
    public AddNumbersCommand(int left, int right)
    {
        Left = left;
        Right = right;
    }

    public int Left { get; }

    public int Right { get; }
}

[RegisterCommandHandler<AddNumbersCommand>]
public sealed class AddNumbersCommandHandler : ICommandHandler<AddNumbersCommand, int>
{
    private readonly HandledCommandsCollector _collector;

    public AddNumbersCommandHandler(HandledCommandsCollector collector) => _collector = collector;

    public ValueTask<int> HandleAsync(AddNumbersCommand command, CancellationToken cancellationToken = default)
    {
        _collector.Commands.Add($"Add:{command.Left}+{command.Right}");
        return new ValueTask<int>(command.Left + command.Right);
    }
}
