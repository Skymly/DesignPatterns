using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Detects conflicting generated or mutable static singleton lifecycles.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingletonLifecycleAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateSingletonAttributeName = "DesignPatterns.Creational.GenerateSingletonAttribute";

    private static readonly DiagnosticDescriptor GeneratedAndDiRule =
        DesignPatternsDiagnosticDescriptors.GenerateSingletonDiDoubleRegistration;
    private static readonly DiagnosticDescriptor NonThreadSafeStateRule =
        DesignPatternsDiagnosticDescriptors.GenerateSingletonNonThreadSafeMutableState;
    private static readonly DiagnosticDescriptor StaticMutableRule =
        DesignPatternsDiagnosticDescriptors.StaticMutableSingleton;
    private static readonly DiagnosticDescriptor StaticMutableAndDiRule =
        DesignPatternsDiagnosticDescriptors.StaticMutableSingletonDiDoubleRegistration;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(GeneratedAndDiRule, NonThreadSafeStateRule, StaticMutableRule, StaticMutableAndDiRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var singletonRegistrations = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        context.RegisterSyntaxNodeAction(
            syntaxContext => CollectSingletonRegistration(syntaxContext, singletonRegistrations),
            SyntaxKind.InvocationExpression);
        context.RegisterCompilationEndAction(endContext => Analyze(endContext, singletonRegistrations));
    }

    private static void CollectSingletonRegistration(
        SyntaxNodeAnalysisContext context,
        HashSet<INamedTypeSymbol> registrations)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var isMsDiSingleton = method.Name == "AddSingleton" &&
            method.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal);
        var isAutofacRegistration = method.Name == "RegisterType" &&
            method.ContainingNamespace.ToDisplayString().StartsWith("Autofac", StringComparison.Ordinal) &&
            HasSingleInstanceInChain(invocation);

        if (!isMsDiSingleton && !isAutofacRegistration)
        {
            return;
        }

        var implementation = method.TypeArguments.LastOrDefault() as INamedTypeSymbol;
        if (implementation is not null)
        {
            registrations.Add(implementation);
        }
    }

    private static void Analyze(
        CompilationAnalysisContext context,
        HashSet<INamedTypeSymbol> registrations)
    {
        foreach (var type in AnalyzerSymbolHelper.GetAllTypes(context.Compilation.Assembly.GlobalNamespace))
        {
            var generatedSingleton = type.GetAttributes()
                .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == GenerateSingletonAttributeName);

            if (generatedSingleton is not null)
            {
                AnalyzeGeneratedSingleton(context, type, generatedSingleton, registrations);
            }

            AnalyzeStaticMutableSingleton(context, type, registrations);
        }
    }

    private static void AnalyzeGeneratedSingleton(
        CompilationAnalysisContext context,
        INamedTypeSymbol type,
        AttributeData attribute,
        HashSet<INamedTypeSymbol> registrations)
    {
        var location = type.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        if (registrations.Contains(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GeneratedAndDiRule,
                location,
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        var threadSafe = attribute.NamedArguments
            .FirstOrDefault(argument => argument.Key == "ThreadSafe").Value.Value as bool? ?? true;
        if (threadSafe)
        {
            return;
        }

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>()
                     .Where(field => !field.IsStatic && !field.IsReadOnly && !field.IsConst))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NonThreadSafeStateRule,
                field.Locations.FirstOrDefault() ?? location,
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                field.Name));
        }
    }

    private static void AnalyzeStaticMutableSingleton(
        CompilationAnalysisContext context,
        INamedTypeSymbol type,
        HashSet<INamedTypeSymbol> registrations)
    {
        ISymbol? candidate = type.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.IsStatic && !field.IsReadOnly && IsSingletonName(field.Name));

        candidate ??= type.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(property => property.IsStatic && property.SetMethod is not null && IsSingletonName(property.Name));

        if (candidate is null)
        {
            return;
        }

        var location = candidate.Locations.FirstOrDefault() ?? type.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            StaticMutableRule,
            location,
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            candidate.Name));

        if (registrations.Contains(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                StaticMutableAndDiRule,
                location,
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static bool HasSingleInstanceInChain(InvocationExpressionSyntax invocation) =>
        invocation.Parent is MemberAccessExpressionSyntax memberAccess &&
        memberAccess.Parent is InvocationExpressionSyntax parent &&
        (memberAccess.Name.Identifier.ValueText == "SingleInstance" || HasSingleInstanceInChain(parent));

    private static bool IsSingletonName(string name) =>
        name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("singleton", StringComparison.OrdinalIgnoreCase) >= 0;
}
