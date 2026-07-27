using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports concrete command handler implementations that implement <c>ICommandHandler&lt;TCommand&gt;</c>
/// (or <c>ICommandHandler&lt;TCommand, TResult&gt;</c>) for a registered command type but lack <c>[RegisterCommandHandler]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredCommandHandlerAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.CommandHandlerUnregisteredImplementation;

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
        var registeredCommandTypes = CollectRegisteredCommandTypes(context.Compilation);
        if (registeredCommandTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, registeredCommandTypes),
            SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> registeredCommandTypes)
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

        foreach (var commandType in GetCommandHandlerCommandTypes(typeSymbol))
        {
            if (!registeredCommandTypes.Contains(commandType))
            {
                continue;
            }

            if (HasRegisterCommandHandlerForCommandType(typeSymbol, commandType))
            {
                continue;
            }

            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                typeSymbol.Name,
                commandType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectRegisteredCommandTypes(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var commandType in GetRegisteredCommandTypesFromType(typeSymbol))
                {
                    builder.Add(commandType);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static IEnumerable<INamedTypeSymbol> GetCommandHandlerCommandTypes(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name != "ICommandHandler")
            {
                continue;
            }

            if (iface.TypeArguments.Length is not (1 or 2))
            {
                continue;
            }

            if (iface.ContainingNamespace.ToDisplayString() != "DesignPatterns.Behavioral")
            {
                continue;
            }

            if (iface.TypeArguments[0] is INamedTypeSymbol commandType)
            {
                yield return commandType;
            }
        }
    }

    private static bool HasRegisterCommandHandlerForCommandType(INamedTypeSymbol typeSymbol, INamedTypeSymbol commandType) =>
        GetRegisteredCommandTypesFromType(typeSymbol).Any(
            registeredCommand => SymbolEqualityComparer.Default.Equals(registeredCommand, commandType));

    private static IEnumerable<INamedTypeSymbol> GetRegisteredCommandTypesFromType(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!CommandHandlerAnalysisConstants.IsRegisterCommandHandlerAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var commandType = TryGetCommandTypeFromAttribute(attribute);
            if (commandType is not null)
            {
                yield return commandType;
            }
        }
    }

    private static INamedTypeSymbol? TryGetCommandTypeFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass?.IsGenericType == true)
        {
            return attribute.AttributeClass.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;
        }

        if (attribute.ConstructorArguments.Length >= 1 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
            attribute.ConstructorArguments[0].Value is INamedTypeSymbol nonGenericCommand)
        {
            return nonGenericCommand;
        }

        return null;
    }
}
