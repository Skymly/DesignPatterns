using DesignPatterns.Behavioral;
using DesignPatterns.Creational;

var strategy = MetaPackageConsumerStrategyRegistry.Instance.Get("echo");
if (strategy.Execute("ok") != "echo:ok")
{
    throw new InvalidOperationException("Generated strategy registry did not resolve the expected implementation.");
}

if (!ReferenceEquals(MetaPackageConsumerSingleton.Instance, MetaPackageConsumerSingleton.Instance))
{
    throw new InvalidOperationException("Generated singleton instance was not stable.");
}

Console.WriteLine("NuGet meta-package consumer smoke test passed.");

public interface IMetaPackageConsumerStrategy
{
    string Execute(string value);
}

[RegisterStrategy("echo", typeof(IMetaPackageConsumerStrategy))]
public sealed class EchoStrategy : IMetaPackageConsumerStrategy
{
    public string Execute(string value) => "echo:" + value;
}

[GenerateSingleton]
public sealed partial class MetaPackageConsumerSingleton
{
}
