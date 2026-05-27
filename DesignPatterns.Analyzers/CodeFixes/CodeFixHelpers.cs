using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.CodeFixes;

internal static class CodeFixHelpers
{
    internal static bool TryGetClassDeclaration(
        SyntaxNode root,
        Diagnostic diagnostic,
        out ClassDeclarationSyntax? classDeclaration)
    {
        classDeclaration = root.FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        return classDeclaration is not null;
    }

    internal static bool TryGetContractTypeName(Diagnostic diagnostic, out string? contractTypeName)
    {
        contractTypeName = null;
        var matches = Regex.Matches(diagnostic.GetMessage(), "'([^']+)'", RegexOptions.CultureInvariant);
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
}
