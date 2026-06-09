using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers;

internal static class AnalyzerSymbolHelper
{
    internal static IEnumerable<IAssemblySymbol> GetAssembliesInCompilation(Compilation compilation)
    {
        yield return compilation.Assembly;

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                yield return assemblySymbol;
            }
        }
    }

    internal static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol nestedNamespace:
                    foreach (var nested in GetAllTypes(nestedNamespace))
                    {
                        yield return nested;
                    }

                    break;
                case INamedTypeSymbol typeSymbol:
                    yield return typeSymbol;
                    foreach (var nestedType in typeSymbol.GetTypeMembers())
                    {
                        yield return nestedType;
                    }

                    break;
            }
        }
    }

    internal static bool ImplementsContract(INamedTypeSymbol typeSymbol, INamedTypeSymbol contract) =>
        typeSymbol.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) ||
        SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType, contract);

    internal static INamedTypeSymbol? TryGetContractTypeFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.IsGenericType == true)
        {
            return attribute.AttributeClass.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;
        }

        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Kind == TypedConstantKind.Type && argument.Value is INamedTypeSymbol contract)
            {
                return contract;
            }
        }

        if (attribute.ConstructorArguments.Length >= 2 &&
            attribute.ConstructorArguments[1].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[1].Value is INamedTypeSymbol nonGenericContract)
        {
            return nonGenericContract;
        }

        return null;
    }

    internal static string? TryGetKeyFromAttribute(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return null;
        }

        if (attribute.ConstructorArguments[0].Kind != TypedConstantKind.Primitive)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value as string;
    }

    internal static bool IsKeyedRegistrationAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        var metadataName = attributeClass.ToDisplayString();
        var genericMetadataName = attributeClass.OriginalDefinition.ToDisplayString();

        return metadataName == StrategyAnalysisConstants.RegisterStrategyMetadataName ||
               genericMetadataName == StrategyAnalysisConstants.RegisterStrategyGenericMetadataName ||
               metadataName == FactoryAnalysisConstants.RegisterFactoryMetadataName ||
               genericMetadataName == FactoryAnalysisConstants.RegisterFactoryGenericMetadataName;
    }

    internal static ImmutableDictionary<INamedTypeSymbol, ImmutableHashSet<string>> CollectRegisteredKeysByContract(
        Compilation compilation)
    {
        var builder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableHashSet<string>>(
            SymbolEqualityComparer.Default);

        foreach (var assembly in GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    if (!IsKeyedRegistrationAttribute(attribute.AttributeClass))
                    {
                        continue;
                    }

                    var contract = TryGetContractTypeFromAttribute(attribute);
                    var key = TryGetKeyFromAttribute(attribute);
                    if (contract is null || key is null)
                    {
                        continue;
                    }

                    if (!builder.TryGetValue(contract, out var keys))
                    {
                        keys = ImmutableHashSet<string>.Empty;
                    }

                    builder[contract] = keys.Add(key);
                }
            }
        }

        return builder.ToImmutable();
    }

    internal static bool TryGetRegistryContract(ITypeSymbol receiverType, out INamedTypeSymbol contractType)
    {
        contractType = null!;

        if (receiverType is INamedTypeSymbol namedReceiver &&
            TryGetRegistryContractFromType(namedReceiver, out contractType))
        {
            return true;
        }

        foreach (var iface in receiverType.AllInterfaces)
        {
            if (TryGetRegistryContractFromType(iface, out contractType))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ImplementsNamedGenericInterface(ITypeSymbol typeSymbol, string interfaceName)
    {
        if (typeSymbol is INamedTypeSymbol namedType && IsNamedGenericInterface(namedType, interfaceName))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.Any(iface => IsNamedGenericInterface(iface, interfaceName));
    }

    private static bool TryGetRegistryContractFromType(INamedTypeSymbol typeSymbol, out INamedTypeSymbol contractType)
    {
        contractType = null!;

        if (typeSymbol.TypeArguments.Length != 2)
        {
            return false;
        }

        if (typeSymbol.TypeArguments[0] is not INamedTypeSymbol { SpecialType: SpecialType.System_String })
        {
            return false;
        }

        if (!IsNamedGenericInterface(typeSymbol, "IReadOnlyRegistry"))
        {
            return false;
        }

        if (typeSymbol.TypeArguments[1] is INamedTypeSymbol valueType)
        {
            contractType = valueType;
            return true;
        }

        return false;
    }

    internal static bool IsNamedGenericInterface(INamedTypeSymbol iface, string interfaceName) =>
        iface.Name == interfaceName ||
        iface.OriginalDefinition.Name == interfaceName;

    internal static bool IsRegistryKeyInvocation(ITypeSymbol receiverType, IMethodSymbol method) =>
        TryGetRegistryContract(receiverType, out _) &&
        method.Name switch
        {
            "TryGet" => ImplementsNamedGenericInterface(receiverType, "IReadOnlyRegistry"),
            "Get" => ImplementsNamedGenericInterface(receiverType, "IStrategyRegistry"),
            "Create" or "TryCreate" => ImplementsNamedGenericInterface(receiverType, "IFactoryRegistry"),
            _ => false,
        };

    internal static string FormatRegisteredKeysForMessage(ImmutableHashSet<string> keys) =>
        string.Join(", ", keys.OrderBy(static key => key, StringComparer.Ordinal).Select(static key => $"'{key}'"));

    internal const string SuggestedRegistryKeyPropertyName = "SuggestedRegistryKey";

    internal static string? FindClosestRegistryKey(string wrongKey, IEnumerable<string> registeredKeys, int maxDistance)
    {
        string? bestMatch = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in registeredKeys)
        {
            if (string.Equals(candidate, wrongKey, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            var distance = ComputeLevenshteinDistance(wrongKey, candidate);
            if (distance > maxDistance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestMatch = candidate;
        }

        return bestMatch;
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
