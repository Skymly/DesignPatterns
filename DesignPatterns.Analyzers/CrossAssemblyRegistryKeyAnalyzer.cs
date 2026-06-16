using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports duplicate strategy keys for the same contract when multiple referenced provider assemblies are in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrossAssemblyRegistryKeyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.PluginRegistryDuplicateKeyAcrossAssemblies;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var registrations = CollectStrategyRegistrations(context.Compilation);
        if (registrations.IsEmpty)
        {
            return;
        }

        ReportDuplicates(context, registrations);
    }

    private static void ReportDuplicates(
        CompilationAnalysisContext context,
        ImmutableArray<StrategyRegistration> registrations)
    {
        foreach (var group in registrations.GroupBy(
                     static registration => (
                         Contract: GetContractIdentity(registration.Contract),
                         registration.Key),
                     EqualityComparer<(string Contract, string Key)>.Default))
        {
            var assemblyNames = string.Join(
                ", ",
                group
                    .Select(static registration => registration.Assembly.Name ?? string.Empty)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static name => name, StringComparer.Ordinal));

            if (group.Select(static registration => registration.Assembly).Distinct(SymbolEqualityComparer.Default).Count() <= 1)
            {
                continue;
            }

            foreach (var registration in group)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    registration.Location,
                    group.Key.Key,
                    group.Key.Contract,
                    assemblyNames));
            }
        }
    }

    private static string GetContractIdentity(INamedTypeSymbol contract) =>
        contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static ImmutableArray<StrategyRegistration> CollectStrategyRegistrations(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<StrategyRegistration>();

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    if (!StrategyAnalysisConstants.IsRegisterStrategyAttribute(attribute.AttributeClass))
                    {
                        continue;
                    }

                    var contract = AnalyzerSymbolHelper.TryGetContractTypeFromAttribute(attribute);
                    var key = AnalyzerSymbolHelper.TryGetKeyFromAttribute(attribute);
                    if (contract is null || key is null)
                    {
                        continue;
                    }

                    var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                        ?? typeSymbol.Locations.FirstOrDefault()
                        ?? Location.None;

                    builder.Add(new StrategyRegistration(contract, key, assembly, location));
                }
            }
        }

        return builder.ToImmutable();
    }

    private sealed class StrategyRegistration
    {
        public StrategyRegistration(
            INamedTypeSymbol contract,
            string key,
            IAssemblySymbol assembly,
            Location location)
        {
            Contract = contract;
            Key = key;
            Assembly = assembly;
            Location = location;
        }

        public INamedTypeSymbol Contract { get; }

        public string Key { get; }

        public IAssemblySymbol Assembly { get; }

        public Location Location { get; }
    }
}
