using System;
using System.Collections.Specialized;
using DesignPatterns.Extensions.AppSettings;

namespace DesignPatterns.Extensions.AppSettings.Tests;

public sealed class AppSettingsFixture : IDisposable
{
    private readonly Func<string, string?> _previous;

    public AppSettingsFixture()
    {
        _previous = RegistryConfigurationAppSettings.GetValue;

        var settings = new NameValueCollection
        {
            ["ValidProvider"] = "alpha",
            ["UnknownProvider"] = "not-registered",
            ["EmptyProvider"] = string.Empty,
        };

        RegistryConfigurationAppSettings.GetValue = key => settings[key];
    }

    public void Dispose()
    {
        RegistryConfigurationAppSettings.GetValue = _previous;
    }
}
