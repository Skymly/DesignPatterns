namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// DI container lifetime aligned with common meaning (Singleton / Scoped / Transient).
/// Numeric values match <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c>
/// so constant folding of that enum can be mapped directly.
/// </summary>
internal enum Lifetime
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2,
}
