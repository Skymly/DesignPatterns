using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class RegisterStrategyAttributeTests
{
    private interface ITestContract
    {
    }

    [Fact]
    public void Constructor_SetsKeyAndFor()
    {
        var attribute = new RegisterStrategyAttribute("alpha", typeof(ITestContract));

        Assert.Equal("alpha", attribute.Key);
        Assert.Equal(typeof(ITestContract), attribute.For);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_Throws(string? key)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new RegisterStrategyAttribute(key!, typeof(ITestContract)));

        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullFor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterStrategyAttribute("alpha", null!));
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void GenericAttribute_Constructor_SetsKey()
    {
        var attribute = new RegisterStrategyAttribute<ITestContract>("beta");

        Assert.Equal("beta", attribute.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenericAttribute_EmptyKey_Throws(string? key)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new RegisterStrategyAttribute<ITestContract>(key!));

        Assert.Equal("key", ex.ParamName);
    }
#endif
}
