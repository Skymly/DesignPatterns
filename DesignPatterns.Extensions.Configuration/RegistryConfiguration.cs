using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.Extensions.Configuration;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Configuration;

/// <summary>
/// Bridges configuration string values (<see cref="ConfigurationManager.AppSettings"/> or <see cref="IConfiguration"/>)
/// to <see cref="IStrategyRegistry{TKey,TStrategy}"/> lookups.
/// </summary>
public static class RegistryConfiguration
{
    /// <summary>
    /// Reads an AppSettings value and resolves the matching strategy implementation.
    /// </summary>
    /// <typeparam name="TContract">Strategy contract type.</typeparam>
    /// <param name="registry">Registry to query.</param>
    /// <param name="appSettingsKey">AppSettings key name.</param>
    /// <param name="defaultKey">Fallback registry key when the AppSettings entry is missing or whitespace.</param>
    /// <returns>The resolved strategy implementation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="appSettingsKey"/> is null or empty.</exception>
    /// <exception cref="RegistryConfigurationException">The configured or default key cannot be resolved.</exception>
    public static TContract ResolveConfigured<TContract>(
        IStrategyRegistry<string, TContract> registry,
        string appSettingsKey,
        string? defaultKey = null)
    {
        if (TryResolveConfigured(registry, appSettingsKey, out var implementation, defaultKey))
        {
            return implementation!;
        }

        throw CreateException(registry, appSettingsKey, defaultKey);
    }

    /// <summary>
    /// Reads an AppSettings value and resolves the matching strategy implementation when possible.
    /// </summary>
    /// <typeparam name="TContract">Strategy contract type.</typeparam>
    /// <param name="registry">Registry to query.</param>
    /// <param name="appSettingsKey">AppSettings key name.</param>
    /// <param name="implementation">Resolved implementation when this method returns <see langword="true"/>.</param>
    /// <param name="defaultKey">Fallback registry key when the AppSettings entry is missing or whitespace.</param>
    /// <returns><see langword="true"/> when a strategy was resolved; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="appSettingsKey"/> is null or empty.</exception>
    public static bool TryResolveConfigured<TContract>(
        IStrategyRegistry<string, TContract> registry,
        string appSettingsKey,
        out TContract? implementation,
        string? defaultKey = null)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (string.IsNullOrEmpty(appSettingsKey))
        {
            throw new ArgumentException("AppSettings key must not be null or empty.", nameof(appSettingsKey));
        }

        var configuredValue = RegistryConfigurationAppSettings.GetValue(appSettingsKey);
        var strategyKey = string.IsNullOrWhiteSpace(configuredValue) ? defaultKey : configuredValue;

        if (string.IsNullOrWhiteSpace(strategyKey))
        {
            implementation = default;
            return false;
        }

        var resolvedKey = strategyKey!;
        if (registry.TryGet(resolvedKey, out implementation))
        {
            return true;
        }

        implementation = default;
        return false;
    }

    private static RegistryConfigurationException CreateException<TContract>(
        IStrategyRegistry<string, TContract> registry,
        string appSettingsKey,
        string? defaultKey)
    {
        var configuredValue = RegistryConfigurationAppSettings.GetValue(appSettingsKey);
        var registeredKeys = FormatKeys(registry.Keys);

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            if (string.IsNullOrWhiteSpace(defaultKey))
            {
                return new RegistryConfigurationException(
                    $"AppSettings key '{appSettingsKey}' is missing or empty. Registered keys: {registeredKeys}.");
            }

            return new RegistryConfigurationException(
                $"AppSettings key '{appSettingsKey}' is missing or empty; default key '{defaultKey}' is not registered. Registered keys: {registeredKeys}.");
        }

        return new RegistryConfigurationException(
            $"AppSettings key '{appSettingsKey}' has value '{configuredValue}' which is not registered. Registered keys: {registeredKeys}.");
    }

    /// <summary>
    /// Reads a <see cref="IConfiguration"/> value and resolves the matching strategy implementation.
    /// </summary>
    public static TContract ResolveConfigured<TContract>(
        IStrategyRegistry<string, TContract> registry,
        IConfiguration configuration,
        string configurationKey,
        string? defaultKey = null)
    {
        if (TryResolveConfigured(registry, configuration, configurationKey, out var implementation, defaultKey))
        {
            return implementation!;
        }

        throw CreateException(registry, configuration, configurationKey, defaultKey);
    }

    /// <summary>
    /// Reads a <see cref="IConfiguration"/> value and resolves the matching strategy implementation when possible.
    /// </summary>
    public static bool TryResolveConfigured<TContract>(
        IStrategyRegistry<string, TContract> registry,
        IConfiguration configuration,
        string configurationKey,
        out TContract? implementation,
        string? defaultKey = null)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (string.IsNullOrEmpty(configurationKey))
        {
            throw new ArgumentException("Configuration key must not be null or empty.", nameof(configurationKey));
        }

        var configuredValue = configuration[configurationKey];
        var strategyKey = string.IsNullOrWhiteSpace(configuredValue) ? defaultKey : configuredValue;

        if (string.IsNullOrWhiteSpace(strategyKey))
        {
            implementation = default;
            return false;
        }

        var resolvedKey = strategyKey!;
        if (registry.TryGet(resolvedKey, out implementation))
        {
            return true;
        }

        implementation = default;
        return false;
    }

    private static RegistryConfigurationException CreateException<TContract>(
        IStrategyRegistry<string, TContract> registry,
        IConfiguration configuration,
        string configurationKey,
        string? defaultKey)
    {
        var configuredValue = configuration[configurationKey];
        var registeredKeys = FormatKeys(registry.Keys);

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            if (string.IsNullOrWhiteSpace(defaultKey))
            {
                return new RegistryConfigurationException(
                    $"Configuration key '{configurationKey}' is missing or empty. Registered keys: {registeredKeys}.");
            }

            return new RegistryConfigurationException(
                $"Configuration key '{configurationKey}' is missing or empty; default key '{defaultKey}' is not registered. Registered keys: {registeredKeys}.");
        }

        return new RegistryConfigurationException(
            $"Configuration key '{configurationKey}' has value '{configuredValue}' which is not registered. Registered keys: {registeredKeys}.");
    }

    private static string FormatKeys(IReadOnlyCollection<string> keys) =>
        keys.Count == 0
            ? "(none)"
            : string.Join(", ", keys.OrderBy(key => key, StringComparer.Ordinal));
}
