using DesignPatterns.Creational;

namespace DesignPatterns.Tests.Creational;

public sealed class GenerateBuilderAttributeTests
{
    [Fact]
    public void GenerateBuilder_CanBeAppliedToSchemaHolder()
    {
        var attribute = new GenerateBuilderAttribute();

        Assert.NotNull(attribute);
        Assert.IsAssignableFrom<Attribute>(attribute);
        Assert.NotEmpty(typeof(AnnotatedHolder).GetCustomAttributes(typeof(GenerateBuilderAttribute), inherit: false));
    }

    [Fact]
    public void BuilderStep_DefaultsToRequired()
    {
        var attribute = new BuilderStepAttribute();

        Assert.True(attribute.Required);
        Assert.Null(attribute.MutexGroup);
        Assert.Null(attribute.After);
        Assert.Null(attribute.Before);
    }

    [Fact]
    public void BuilderStep_CanDeclareOptionalMutexAndOrderMetadata()
    {
        var attribute = new BuilderStepAttribute
        {
            Required = false,
            MutexGroup = "Auth",
            After = nameof(SampleHolder.WithUrl),
            Before = nameof(SampleHolder.WithBody),
        };

        Assert.False(attribute.Required);
        Assert.Equal("Auth", attribute.MutexGroup);
        Assert.Equal(nameof(SampleHolder.WithUrl), attribute.After);
        Assert.Equal(nameof(SampleHolder.WithBody), attribute.Before);
    }

    [Fact]
    public void BuilderAssemble_CanMarkAssembleMethod()
    {
        var attribute = new BuilderAssembleAttribute();
        var assemble = typeof(AnnotatedHolder).GetMethod(nameof(AnnotatedHolder.Assemble))!;

        Assert.NotNull(attribute);
        Assert.IsAssignableFrom<Attribute>(attribute);
        Assert.NotEmpty(assemble.GetCustomAttributes(typeof(BuilderAssembleAttribute), inherit: false));
    }

    [Fact]
    public void BuilderStep_CanAnnotateStepMethodsOnHolder()
    {
        var withUrl = typeof(AnnotatedHolder).GetMethod(nameof(AnnotatedHolder.WithUrl))!;
        var withToken = typeof(AnnotatedHolder).GetMethod(nameof(AnnotatedHolder.WithBearerToken))!;
        var step = Assert.Single(withUrl.GetCustomAttributes(typeof(BuilderStepAttribute), inherit: false));
        var optional = Assert.IsType<BuilderStepAttribute>(
            Assert.Single(withToken.GetCustomAttributes(typeof(BuilderStepAttribute), inherit: false)));

        Assert.IsType<BuilderStepAttribute>(step);
        Assert.False(optional.Required);
        Assert.Equal("Auth", optional.MutexGroup);
        Assert.Equal(nameof(AnnotatedHolder.WithUrl), optional.After);
    }

    [Fact]
    public void TypeStateMarkers_NotSetAndSetAreDistinctTypes()
    {
        Assert.NotSame(typeof(BuilderStepState.NotSet), typeof(BuilderStepState.Set));
        Assert.True(typeof(BuilderStepState.NotSet).IsClass);
        Assert.True(typeof(BuilderStepState.Set).IsClass);
        Assert.True(typeof(BuilderStepState.NotSet).IsSealed);
        Assert.True(typeof(BuilderStepState.Set).IsSealed);
    }

    [Fact]
    public void TypeStateMarkers_CanBeUsedAsGenericTypeArguments()
    {
        var unset = new MarkerBox<BuilderStepState.NotSet>();
        var set = new MarkerBox<BuilderStepState.Set>();

        Assert.Equal(typeof(BuilderStepState.NotSet), unset.MarkerType);
        Assert.Equal(typeof(BuilderStepState.Set), set.MarkerType);
    }

    private sealed class MarkerBox<TMarker>
    {
        public Type MarkerType => typeof(TMarker);
    }

    /// <summary>Schema-shaped fixture used only for nameof references in attribute tests.</summary>
    private static class SampleHolder
    {
        public static void WithUrl()
        {
        }

        public static void WithBody()
        {
        }
    }

    /// <summary>Compile-time annotation smoke: holder + required/optional steps + assemble.</summary>
    [GenerateBuilder]
    private static class AnnotatedHolder
    {
        [BuilderStep]
        public static void WithUrl(string url)
        {
        }

        [BuilderStep(Required = false, MutexGroup = "Auth", After = nameof(WithUrl))]
        public static void WithBearerToken(string token)
        {
        }

        [BuilderAssemble]
        public static string Assemble(string url, string? bearerToken) => url;
    }
}
