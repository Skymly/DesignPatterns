using DesignPatterns.Creational;

namespace DesignPatterns.Tests.Creational;

public sealed class GenerateSingletonAttributeTests
{
    [Fact]
    public void ThreadSafe_DefaultsToTrue()
    {
        var attribute = new GenerateSingletonAttribute();

        Assert.True(attribute.ThreadSafe);
    }

    [Fact]
    public void ThreadSafe_CanBeSetToFalse()
    {
        var attribute = new GenerateSingletonAttribute { ThreadSafe = false };

        Assert.False(attribute.ThreadSafe);
    }
}
