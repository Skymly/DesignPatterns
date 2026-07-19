# Configuration bridge — IConfiguration to strategy registries

`DesignPatterns.Extensions.Configuration` maps **`IConfiguration`** strings to **`IStrategyRegistry<string, TContract>`** lookups, replacing hand-written `switch` blocks when selecting a provider by configuration key.

Works with generated `{Contract}Registry.Instance` / `{Contract}Keys` from `[RegisterStrategy]` (see [Strategy.md](Strategy.md)).

Related: [PluginAssemblies.md](PluginAssemblies.md) (multi-assembly hosts), [Autofac.md](Autofac.md) (container registration).

---

## Package

Independent extension package `Skymly.DesignPatterns.Extensions.Configuration` (not included in the `Skymly.DesignPatterns` meta-package), same policy as MSDI and Autofac extensions.

```xml
<PackageReference Include="Skymly.DesignPatterns.Extensions.Configuration" Version="..." />
```

Or sibling project reference:

```xml
<ProjectReference Include="path/to/DesignPatterns.Extensions.Configuration/DesignPatterns.Extensions.Configuration.csproj" />
```

Targets: `netstandard2.0` and `net8.0`. Depends on `Microsoft.Extensions.Configuration.Abstractions`.

---

## API

```csharp
using DesignPatterns.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

// e.g. provided by your host (IConfigurationRoot / IConfiguration).
IConfiguration configuration = ...;

// Throws RegistryConfigurationException when the configured key cannot be resolved.
var card = RegistryConfiguration.ResolveConfigured(
    CardMotionRegistry.Instance,
    configuration: configuration,
    configurationKey: "Card",
    defaultKey: CardMotionKeys.Alpha);

// Non-throwing variant.
if (RegistryConfiguration.TryResolveConfigured(
        CardMotionRegistry.Instance,
        configuration,
        "Card",
        out var motion,
        defaultKey: CardMotionKeys.Alpha))
{
    // use motion
}
```

### Resolution order

1. Read `IConfiguration[configurationKey]`.
2. When the value is missing or whitespace, use `defaultKey` when provided.
3. Call `registry.TryGet(strategyKey, out implementation)`.

### Failure messages

`RegistryConfigurationException` includes:

- the **configuration key name**
- the **configured value** (or default key when config is empty)
- the registry **`Keys`** list

Example:

```text
Configuration key 'Card' has value 'beta' which is not registered. Registered keys: alpha, gamma.
```

---

## Host example

```csharp
using DesignPatterns.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var card = RegistryConfiguration.ResolveConfigured(
    CardMotionRegistry.Instance,
    configuration,
    "Card",
    defaultKey: CardMotionKeys.Alpha);
```

Prefer `{Contract}Keys` constants for `defaultKey` to stay DP025-safe at call sites.

### Legacy App.config hosts

There is no dedicated `ConfigurationManager.AppSettings` extension. Map AppSettings into `IConfiguration` in the host (indexer is enough for `RegistryConfiguration`), then call the same API. See the PluginAssemblies sample host for a minimal adapter.
---

## Tests

| Project | TFM | Role |
|---------|-----|------|
| `tests/DesignPatterns.Extensions.Configuration.Tests` | net8.0 | Unit tests (valid key, missing entry, unknown key, default fallback) |
| `tests/DesignPatterns.Extensions.Configuration.Tests.Net48` | net48 | Same tests linked; compiled on CI, runnable on Windows with `dotnet test` |

---

## Non-goals

- MSBuild / App.config static analysis (**DP032** — future)
- Factory registry bridge (strategy-only v1)
- Replacing composition-root module order
- Bundling `System.Configuration.ConfigurationManager` into this package
