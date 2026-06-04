using DesignPatterns.Creational;
using RegisterFactory.Sample;

// The source generator creates:
// - ProductFactoryKeys (constants for each registered key)
// - ProductFactoryRegistry (static Create() and, with DI package, RegisterDi + Create(IServiceProvider))

var registry = ProductFactoryRegistry.Create();

var standardFactory = registry.Create(ProductFactoryKeys.Standard);
var premiumFactory = registry.Create(ProductFactoryKeys.Premium);

var standard = standardFactory.Create();
var premium = premiumFactory.Create();

Console.WriteLine($"Created: {standard.Name}");
Console.WriteLine($"Created: {premium.Name}");

var standardFactoryAgain = registry.Create(ProductFactoryKeys.Standard);
Console.WriteLine($"New factory each call? {!ReferenceEquals(standardFactory, standardFactoryAgain)}");

if (!registry.TryCreate("unknown", out _))
{
    Console.WriteLine("Unknown key not found (expected).");
}

try
{
    registry.Create("missing");
}
catch (FactoryNotFoundException ex)
{
    Console.WriteLine($"Create missing key: {ex.Message}");
}
