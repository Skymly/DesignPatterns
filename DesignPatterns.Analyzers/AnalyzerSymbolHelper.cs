using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.Analyzers;

internal static class AnalyzerSymbolHelper
{
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
}
