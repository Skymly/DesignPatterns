using DesignPatterns.Creational;

namespace DesignPatterns.Tests.Integration.Singleton;

[GenerateSingleton]
public partial class IntegrationAppSettings
{
    public string AppName { get; set; } = "TestApp";
}

[GenerateSingleton(ThreadSafe = false)]
public partial class IntegrationFastCache
{
    public int Value { get; set; }
}

public sealed class GenerateSingletonIntegrationTests
{
    [Fact]
    public void GeneratedSingleton_ThreadSafeInstance_ReturnsSameReference()
    {
        var first = IntegrationAppSettings.Instance;
        var second = IntegrationAppSettings.Instance;

        Assert.Same(first, second);
        Assert.Equal("TestApp", first.AppName);
    }

    [Fact]
    public void GeneratedSingleton_NonThreadSafeInstance_ReturnsSameReference()
    {
        var first = IntegrationFastCache.Instance;
        var second = IntegrationFastCache.Instance;

        Assert.Same(first, second);
    }
}
