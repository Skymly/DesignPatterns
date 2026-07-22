using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// Helpers for Autofac registration calls matched by namespace (no Autofac package reference).
/// </summary>
internal static class AutofacRegistration
{
    internal static bool IsAutofacMethod(IMethodSymbol? methodSymbol) =>
        methodSymbol?.ContainingNamespace?.ToDisplayString()
            .StartsWith("Autofac", StringComparison.Ordinal) == true;

    /// <summary>
    /// Walks up the fluent chain from an Autofac registration call and returns
    /// the declared lifetime. Intermediate calls (e.g. <c>As&lt;T&gt;()</c>)
    /// are skipped; without an explicit lifetime call Autofac defaults to
    /// InstancePerDependency (Transient).
    /// </summary>
    internal static Lifetime ResolveChainLifetime(InvocationExpressionSyntax registrationCall)
    {
        var lifetime = Lifetime.Transient;
        SyntaxNode node = registrationCall;

        while (node.Parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
        {
            switch (memberAccess.Name.Identifier.ValueText)
            {
                case "SingleInstance":
                    lifetime = Lifetime.Singleton;
                    break;
                case "InstancePerLifetimeScope":
                case "InstancePerMatchingLifetimeScope":
                case "InstancePerRequest":
                case "InstancePerOwned":
                    lifetime = Lifetime.Scoped;
                    break;
                case "InstancePerDependency":
                    lifetime = Lifetime.Transient;
                    break;
            }

            node = parentInvocation;
        }

        return lifetime;
    }

    internal static bool IsOpenGeneric(INamedTypeSymbol type) =>
        type.IsUnboundGenericType ||
        (type.TypeParameters.Length > 0 && type.IsDefinition);
}
