using System;
using DesignPatterns.Behavioral;

namespace MetaPackage.Consumer.Net48;

internal static class Program
{
    private static void Main()
    {
        var strategy = Net48ConsumerStrategyRegistry.Instance.Get(Net48ConsumerStrategyKeys.Echo);
        if (strategy.Execute("ok") != "echo:ok")
        {
            throw new InvalidOperationException("Generated strategy registry did not resolve the expected implementation.");
        }

        Console.WriteLine("NuGet meta-package net48 consumer smoke test passed.");
    }
}

public interface INet48ConsumerStrategy
{
    string Execute(string value);
}

[RegisterStrategy("echo", typeof(INet48ConsumerStrategy))]
public sealed class EchoStrategy : INet48ConsumerStrategy
{
    public string Execute(string value) => "echo:" + value;
}
