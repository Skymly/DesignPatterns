namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// Categories of DesignPatterns registration attributes.
/// Used to match attributed types to the correct <c>RegisterDi</c> call
/// based on the holder type name pattern.
/// </summary>
internal enum RegistrationCategory
{
    Strategy,
    Factory,
    EventHandler,
    Decorator,
    Composite,
}
