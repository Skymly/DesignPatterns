using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Generators;

internal sealed class KeyedRegistration
{
    public KeyedRegistration(
        string key,
        INamedTypeSymbol contract,
        INamedTypeSymbol implementation,
        Location location)
    {
        Key = key;
        Contract = contract;
        Implementation = implementation;
        Location = location;
    }

    public string Key { get; }

    public INamedTypeSymbol Contract { get; }

    public INamedTypeSymbol Implementation { get; }

    public Location Location { get; }
}

internal readonly struct RegistrationGeneratorDiagnostics
{
    public RegistrationGeneratorDiagnostics(
        DiagnosticDescriptor duplicateKey,
        DiagnosticDescriptor contractMismatch,
        DiagnosticDescriptor missingParameterlessConstructor)
    {
        DuplicateKey = duplicateKey;
        ContractMismatch = contractMismatch;
        MissingParameterlessConstructor = missingParameterlessConstructor;
    }

    public DiagnosticDescriptor DuplicateKey { get; }

    public DiagnosticDescriptor ContractMismatch { get; }

    public DiagnosticDescriptor MissingParameterlessConstructor { get; }
}

internal static class RegistrationGeneratorHelper
{
    internal static List<KeyedRegistration> Transform(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        var result = new List<KeyedRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol implementation)
        {
            return result;
        }

        foreach (var attribute in context.Attributes)
        {
            var key = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            INamedTypeSymbol? contract = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    contract = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 1)
            {
                contract = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
            }

            if (contract is null || contract.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var location = context.TargetNode.GetLocation();
            result.Add(new KeyedRegistration(key!, contract, implementation, location));
        }

        return result;
    }

    internal static void Execute(
        SourceProductionContext context,
        ImmutableArray<KeyedRegistration> nonGeneric,
        ImmutableArray<KeyedRegistration> generic,
        bool enableDiIntegration,
        RegistrationGeneratorDiagnostics diagnostics,
        Action<SourceProductionContext, INamedTypeSymbol, List<KeyedRegistration>, bool, bool> emitGeneratedSources)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .ToList();

        if (registrations.Count == 0)
        {
            return;
        }

        var contractNamesWithCollisions = new HashSet<string>(
            registrations
                .Select(static r => r.Contract)
                .OfType<INamedTypeSymbol>()
                .Distinct(SymbolEqualityComparer.Default)
                .GroupBy(static c => c!.Name, StringComparer.Ordinal)
                .Where(static g => g.Count() > 1)
                .Select(static g => g.Key),
            StringComparer.Ordinal);

        foreach (var group in registrations.GroupBy(static r => r.Contract, SymbolEqualityComparer.Default))
        {
            if (group.Key is not INamedTypeSymbol contract)
            {
                continue;
            }

            var contractRegistrations = group.ToList();
            ReportDuplicateKeys(context, contractRegistrations, diagnostics.DuplicateKey);
            ReportContractMismatches(context, contractRegistrations, diagnostics.ContractMismatch);
            ReportMissingConstructors(context, contractRegistrations, diagnostics.MissingParameterlessConstructor);

            var valid = contractRegistrations
                .Where(r => ImplementsContract(r.Implementation, contract))
                .Where(r => HasPublicParameterlessConstructor(r.Implementation))
                .GroupBy(r => r.Key, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            emitGeneratedSources(
                context,
                contract,
                valid,
                enableDiIntegration,
                contractNamesWithCollisions.Contains(contract.Name));
        }
    }

    private static void ReportDuplicateKeys(
        SourceProductionContext context,
        List<KeyedRegistration> registrations,
        DiagnosticDescriptor descriptor)
    {
        foreach (var duplicateGroup in registrations.GroupBy(static r => r.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var contractName = duplicateGroup.First().Contract.ToDisplayString();
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    registration.Location,
                    registration.Key,
                    contractName));
            }
        }
    }

    private static void ReportContractMismatches(
        SourceProductionContext context,
        List<KeyedRegistration> registrations,
        DiagnosticDescriptor descriptor)
    {
        foreach (var registration in registrations.Where(r => !ImplementsContract(r.Implementation, r.Contract)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                registration.Location,
                registration.Implementation.Name,
                registration.Contract.ToDisplayString()));
        }
    }

    private static void ReportMissingConstructors(
        SourceProductionContext context,
        List<KeyedRegistration> registrations,
        DiagnosticDescriptor descriptor)
    {
        foreach (var registration in registrations.Where(r => !HasPublicParameterlessConstructor(r.Implementation)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                registration.Location,
                registration.Implementation.Name));
        }
    }

    internal static bool ImplementsContract(INamedTypeSymbol implementation, INamedTypeSymbol contract)
    {
        if (SymbolEqualityComparer.Default.Equals(implementation, contract))
        {
            return true;
        }

        foreach (var iface in implementation.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, contract))
            {
                return true;
            }
        }

        for (var baseType = implementation.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, contract))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasPublicParameterlessConstructor(INamedTypeSymbol implementation) =>
        implementation.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);
}
