using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// A single registration contributing to the registration map
/// (explicit container registration or attributed <c>RegisterDi</c> expansion).
/// </summary>
internal sealed class DiRegistration
{
    public DiRegistration(
        INamedTypeSymbol implementationType,
        Lifetime lifetime,
        InvocationExpressionSyntax invocation,
        bool skipConstructorAnalysis = false)
    {
        ImplementationType = implementationType;
        Lifetime = lifetime;
        Invocation = invocation;
        SkipConstructorAnalysis = skipConstructorAnalysis;
    }

    public INamedTypeSymbol ImplementationType { get; }

    public Lifetime Lifetime { get; }

    public InvocationExpressionSyntax Invocation { get; }

    /// <summary>
    /// True for factory-delegate and instance registrations: the container
    /// never invokes the implementation constructor, so constructor analysis
    /// does not apply (the type still participates in the lifetime map).
    /// </summary>
    public bool SkipConstructorAnalysis { get; }
}
