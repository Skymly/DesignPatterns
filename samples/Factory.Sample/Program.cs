using DesignPatterns.Creational;
using Factory.Sample;

var registry = new FactoryRegistryBuilder<string, IProduct>()
    .Register("standard", () => new StandardProduct())
    .Register("premium", () => new PremiumProduct())
    .Build();

var standard = registry.Create("standard");
var premium = registry.Create("premium");

Console.WriteLine($"Created: {standard.Name}");
Console.WriteLine($"Created: {premium.Name}");

var standardAgain = registry.Create("standard");
Console.WriteLine($"Same instance? {ReferenceEquals(standard, standardAgain)}");

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
