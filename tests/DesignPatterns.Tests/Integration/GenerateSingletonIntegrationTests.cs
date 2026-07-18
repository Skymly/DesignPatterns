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

[GenerateSingleton(InitializeAsync = nameof(InitializeAsync))]
public partial class IntegrationAsyncSettings
{
    public bool Initialized { get; private set; }

    public static System.Threading.Tasks.Task InitializeAsync(
        IntegrationAsyncSettings settings,
        System.Threading.CancellationToken cancellationToken)
    {
        settings.Initialized = true;
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

[GenerateSingleton(InitializeAsync = nameof(InitializeAsync))]
public partial class IntegrationValueTaskSettings
{
    public bool Initialized { get; private set; }

    public static System.Threading.Tasks.ValueTask InitializeAsync(
        IntegrationValueTaskSettings settings,
        System.Threading.CancellationToken cancellationToken)
    {
        settings.Initialized = true;
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
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

    [Fact]
    public async System.Threading.Tasks.Task GeneratedSingleton_AsyncInitializer_ReturnsInitializedSameReference()
    {
        var first = await IntegrationAsyncSettings.GetInstanceAsync();
        var second = await IntegrationAsyncSettings.GetInstanceAsync();

        Assert.Same(first, second);
        Assert.True(first.Initialized);
    }

    [Fact]
    public async System.Threading.Tasks.Task GeneratedSingleton_ValueTaskInitializer_ReturnsInitializedInstance()
    {
        var instance = await IntegrationValueTaskSettings.GetInstanceAsync();

        Assert.True(instance.Initialized);
    }
}
