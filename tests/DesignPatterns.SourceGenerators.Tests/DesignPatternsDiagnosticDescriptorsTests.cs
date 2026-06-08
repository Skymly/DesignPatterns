using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests;

public sealed class DesignPatternsDiagnosticDescriptorsTests
{
    [Fact]
    public void Generator_descriptor_exposes_help_link_and_actionable_message()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            public interface IPaymentStrategy { }

            [RegisterStrategy("a", typeof(IPaymentStrategy))]
            public sealed class A : IPaymentStrategy { }

            [RegisterStrategy("a", typeof(IPaymentStrategy))]
            public sealed class B : IPaymentStrategy { }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterStrategyGenerator>(("Test.cs", source));
        var diagnostic = runResult.Results.SelectMany(r => r.Diagnostics).First(d => d.Id == DiagnosticIds.RegisterStrategyDuplicateKey);

        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Descriptor.Description.ToString()));
        Assert.Equal(DiagnosticHelpLinks.For(diagnostic.Id), diagnostic.Descriptor.HelpLinkUri);
        Assert.Contains("duplicate [RegisterStrategy]", diagnostic.GetMessage(), StringComparison.Ordinal);
    }
}
