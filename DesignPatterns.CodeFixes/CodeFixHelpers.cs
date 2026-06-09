using System;
using System.Linq;
using System.Text.RegularExpressions;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.CodeFixes;

internal static class CodeFixHelpers
{
    internal static bool TryGetClassDeclaration(
        SyntaxNode root,
        Diagnostic diagnostic,
        out ClassDeclarationSyntax? classDeclaration)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        classDeclaration = node?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        return classDeclaration is not null;
    }

    internal static bool TryGetContractTypeName(Diagnostic diagnostic, out string? contractTypeName)
    {
        contractTypeName = null;
        var message = diagnostic.GetMessage();

        if (diagnostic.Id == DiagnosticIds.HandlerOrderContractMismatch)
        {
            var handlerMatch = Regex.Match(message, @"IHandler<([^>]+)>", RegexOptions.CultureInvariant);
            if (handlerMatch.Success)
            {
                contractTypeName = $"IHandler<{handlerMatch.Groups[1].Value}>";
                return true;
            }

            return false;
        }

        if (diagnostic.Id == DiagnosticIds.HandlerOrderUnregisteredImplementation)
        {
            var handlerMatch = Regex.Match(message, @"IHandler<([^>]+)>", RegexOptions.CultureInvariant);
            if (handlerMatch.Success)
            {
                contractTypeName = handlerMatch.Groups[1].Value;
                return true;
            }

            return false;
        }

        if (diagnostic.Id == DiagnosticIds.CompositePartMissingBuildable)
        {
            var buildableMatch = Regex.Match(message, @"ICompositeBuildable<([^>]+)>", RegexOptions.CultureInvariant);
            if (buildableMatch.Success)
            {
                contractTypeName = buildableMatch.Groups[1].Value;
                return true;
            }

            return false;
        }

        if (diagnostic.Id == DiagnosticIds.DecoratorMissingDecoratorInterface)
        {
            var decoratorMatch = Regex.Match(message, @"IDecorator<([^>]+)>", RegexOptions.CultureInvariant);
            if (decoratorMatch.Success)
            {
                contractTypeName = $"IDecorator<{decoratorMatch.Groups[1].Value}>";
                return true;
            }

            return false;
        }

        var matches = Regex.Matches(message, "'([^']+)'", RegexOptions.CultureInvariant);
        if (matches.Count < 2)
        {
            return false;
        }

        contractTypeName = matches[1].Groups[1].Value;
        return !string.IsNullOrWhiteSpace(contractTypeName);
    }

    internal static string ToSnakeCaseKey(string className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return "key";
        }

        var buffer = new char[className.Length * 2];
        var index = 0;

        for (var i = 0; i < className.Length; i++)
        {
            var character = className[i];
            if (char.IsUpper(character) && i > 0)
            {
                buffer[index++] = '-';
            }

            buffer[index++] = char.ToLowerInvariant(character);
        }

        return new string(buffer, 0, index);
    }

    internal const string SuggestedRegistryKeyPropertyName = "SuggestedRegistryKey";

    internal static bool TryGetSuggestedRegistryKey(Diagnostic diagnostic, out string? suggestedKey)
    {
        suggestedKey = null;
        if (diagnostic.Id != DiagnosticIds.RegistryKeyNotRegistered)
        {
            return false;
        }

        return diagnostic.Properties.TryGetValue(SuggestedRegistryKeyPropertyName, out suggestedKey) &&
               !string.IsNullOrWhiteSpace(suggestedKey);
    }
}
