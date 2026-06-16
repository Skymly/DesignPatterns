using System;
using System.Configuration;

namespace DesignPatterns.Extensions.AppSettings;

internal static class RegistryConfigurationAppSettings
{
    internal static Func<string, string?> GetValue { get; set; } = key => ConfigurationManager.AppSettings[key];
}
