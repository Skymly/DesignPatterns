# Design Doc: Factory Registry

> **关联 Spec**：[docs/spec/FactoryRegistry.md](../spec/FactoryRegistry.md)
> **关联 RFC**：[docs/rfc/XXX.md](../rfc/XXX.md)（如有）
> **关联 ADR**：ADR-XXX（如有）

## 概述

Factory Registry 按 key 解析**工厂实现**，每次 `Create` 调用对应 factory 委托得到新产品实例。本库提供：

- **运行时**：`IFactoryRegistry`、`FactoryRegistryBuilder` 手动注册
- **编译期**：`[RegisterFactory]` / `[RegisterFactory<TContract>]` 源生成器 → 强类型 Keys + 静态 Registry
- **Analyzer**：DP023 提示未标记的工厂实现（与 Strategy DP006 对称）

## 设计目标

1. 最小 API：`TryCreate` / `Create` / `Keys`
2. 显式失败：`FactoryNotFoundException` 带 key 信息
3. 不侵入 Core 的 DI 依赖；生命周期由 factory 委托或 DI 扩展包负责

## 实现概览

### 运行时

**`FactoryRegistry<TKey, TProduct>`**：不可变实现，内部持有 `IReadOnlyDictionary<TKey, Func<TProduct>>`。net8.0+ 上构造时将字典冻结为 `FrozenDictionary` 以加速查找。`Create(key)` 通过 `TryCreate` 解析 factory 委托并执行，未命中时抛 `FactoryNotFoundException.ForKey(key)`。

**`FactoryRegistryBuilder<TKey, TProduct>`**：手动注册入口，支持两种 factory 委托形式：

- `Register(key, Func<TProduct>)`：每次 `Create(key)` 调用该 factory
- `Register(key, Func<TKey, TProduct>)`：factory 接收 key（内部包装为 `() => factory(key)`）

重复 key 在 `Register` 时抛 `ArgumentException`（**运行时**检测）。

```csharp
using DesignPatterns.Creational;

public interface IProduct
{
    string Name { get; }
}

var registry = new FactoryRegistryBuilder<string, IProduct>()
    .Register("standard", () => new StandardProduct())
    .Register("premium", () => new PremiumProduct())
    .Build();

var product = registry.Create("standard");
if (!registry.TryCreate("unknown", out _))
{
    // key 不存在
}
```

**异步适配**：`FactoryRegistryAsyncExtensions.AsAsync()` 将同步 `IFactoryRegistry` 包装为 `IAsyncFactoryRegistry`，`CreateAsync` 内部调用同步 `Create` 并包装为 `ValueTask`；`cancellationToken` 对同步 factory 被忽略。

### 源生成器

**`RegisterFactoryGenerator`**：增量源生成器，扫描标注 `[RegisterFactory]` / `[RegisterFactory<TContract>]` 的类，按契约分组。

**扫描阶段**：`ForAttributeWithMetadataName` 收集所有标注类，提取 `Key`、`Contract`（泛型版本从类型参数推断，非泛型版本从 `Contract` 属性读取）、`IsAsync`、`PoolSize`。

**Keys 生成**：对每个契约 `TContract` 生成 `{Contract}FactoryKeys`（命名规则与 Strategy 相同：去掉前缀 `I` 等），包含 `public const string` 常量。

```csharp
// ProductFactoryKeys.g.cs
public static partial class ProductFactoryKeys
{
    public const string Standard = "standard";
    public const string Premium = "premium";
}
```

**Registry 生成**：对每个契约生成 `{Contract}FactoryRegistry`，暴露 `Create()` 返回 `IFactoryRegistry<string, TContract>`。无 DI 时使用 `() => new Implementation()` 注册工厂委托；引用 DI 扩展包后额外生成 `Create(IServiceProvider)` 与 `RegisterDi(services)`。

```csharp
// ProductFactoryRegistry.g.cs
public static partial class ProductFactoryRegistry
{
    public static IFactoryRegistry<string, IProductFactory> Create() { ... }
}
```

`Create()` 返回的注册表在每次 `Create(key)` 时**新建**产品实例（与 Strategy 注册表返回单例实例不同）。

**异步路径**：当 `IsAsync=true` 或实现类实现 `IAsyncFactory<TProduct>` 时，生成器额外输出 `IAsyncFactoryRegistry` 与对应的 `CreateAsync` / `TryCreateAsync` 路径。

**池化路径**：当 `PoolSize > 0` 且工厂为异步时，生成器输出 `IPooledFactoryRegistry`，按 key 维护对象池。

### 诊断

| ID | 严重性 | 来源 | 触发条件 |
|----|--------|------|----------|
| DP020 | Error | 源生成器 | 同一 `TContract` 下 key 重复 |
| DP021 | Error | 源生成器 | 标记的类未实现指定的 `TContract` |
| DP022 | Error | 源生成器 | 标记的类缺少 public 无参构造 |
| DP023 | Info | Analyzer | 实现了某工厂契约但未加 `[RegisterFactory]` |
| DP053 | Error | 源生成器 | `IsAsync=true` 但未实现 `IAsyncFactory<TProduct>` |
| DP054 | Error | 源生成器 | `PoolSize` 为负数 |
| DP055 | Warning | 源生成器 | `PoolSize` 过大（可能导致内存过高） |

DP023 属 **Analyzer**；DP020 / DP021 / DP022 / DP053 / DP054 / DP055 属**生成器**。

CodeFix：DP023 可一键添加 `[RegisterFactory("suggested-key", typeof(TContract))]`（与 DP006 对称）。

## 设计权衡

### Factory vs Strategy：实例 vs 创建新实例

Strategy Registry 注册的是**实例**（`key → TStrategy`），`Get(key)` 返回同一实例；Factory Registry 注册的是**工厂委托**（`key → Func<TProduct>`），`Create(key)` 每次调用返回新产品实例。二者语义不同：Strategy 用于「选一个算法执行」，Factory 用于「按需创建对象」。

### `IReadOnlyRegistry` 共享抽象

`IFactoryRegistry` 继承 `IReadOnlyRegistry<TKey, TProduct>`，与 `IStrategyRegistry` 共享该抽象（提供 `Keys` 和 `TryGet`）。Factory 的 `TryCreate` / `Create` 语义（执行委托产生新实例）在 `IReadOnlyRegistry` 之外扩展。

### DI 默认 Transient 而非 Singleton

工厂模式的语义是每次 `Create` / `CreateAsync` 返回新产品实例，因此 `RegisterDi` 的 `implementationLifetime` 默认值为 `Transient`。这与 Strategy / Chain 等模式默认 `Singleton` 不同。

```csharp
// 默认：Transient（每次 Create 返回新产品）
ProductFactoryRegistry.RegisterDi(services);

// 显式 Singleton（多次 Create 返回同一实现实例）
ProductFactoryRegistry.RegisterDi(services, implementationLifetime: ServiceLifetime.Singleton);
```

> **注意**：此默认值与 Strategy/Chain 等模式不同（后者默认 Singleton），因为工厂的语义是"创建新实例"而非"解析同一实例"。EventAggregator 也使用 Transient 默认值（handler 通常无状态）。

## 与生态的边界

### DI 集成

引用 `DesignPatterns.Extensions.DependencyInjection` 后，生成器为 `{Contract}FactoryRegistry` 增加：

```csharp
ProductFactoryRegistry.RegisterDi(services); // TryAdd 各实现 + IFactoryRegistry<,>
// 或
services.AddFactoryRegistry<string, IProduct>(builder => { ... }); // 手动 Builder
```

`Create(IServiceProvider sp)` 使用 `() => sp.GetRequiredService<TImpl>()` 注册工厂委托；**每次** `registry.Create(key)` 调用委托。

无 DI 包时仍可使用静态 `Create()`（`new Impl()`）。

### 与 Strategy Registry 的对比

| | Strategy Registry | Factory Registry |
|---|---|---|
| 注册内容 | key → **实例** | key → **每次 Create 新建** |
| 编译期 | `[RegisterStrategy]` | `[RegisterFactory]` |
| 共享抽象 | `IStrategyRegistry` → `IReadOnlyRegistry` | `IFactoryRegistry` → `IReadOnlyRegistry` |
| 未找到 | `StrategyNotFoundException` | `FactoryNotFoundException` |
| 未注册实现 | DP006 (Info) | DP023 (Info) |
| DI 默认 lifetime | Singleton | Transient |

详见 [Strategy.md](../spec/Strategy.md)。

## 已知局限

- **不做抽象工厂族**：不提供多个产品类型族（Abstract Factory）的完整框架
- **v1 无 async factory 完整框架**：异步工厂（`IAsyncFactory`）与池化（`IPooledFactoryRegistry`）在 v1 中为初步支持，后续版本完善
- **无 DI 自动注册（Core）**：Core 不引用 MSDI，需引用 `DesignPatterns.Extensions.DependencyInjection` 并调用 `RegisterDi`，或使用手动 Builder
- **不用反射扫描程序集**：工厂注册通过 `[RegisterFactory]` 源生成器在编译期完成

## 参考

- GoF: Factory Method / 注册表变体（Creational）
- [Strategy.md](../spec/Strategy.md) — 策略注册表对比
- [AGENTS.md](../../AGENTS.md) — 项目规则与里程碑
