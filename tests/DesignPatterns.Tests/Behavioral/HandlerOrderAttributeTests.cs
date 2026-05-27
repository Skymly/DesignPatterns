using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class HandlerOrderAttributeTests
{
    [Fact]
    public void Constructor_SetsOrderAndContextType()
    {
        var attribute = new HandlerOrderAttribute(10, typeof(string));

        Assert.Equal(10, attribute.Order);
        Assert.Equal(typeof(string), attribute.ContextType);
    }

    [Fact]
    public void Constructor_NullContextType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HandlerOrderAttribute(0, null!));
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void GenericAttribute_Constructor_SetsOrder()
    {
        var attribute = new HandlerOrderAttribute<int>(5);

        Assert.Equal(5, attribute.Order);
    }
#endif
}
