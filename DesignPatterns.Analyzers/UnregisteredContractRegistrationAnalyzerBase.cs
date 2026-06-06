using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

public abstract class UnregisteredContractRegistrationAnalyzerBase : DiagnosticAnalyzer
{
    protected abstract DiagnosticDescriptor Rule { get; }

    protected abstract bool IsRegistrationAttribute(INamedTypeSymbol? attributeClass);

    public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public sealed override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private void OnCompilationStart(CompilationStartAnalysisContext context)
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

    private void AnalyzeNamedType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> registeredContracts)
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
            if (!AnalyzerSymbolHelper.ImplementsContract(typeSymbol, contract))
            {
                continue;
            }

            if (HasRegistrationForContract(typeSymbol, contract))
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

    private ImmutableHashSet<INamedTypeSymbol> CollectRegisteredContracts(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(compilation.Assembly.GlobalNamespace))
        {
            foreach (var contract in GetRegistrationContractsFromType(typeSymbol))
            {
                builder.Add(contract);
            }
        }

        return builder.ToImmutable();
    }

    private IEnumerable<INamedTypeSymbol> GetRegistrationContractsFromType(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsRegistrationAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var contract = AnalyzerSymbolHelper.TryGetContractTypeFromAttribute(attribute);
            if (contract is not null)
            {
                yield return contract;
            }
        }
    }

    private bool HasRegistrationForContract(INamedTypeSymbol typeSymbol, INamedTypeSymbol contract) =>
        GetRegistrationContractsFromType(typeSymbol).Any(
            registeredContract => SymbolEqualityComparer.Default.Equals(registeredContract, contract));
}
