using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete handler implementations that implement <c>IHandler&lt;TContext&gt;</c> for a registered context but lack <c>[HandlerOrder]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredHandlerAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.HandlerOrderUnregisteredImplementation;

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
        var registeredContexts = CollectRegisteredContexts(context.Compilation);
        if (registeredContexts.IsEmpty)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, registeredContexts),
            SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> registeredContexts)
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

        foreach (var contextType in GetHandlerContextTypes(typeSymbol))
        {
            if (!registeredContexts.Contains(contextType))
            {
                continue;
            }

            if (HasHandlerOrderForContext(typeSymbol, contextType))
            {
                continue;
            }

            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                typeSymbol.Name,
                contextType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectRegisteredContexts(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var contextType in GetHandlerOrderContextTypes(typeSymbol))
                {
                    builder.Add(contextType);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static IEnumerable<INamedTypeSymbol> GetHandlerContextTypes(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name != "IHandler" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (iface.TypeArguments[0] is INamedTypeSymbol contextType)
            {
                yield return contextType;
            }
        }
    }

    private static bool HasHandlerOrderForContext(INamedTypeSymbol typeSymbol, INamedTypeSymbol contextType) =>
        GetHandlerOrderContextTypes(typeSymbol).Any(
            registeredContext => SymbolEqualityComparer.Default.Equals(registeredContext, contextType));

    private static IEnumerable<INamedTypeSymbol> GetHandlerOrderContextTypes(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsHandlerOrderAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var context = TryGetContextFromAttribute(attribute);
            if (context is not null)
            {
                yield return context;
            }
        }
    }

    private static bool IsHandlerOrderAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        var metadataName = attributeClass.MetadataName;
        if (metadataName == "HandlerOrderAttribute")
        {
            return true;
        }

        return attributeClass.OriginalDefinition.MetadataName == "HandlerOrderAttribute`1";
    }

    private static INamedTypeSymbol? TryGetContextFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.IsGenericType == true)
        {
            return attribute.AttributeClass.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;
        }

        if (attribute.ConstructorArguments.Length >= 2 &&
            attribute.ConstructorArguments[1].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[1].Value is INamedTypeSymbol nonGenericContext)
        {
            return nonGenericContext;
        }

        return null;
    }

}
