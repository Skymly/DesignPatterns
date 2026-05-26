using DesignPatterns.Creational;

namespace GenerateSingleton.Sample;

[GenerateSingleton]
public partial class AppSettings
{
    public string AppName { get; set; } = "DesignPatterns";
}

[GenerateSingleton(ThreadSafe = false)]
public partial class FastCache
{
}
