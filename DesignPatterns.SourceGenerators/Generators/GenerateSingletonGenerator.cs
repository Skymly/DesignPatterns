using System;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates <c>Lazy&lt;T&gt;</c>-backed singleton members for types marked with <c>[GenerateSingleton]</c>.
/// </summary>
[Generator]
public sealed class GenerateSingletonGenerator : IIncrementalGenerator
{
    /// <summary>Full metadata name of <c>GenerateSingletonAttribute</c>.</summary>
    public const string AttributeMetadataName = "DesignPatterns.Creational.GenerateSingletonAttribute";

    private static readonly DiagnosticDescriptor NotPartialDescriptor =
        DesignPatternsDiagnosticDescriptors.GenerateSingletonNotPartial;

    private static readonly DiagnosticDescriptor InvalidTargetDescriptor =
        DesignPatternsDiagnosticDescriptors.GenerateSingletonInvalidTarget;

    private static readonly DiagnosticDescriptor InvalidInitializeAsyncDescriptor =
        DesignPatternsDiagnosticDescriptors.GenerateSingletonInitializeAsyncInvalid;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeMetadataName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, _) => GetTargetInfo(ctx))
                .WithTrackingName(TrackingNames.SingletonTransform),
            static (spc, result) => Execute(spc, result));
    }

    private static Result<SingletonTargetInfo> GetTargetInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return Result<SingletonTargetInfo>.Empty;
        }

        var symbol = context.TargetSymbol as INamedTypeSymbol;
        if (symbol is null || symbol.TypeKind == TypeKind.Error)
        {
            return Result<SingletonTargetInfo>.Empty;
        }

        var threadSafe = true;
        string? initializeAsync = null;
        foreach (var attribute in context.Attributes)
        {
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "ThreadSafe" && named.Value.Value is bool value)
                {
                    threadSafe = value;
                }
                else if (named.Key == "InitializeAsync" && named.Value.Value is string methodName)
                {
                    initializeAsync = methodName;
                }
            }
        }

        var location = new LocationInfo(classDeclaration.GetLocation());
        var className = symbol.Name;
        var isStatic = symbol.IsStatic;
        var typeKind = symbol.TypeKind;
        var isPartial = classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        if (typeKind == TypeKind.Struct || isStatic)
        {
            return Result<SingletonTargetInfo>.Failure(
                new DiagnosticInfo(InvalidTargetDescriptor, location, className));
        }

        if (!isPartial)
        {
            return Result<SingletonTargetInfo>.Failure(
                new DiagnosticInfo(NotPartialDescriptor, location, className));
        }

        if (initializeAsync is not null &&
            (string.IsNullOrWhiteSpace(initializeAsync) ||
             !TryValidateInitializeAsync(symbol, initializeAsync)))
        {
            return Result<SingletonTargetInfo>.Failure(
                new DiagnosticInfo(InvalidInitializeAsyncDescriptor, location, initializeAsync, className));
        }

        return Result<SingletonTargetInfo>.Success(new SingletonTargetInfo(
            location,
            className,
            symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString(),
            threadSafe,
            isStatic,
            typeKind,
            isPartial,
            initializeAsync));
    }

    private static void Execute(SourceProductionContext context, Result<SingletonTargetInfo> result)
    {
        if (!ResultExtensions.TryReportAndUnwrap(context, result, out var info))
        {
            return;
        }

        var compilationUnit = SingletonSyntaxFactory.CreateCompilationUnit(
            info.NamespaceName,
            info.ClassName,
            info.ThreadSafe,
            info.InitializeAsync);

        var sourceText = SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8);
        context.AddSource($"{info.ClassName}.Singleton.g.cs", sourceText);
    }

    private sealed record SingletonTargetInfo(
        LocationInfo Location,
        string ClassName,
        string? NamespaceName,
        bool ThreadSafe,
        bool IsStatic,
        TypeKind TypeKind,
        bool IsPartial,
        string? InitializeAsync);

    private static bool TryValidateInitializeAsync(
        INamedTypeSymbol type,
        string methodName)
    {
        foreach (var method in type.GetMembers(methodName).OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || method.Parameters.Length != 2 ||
                !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type) ||
                method.Parameters[1].Type.ToDisplayString() != "System.Threading.CancellationToken")
            {
                continue;
            }

            var returnType = method.ReturnType.ToDisplayString();
            if (returnType == "System.Threading.Tasks.Task")
            {
                return true;
            }

            if (returnType == "System.Threading.Tasks.ValueTask")
            {
                return true;
            }
        }

        return false;
    }
}
