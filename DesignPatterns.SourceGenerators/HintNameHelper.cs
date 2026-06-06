using System;
using System.Text;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators;

internal static class HintNameHelper
{
    internal static string FromSymbol(INamedTypeSymbol symbol)
    {
        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        const string globalPrefix = "global::";
        if (displayName.StartsWith(globalPrefix, StringComparison.Ordinal))
        {
            displayName = displayName.Substring(globalPrefix.Length);
        }

        var builder = new StringBuilder(displayName.Length);
        foreach (var character in displayName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }
}
