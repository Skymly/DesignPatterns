using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// A Singleton factory-delegate registration collected for captive-dependency
/// analysis (DP066). Only MSDI <c>AddSingleton&lt;T&gt;(factory)</c> and Autofac
/// <c>Register(...).SingleInstance()</c> lambda registrations are included.
/// </summary>
internal sealed class FactoryDelegateRegistration
{
    public FactoryDelegateRegistration(
        INamedTypeSymbol serviceType,
        AnonymousFunctionExpressionSyntax lambda,
        SemanticModel semanticModel)
    {
        ServiceType = serviceType;
        Lambda = lambda;
        SemanticModel = semanticModel;
    }

    public INamedTypeSymbol ServiceType { get; }

    public AnonymousFunctionExpressionSyntax Lambda { get; }

    /// <summary>
    /// Semantic model of the lambda's tree, captured at collection time.
    /// </summary>
    public SemanticModel SemanticModel { get; }
}
