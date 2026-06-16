# AppSettings bridge — ConfigurationManager to strategy registries

`DesignPatterns.Extensions.AppSettings` maps **`ConfigurationManager.AppSettings`** strings to **`IStrategyRegistry<string, TContract>`** lookups, replacing hand-written `switch` blocks when selecting a provider by `App.config` key.

Works with generated `{Contract}Registry.Instance` / `{Contract}Keys` from `[RegisterStrategy]` (see [Strategy.md](Strategy.md)).

Related: [Configuration.md](Configuration.md) (`IConfiguration` bridge), [PluginAssemblies.md](PluginAssemblies.md) (multi-assembly hosts), [Autofac.md](Autofac.md) (container registration).

---

## Package

Independent extension assembly (not included in the `Skymly.DesignPatterns` meta-package), same policy as MSDI and Autofac extensions.

```xml
<ProjectReference Include="path/to/DesignPatterns.Extensions.AppSettings/DesignPatterns.Extensions.AppSettings.csproj" />
```

Targets: `netstandard2.0` and `net8.0`. Depends on `System.Configuration.ConfigurationManager`.

---

## API

```csharp
using DesignPatterns.Extensions.AppSettings;

// Throws RegistryConfigurationException when the configured key cannot be resolved.
var card = RegistryConfiguration.ResolveConfigured(
    CardMotionRegistry.Instance,
    appSettingsKey: "Card",
    defaultKey: CardMotionKeys.Alpha);

// Non-throwing variant.
if (RegistryConfiguration.TryResolveConfigured(
        CardMotionRegistry.Instance,
        "Card",
        out var motion,
        defaultKey: CardMotionKeys.Alpha))
{
    // use motion
}
```

### Resolution order

1. Read `ConfigurationManager.AppSettings[appSettingsKey]`.
2. When the value is missing or whitespace, use `defaultKey` when provided.
3. Call `registry.TryGet(strategyKey, out implementation)`.

### Failure messages

`RegistryConfigurationException` includes:

- the **AppSettings key name**
- the **configured value** (or default key when config is empty)
- the registry **`Keys`** list

Example:

```text
AppSettings key 'Card' has value 'beta' which is not registered. Registered keys: alpha, gamma.
```

---

## Host example (plugin assemblies)

After Autofac modules register provider registries ([PluginAssemblies.md](PluginAssemblies.md)):

```xml
<!-- App.config -->
<appSettings>
  <add key="Card" value="alpha" />
  <add key="FC" value="gamma" />
</appSettings>
```

```csharp
using Autofac;
using DesignPatterns.Extensions.AppSettings;
using Plugin.Contracts;
using Plugin.Providers.Alpha;
using Plugin.Providers.Gamma;

var builder = new ContainerBuilder();
builder.RegisterModule<AlphaProviderModule>();
builder.RegisterModule<GammaProviderModule>();
using var container = builder.Build();

var card = RegistryConfiguration.ResolveConfigured(
    CardMotionRegistry.Create(container),
    "Card");

var fc = RegistryConfiguration.ResolveConfigured(
    FCControlRegistry.Create(container),
    "FC");
```

Prefer `{Contract}Keys` constants for `defaultKey` to stay DP025-safe at call sites.

---

## Tests

| Project | TFM | Role |
|---------|-----|------|
| `tests/DesignPatterns.Extensions.AppSettings.Tests` | net8.0 | Unit tests (valid key, missing entry, unknown key, default fallback) |
| `tests/DesignPatterns.Extensions.AppSettings.Tests.Net48` | net48 | Same tests linked; compiled on CI, runnable on Windows with `dotnet test` |

Unit tests inject AppSettings via an internal test seam; production code reads `ConfigurationManager.AppSettings` from the host `App.config`.

---

## Non-goals

- MSBuild / App.config static analysis (**DP032** — future)
- Factory registry bridge (strategy-only v1)
- Replacing composition-root module order
