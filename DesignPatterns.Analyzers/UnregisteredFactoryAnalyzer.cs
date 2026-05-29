using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete factory implementations that implement a registered contract but lack <c>[RegisterFactory]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredFactoryAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.RegisterFactoryUnregisteredImplementation,
        title: "Factory implementation is not registered",
        messageFormat: "Type '{0}' implements factory contract '{1}' but is missing [RegisterFactory]",
        category: "DesignPatterns.Analyzers",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var registeredContracts = CollectRegisteredContracts(context.Compilation);
        if (registeredContracts.IsEmpty)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, registeredContracts),
            SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> registeredContracts)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
        {
            return;
        }

        if (typeSymbol.DeclaredAccessibility == Accessibility.Private && typeSymbol.ContainingType is not null)
        {
            return;
        }

        foreach (var contract in registeredContracts)
        {
            if (!ImplementsContract(typeSymbol, contract))
            {
                continue;
            }

            if (HasRegisterFactoryForContract(typeSymbol, contract))
            {
                continue;
            }

            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                typeSymbol.Name,
                contract.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectRegisteredContracts(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var typeSymbol in GetAllTypes(compilation.Assembly.GlobalNamespace))
        {
            foreach (var contract in GetRegisterFactoryContracts(typeSymbol))
            {
                builder.Add(contract);
            }
        }

        return builder.ToImmutable();
    }

    private static bool ImplementsContract(INamedTypeSymbol typeSymbol, INamedTypeSymbol contract) =>
        typeSymbol.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) ||
        SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType, contract);

    private static bool HasRegisterFactoryForContract(INamedTypeSymbol typeSymbol, INamedTypeSymbol contract) =>
        GetRegisterFactoryContracts(typeSymbol).Any(
            registeredContract => SymbolEqualityComparer.Default.Equals(registeredContract, contract));

    private static IEnumerable<INamedTypeSymbol> GetRegisterFactoryContracts(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsRegisterFactoryAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var contract = TryGetContractFromAttribute(attribute);
            if (contract is not null)
            {
                yield return contract;
            }
        }
    }

    private static bool IsRegisterFactoryAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.ToDisplayString() == FactoryAnalysisConstants.RegisterFactoryMetadataName ||
               attributeClass.OriginalDefinition.ToDisplayString() == FactoryAnalysisConstants.RegisterFactoryGenericMetadataName;
    }

    private static INamedTypeSymbol? TryGetContractFromAttribute(AttributeData attribute)
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

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
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
}
