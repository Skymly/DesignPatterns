using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using DesignPatterns.SourceGenerators.Generators.StateTransition;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Shared guard method signature validator used by State, Strategy, and Chain
/// generators. Resolves a named static method on a type and verifies its
/// return type and parameter types match the expected signature.
/// </summary>
internal static class GuardMethodValidator
{
    /// <summary>
    /// Resolves a guard method on <paramref name="holderType"/> and verifies
    /// it is a static method returning <see cref="bool"/> with parameters
    /// matching <paramref name="expectedParameterTypes"/>.
    /// </summary>
    /// <param name="holderType">The type that should declare the method.</param>
    /// <param name="methodName">The guard method name (from the attribute).</param>
    /// <param name="expectedParameterTypes">Expected parameter types in order.</param>
    /// <returns>
    /// A <see cref="GuardResolution"/> describing whether the method was found,
    /// is static, has a valid signature, and the fully-qualified method reference
    /// for code emission.
    /// </returns>
    internal static GuardResolution Resolve(
        INamedTypeSymbol holderType,
        string methodName,
        ImmutableArray<ITypeSymbol> expectedParameterTypes)
    {
        var methods = holderType.GetMembers(methodName)
            .OfType<IMethodSymbol>()
            .ToList();

        if (methods.Count == 0)
        {
            return new GuardResolution(methodName, IsFound: false, IsStatic: false, HasValidSignature: false, MethodReference: null);
        }

        var holderFqn = holderType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // When overloads exist, prefer the method with the correct signature so
        // that an unrelated overload does not cause a false wrong-signature diagnostic.
        IMethodSymbol? matching = null;
        foreach (var m in methods)
        {
            if (m.IsStatic
                && m.ReturnType.SpecialType == SpecialType.System_Boolean
                && m.Parameters.Length == expectedParameterTypes.Length
                && ParametersMatch(m, expectedParameterTypes))
            {
                matching = m;
                break;
            }
        }

        if (matching is not null)
        {
            return new GuardResolution(
                methodName,
                IsFound: true,
                IsStatic: true,
                HasValidSignature: true,
                MethodReference: $"{holderFqn}.{matching.Name}");
        }

        // No matching signature found — report based on the first candidate.
        var first = methods[0];
        return new GuardResolution(
            methodName,
            IsFound: true,
            IsStatic: first.IsStatic,
            HasValidSignature: false,
            MethodReference: null);
    }

    private static bool ParametersMatch(IMethodSymbol method, ImmutableArray<ITypeSymbol> expected)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(method.Parameters[i].Type, expected[i]))
            {
                return false;
            }
        }

        return true;
    }
}
