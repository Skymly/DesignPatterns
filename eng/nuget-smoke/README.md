# NuGet consumer smoke tests

Verifies that packed **`Skymly.DesignPatterns`** restores, runs source generators, and compiles in real consumer projects.

| Project | TFM | What it proves |
|---------|-----|----------------|
| [`MetaPackage.Consumer/`](MetaPackage.Consumer/) | `net8.0` | Strategy registry, singleton generator; `dotnet run` after local or published pack |
| [`MetaPackage.Consumer.Net48/`](MetaPackage.Consumer.Net48/) | `net48` | Same meta-package on .NET Framework; `RegisterStrategy` + generated `*Keys` / `*Registry` (`CiPack` build-only; run the `.exe` manually on Windows) |

## Local pack feed

`CiPack` writes [`nuget.config.local`](nuget.config.local) pointing at `artifacts/package/` and restores consumers against the just-built package version.

```powershell
./build.ps1 --target CiPack --configuration Release
```

Published feed smoke:

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmokePublished --consumer-feed Published
```
