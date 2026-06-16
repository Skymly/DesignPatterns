namespace DesignPatterns.Extensions.Autofac;

/// <summary>
/// Controls Autofac instance lifetime for generated <c>RegisterAutofac</c> registrations.
/// </summary>
public enum InstanceSharing
{
    /// <summary>
    /// One shared instance per registered type (maps to <c>SingleInstance()</c>).
    /// </summary>
    Shared,

    /// <summary>
    /// A new instance per resolve (maps to <c>InstancePerDependency()</c>).
    /// </summary>
    None,
}
