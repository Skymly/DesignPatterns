using DesignPatterns.Structural;

namespace DesignPatterns.Tests.Structural;

public sealed class CompositePartAttributeTests
{
    private interface ITestContract
    {
    }

    [Fact]
    public void Constructor_SetsKeyAndFor()
    {
        var attribute = new CompositePartAttribute("root", typeof(ITestContract));

        Assert.Equal("root", attribute.Key);
        Assert.Equal(typeof(ITestContract), attribute.For);
        Assert.Null(attribute.ParentKey);
        Assert.Equal(0, attribute.Order);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_Throws(string? key)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new CompositePartAttribute(key!, typeof(ITestContract)));

        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullFor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositePartAttribute("root", null!));
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void GenericAttribute_Constructor_SetsKeyAndOptionalProperties()
    {
        var attribute = new CompositePartAttribute<ITestContract>("settings")
        {
            ParentKey = "root",
            Order = 5,
        };

        Assert.Equal("settings", attribute.Key);
        Assert.Equal("root", attribute.ParentKey);
        Assert.Equal(5, attribute.Order);
    }
#endif
}
