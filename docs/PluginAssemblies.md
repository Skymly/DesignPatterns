# 插件程序集模式 — 卫星程序集 + 生成器注册表

本文档说明如何在**多个程序集**中组织 `[RegisterStrategy]` / `[RegisterFactory]` 实现，使宿主通过 **ProjectReference** 决定编译期可见的 key 集合，而**不**使用 AppDomain 或目录扫描。

与 [Strategy.md](Strategy.md)、[FactoryRegistry.md](FactoryRegistry.md)、[Autofac.md](Autofac.md) 配合阅读。

---

## 1. 目标与约束

| 目标 | 说明 |
|------|------|
| 按需携带 Native / 供应商 SDK | 仅引用的 `Provider.*` 程序集进入编译与部署 |
| 编译期 key 安全 | 使用 `{Contract}Keys` 常量 + DP025 字面量校验 |
| 容器注册一行化 | `RegisterAutofac` / `RegisterDi`（见各扩展文档） |

| 约束 | 说明 |
|------|------|
| **无运行时扫描** | 注册表由 Roslyn 源生成器在**各程序集编译时**产出 |
| **`partial` 不跨程序集** | C# `partial` 类型必须在同一程序集内合并；**不存在**跨 dll 的 partial Registry 合并 |
| **同一契约 + 同一全名类型** | 若两个供应商程序集为**同一契约**生成了**相同命名空间与类型名**的 `{Contract}Registry`，宿主同时引用两者会产生**类型歧义** — 部署时应只引用一个该维度的供应商程序集 |

---

## 2. 推荐目录结构

```
Contracts/                    ← 契约程序集（无 Native 依赖）
├── ICardMotion.cs
├── IFCControl.cs
└── IFCError.cs

Providers.Alpha/              ← 供应商 A（独立程序集）
├── AlphaCard.cs              // [RegisterStrategy<ICardMotion>("alpha")]
└── AlphaModule.cs            // 薄 Autofac Module（可选）

Providers.Beta/                 ← 供应商 B
└── BetaCard.cs

Host/                         ← 应用宿主
├── App.config                // 选型键（若使用配置桥接，见 #107）
├── Program.cs
└── Host.csproj               // ProjectReference → Contracts + 选中的 Provider.*
```

**要点：**

1. **契约**与**实现**分离：契约程序集只含接口与共享 DTO；**不**要求契约程序集声明 `partial *Registry` 空壳（生成器在**含有 `[RegisterStrategy]` 的程序集**内产出完整 `partial` Registry）。
2. 宿主 **ProjectReference** 决定哪些供应商参与编译；未引用的程序集不会贡献 key。
3. 每个供应商可保留独立 **Module**（Autofac）或扩展方法，在调用 `*Registry.RegisterAutofac(builder)` 之外注册 Named/Keyed 服务。

---

## 3. 生成器行为（跨程序集）

对契约 `ICardMotion`（定义在 `Contracts.dll`）：

| 程序集 | 内容 | 生成物 |
|--------|------|--------|
| `Contracts` | 仅 `interface ICardMotion` | **无** Registry（无 `[RegisterStrategy]`） |
| `Providers.Alpha` | `[RegisterStrategy<ICardMotion>("alpha")]` | `CardMotionKeys` + `CardMotionRegistry`（在 **Alpha 程序集**内） |
| `Providers.Beta` | `[RegisterStrategy<ICardMotion>("beta")]` | 同名类型在 **Beta 程序集**内（与 Alpha **不是**同一个 CLR 类型） |

宿主引用 `Contracts` + `Providers.Alpha` 时：

- 使用 `Plugin.Providers.Alpha` 程序集内的 `CardMotionRegistry.Instance` / `RegisterAutofac`。
- `CardMotionRegistry.Keys` 仅含当前程序集编译进来的实现（如 `alpha`），**不含**未引用的 `Providers.Beta` 中的 `beta`。

验证测试：`tests/DesignPatterns.SourceGenerators.Tests/Generators/PluginAssemblyGeneratorTests.cs`。

---

## 4. Autofac 薄模块

```csharp
using Autofac;
using DesignPatterns.Behavioral;
using Plugin.Contracts;

namespace Plugin.Providers.Alpha;

public sealed class AlphaProviderModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        CardMotionRegistry.RegisterAutofac(builder);
        // 供应商特有 Named 服务在此补充，生成器不覆盖
        // builder.RegisterType<...>().Named<IIoControl>("extend-io").SingleInstance();
    }
}
```

宿主：

```csharp
var builder = new ContainerBuilder();
builder.RegisterModule<AlphaProviderModule>();
// 若还有 FC 供应商：builder.RegisterModule<GammaFcModule>();
using var container = builder.Build();
```

---

## 5. 成对类型（Companion types）

同一供应商、**不同契约**、**相同 strategy key** 时，分别标注 `[RegisterStrategy]`，并在模块中注册两个 Registry：

```csharp
[RegisterStrategy<IFCControl>("inovance")]
public sealed class Md200Fc : IFCControl { ... }

[RegisterStrategy<IFCError>("inovance")]
public sealed class Md200Error : IFCError { ... }

// Module:
FCControlRegistry.RegisterAutofac(builder);
FCErrorRegistry.RegisterAutofac(builder);
```

v1 **不**提供 `[RegisterStrategyBundle]`；若样板过多再评估（见 #108）。

同一实现需在 Autofac 中暴露多个接口时，在模块中手动补充 `builder.RegisterType<Impl>().As<IContract>().As<IAdditional>()`；仅当样例摩擦证明有必要时再评估生成器 `AdditionalContracts` 参数。

---

## 6. 配置选型

`App.config` / `ConfigurationManager` 桥接由 `DesignPatterns.Extensions.AppSettings` 提供（`RegistryConfiguration.ResolveConfigured` / `TryResolveConfigured`，见 [AppSettings.md](AppSettings.md)）。

在桥接落地前，宿主可：

- 读取配置字符串，用 `{Contract}Keys` 常量比较；或
- 对 `*Registry.Instance.TryGet(key, out var impl)` 做失败处理。

---

## 7. 诊断与已知限制

| ID | 说明 |
|----|------|
| DP003 | 同一编译单元内重复 key（已有） |
| DP025 | 未知字面量 key（已有） |
| **DP033** | 宿主同时引用多个供应商程序集时，**同一契约**出现**相同 strategy key**（Analyzer Error） |
| **DP034** | 供应商已引用但 key 未出现在样例/配置文档 — 可选 Info，**未实现** |

---

## 8. 非目标

- AppDomain / 插件目录反射加载
- 单程序集内以外的 Registry 物理合并
- 替代宿主 Module 加载顺序

---

## 9. 参考

- [Strategy.md](Strategy.md) — `[RegisterStrategy]` 与 `RegisterDi`
- [Autofac.md](Autofac.md) — `RegisterAutofac`
- [FactoryKeyConventions.md](FactoryKeyConventions.md) — key 命名
- 测试：`PluginAssemblyGeneratorTests`
