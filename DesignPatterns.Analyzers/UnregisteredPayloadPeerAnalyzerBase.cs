using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Peer-presence registration analyzer base: reports concrete implementations that
/// declare a peer (via interface) already registered elsewhere but lack the matching
/// registration attribute on the same type.
/// </summary>
/// <remarks>
/// Contract-peer analyzers (Strategy/Factory) stay on
/// <see cref="UnregisteredContractRegistrationAnalyzerBase"/>. Adapters supply
/// attribute-peer extraction and declared-peer extraction only; assembly walk,
/// empty-registry short-circuit, candidate filters, and reporting live here.
/// </remarks>
public abstract class UnregisteredPayloadPeerAnalyzerBase : DiagnosticAnalyzer
{
    protected abstract DiagnosticDescriptor Rule { get; }

    /// <summary>
    /// Peers declared by registration attributes on <paramref name="typeSymbol"/>.
    /// </summary>
    protected abstract IEnumerable<INamedTypeSymbol> GetPeersFromRegistrationAttributes(
        INamedTypeSymbol typeSymbol);

    /// <summary>
    /// Peers declared by implemented contracts on <paramref name="typeSymbol"/>
    /// (e.g. <c>IEventHandler&lt;T&gt;</c> → <c>T</c>).
    /// </summary>
    protected abstract IEnumerable<INamedTypeSymbol> GetDeclaredPeers(INamedTypeSymbol typeSymbol);

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
        var registeredPeers = CollectRegisteredPeers(context.Compilation);
        if (registeredPeers.IsEmpty)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, registeredPeers),
            SymbolKind.NamedType);
    }

    private void AnalyzeNamedType(
        SymbolAnalysisContext context,
        ImmutableHashSet<INamedTypeSymbol> registeredPeers)
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

        foreach (var peer in GetDeclaredPeers(typeSymbol))
        {
            if (!registeredPeers.Contains(peer))
            {
                continue;
            }

            if (HasRegistrationForPeer(typeSymbol, peer))
            {
                continue;
            }

            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                typeSymbol.Name,
                peer.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private ImmutableHashSet<INamedTypeSymbol> CollectRegisteredPeers(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var peer in GetPeersFromRegistrationAttributes(typeSymbol))
                {
                    builder.Add(peer);
                }
            }
        }

        return builder.ToImmutable();
    }

    private bool HasRegistrationForPeer(INamedTypeSymbol typeSymbol, INamedTypeSymbol peer) =>
        GetPeersFromRegistrationAttributes(typeSymbol).Any(
            registeredPeer => SymbolEqualityComparer.Default.Equals(registeredPeer, peer));
}
