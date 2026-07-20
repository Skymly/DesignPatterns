# Design Doc: Factory Registry

> **版本**：v0.2.3-preview2（与 NuGet 包版本对齐）
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

## API 面

### 运行时接口

命名空间：`DesignPatterns.Creational`

#### 注册表接口

```csharp
namespace DesignPatterns.Creational;

/// <summary>
/// 按 key 解析工厂实现的只读注册表。每次 Create 调用对应 factory 委托得到新产品实例。
/// </summary>
public interface IFactoryRegistry<TKey, TProduct> : IReadOnlyRegistry<TKey, TProduct>
    where TKey : notnull
{
    bool TryCreate(TKey key, [MaybeNullWhen(false)] out TProduct product);
    TProduct Create(TKey key); // 找不到抛 FactoryNotFoundException
    IReadOnlyCollection<TKey> Keys { get; }
}
```

> **注意**：`IFactoryRegistry` 继承 `IReadOnlyRegistry<TKey, TProduct>`，与 `IStrategyRegistry` 共享该抽象。

#### 不可变实现

```csharp
public sealed class FactoryRegistry<TKey, TProduct> : IFactoryRegistry<TKey, TProduct>
    where TKey : notnull
```

net8.0+ 上内部使用 `FrozenDictionary` 优化查找。

#### Builder（手动注册，无生成器时）

```csharp
public sealed class FactoryRegistryBuilder<TKey, TProduct>
    where TKey : notnull
{
    // 每次 Create(key) 调用该 factory
    public FactoryRegistryBuilder<TKey, TProduct> Register(TKey key, Func<TProduct> factory);

    // factory 接收 key
    public FactoryRegistryBuilder<TKey, TProduct> Register(TKey key, Func<TKey, TProduct> factory);

    public IFactoryRegistry<TKey, TProduct> Build();
}
```

- 重复 key 在 `Register` 时抛 `ArgumentException`（**运行时**检测）。

#### 异常

```csharp
public sealed class FactoryNotFoundException : Exception
{
    public FactoryNotFoundException();
    public FactoryNotFoundException(string message);
    public FactoryNotFoundException(string message, Exception innerException);
    public static FactoryNotFoundException ForKey<TKey>(TKey key) where TKey : notnull;
}
```

`ForKey` 生成消息：`"No factory registered for key '{key}'."`

#### 异步工厂（可选）

```csharp
public interface IAsyncFactory<TProduct>
{
    ValueTask<TProduct> CreateAsync(CancellationToken cancellationToken = default);
}

public interface IAsyncFactoryRegistry<TKey, TProduct> : IReadOnlyRegistry<TKey, TProduct>
    where TKey : notnull
{
    ValueTask<(bool Success, TProduct? Product)> TryCreateAsync(
        TKey key, CancellationToken cancellationToken = default);
    ValueTask<TProduct> CreateAsync(
        TKey key, CancellationToken cancellationToken = default);
}
```

`FactoryRegistryAsyncExtensions.AsAsync()` 可将同步 `IFactoryRegistry` 适配为 `IAsyncFactoryRegistry`。

### 特性（Attribute）

命名空间：`DesignPatterns.Creational`

#### 泛型版本（C# 11 / .NET 7+）

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterFactoryAttribute<TContract> : Attribute
{
    public string Key { get; }
    public bool IsAsync { get; set; }
    public int PoolSize { get; set; }

    public RegisterFactoryAttribute(string key);
}
```

#### 非泛型版本（netstandard2.0 / C# 7.3）

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterFactoryAttribute : Attribute
{
    public string Key { get; }
    public Type Contract { get; }
    public bool IsAsync { get; set; }
    public int PoolSize { get; set; }

    public RegisterFactoryAttribute(string key, Type contract);
}
```

#### 属性说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Key` | `string` | （构造函数必填） | 用于解析此工厂的 key |
| `Contract` | `Type` | （非泛型构造函数必填） | 工厂契约类型（接口或基类） |
| `IsAsync` | `bool` | `false` | 标记为异步工厂；`true` 时须实现 `IAsyncFactory<TProduct>`，生成器额外输出 `IAsyncFactoryRegistry`。`false` 时异步检测基于 `IAsyncFactory<TProduct>` 实现自动判断 |
| `PoolSize` | `int` | `0` | 启用对象池的最大池大小（per key）。`0` 禁用池化；正值使生成器输出 `IPooledFactoryRegistry`。仅异步工厂有效 |

#### 用法示例

```csharp
// 泛型 Attribute（C# 11+）
[RegisterFactory<IProductFactory>("standard")]
public class StandardFactory : IProductFactory { ... }

// 非泛型（netstandard2.0）
[RegisterFactory("standard", typeof(IProductFactory))]
public class StandardFactory : IProductFactory { ... }
```

工厂契约一般为**接口**（或基类）；实现类需 **public 无参构造**，以便生成器注册 `() => new Implementation()`。

### 生成器产出

对每个契约 `TContract`（命名规则与 Strategy 相同：去掉前缀 `I` 等），生成器输出：

#### 1. 强类型 Key 常量

```csharp
// ProductFactoryKeys.g.cs
public static partial class ProductFactoryKeys
{
    public const string Standard = "standard";
    public const string Premium = "premium";
}
```

#### 2. 静态注册表（无 DI 场景）

```csharp
// ProductFactoryRegistry.g.cs
public static partial class ProductFactoryRegistry
{
    public static IFactoryRegistry<string, IProductFactory> Create() { ... }
}
```

`Create()` 返回的注册表在每次 `Create(key)` 时**新建**产品实例（与 Strategy 注册表返回单例实例不同）。

#### 3. DI 集成（引用 `DesignPatterns.Extensions.DependencyInjection` 时）

引用扩展包会自动 Import `build/DesignPatterns.Extensions.DependencyInjection.targets`，设置 `DesignPatterns_EnableDiIntegration=true`，生成器额外输出：

```csharp
public static partial class ProductFactoryRegistry
{
    // Create() 仍保留（new() 静态实例，无 DI 生命周期）

    public static IFactoryRegistry<string, IProductFactory> Create(IServiceProvider serviceProvider) { ... }

    public static IServiceCollection RegisterDi(
        IServiceCollection services,
        ServiceLifetime implementationLifetime = ServiceLifetime.Transient,
        ServiceLifetime registryLifetime = ServiceLifetime.Singleton);
}
```

> **注意**：`implementationLifetime` 默认值为 `Transient`（与 Strategy/Chain 等模式默认 `Singleton` 不同），因为工厂语义是每次 `Create` 返回新实例。

手动 Builder 仍可用：`services.AddFactoryRegistry<string, IProduct>(builder => { ... })`（扩展包），与生成器互不冲突。

## 诊断

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP020 | Error | 同一 `TContract` 下 key 重复 | `Factory key '{0}' is already registered for contract '{1}'. Use a unique key or remove the duplicate [RegisterFactory] attribute.` |
| DP021 | Error | 标记的类未实现指定的 `TContract` | `Type '{0}' does not implement factory contract '{1}'. Implement the contract or fix the [RegisterFactory] contract argument.` |
| DP022 | Error | 标记的类缺少 public 无参构造 | `Type '{0}' must declare a public parameterless constructor for [RegisterFactory] static registration, or enable DI integration.` |
| DP023 | Info | 实现了某工厂契约但未加 `[RegisterFactory]` | `Type '{0}' implements factory contract '{1}' but has no [RegisterFactory] attribute. Add [RegisterFactory("key", typeof(...))] with a unique key and the contract type to register it.` |
| DP053 | Error | `IsAsync=true` 但未实现 `IAsyncFactory<TProduct>` | `Factory '{0}' is marked with IsAsync=true but does not implement IAsyncFactory<{1}>. Implement IAsyncFactory<{1}> or set IsAsync=false.` |
| DP054 | Error | `PoolSize` 为负数 | `Factory '{0}' has PoolSize={1}, which is negative. PoolSize must be >= 0 (0 disables pooling).` |
| DP055 | Warning | `PoolSize` 过大（可能导致内存过高） | `Factory '{0}' has PoolSize={1}, which may cause excessive memory usage. Consider a smaller value (recommended: 1-100).` |

> **归属**：DP023 属 **Analyzer**（含 CodeFix，可一键添加 `[RegisterFactory("suggested-key", typeof(TContract))]`，与 DP006 对称）；DP020 / DP021 / DP022 / DP053 / DP054 / DP055 属**生成器**。

## 不变量 / 兼容基线

1. **每次 Create 返回新实例**：`Create(key)` / `TryCreate(key, out _)` 每次调用均执行 factory 委托，得到新产品实例（与 Strategy 注册表返回同一实例不同）。
2. **重复 key 抛 ArgumentException**：`FactoryRegistryBuilder.Register` 在运行时检测重复 key，抛 `ArgumentException`（`"A factory is already registered for key '{key}'."`）。编译期由 DP020 检测。
3. **`IFactoryRegistry` 继承 `IReadOnlyRegistry`**：与 `IStrategyRegistry` 共享 `IReadOnlyRegistry<TKey, TValue>` 抽象（提供 `Keys` 和 `TryGet`）。
4. **标记的类须实现指定的 `TContract`**（DP021）。
5. **标记的类须有 public 无参构造**：生成器使用 `new()` 实例化（DP022），或启用 DI 集成由容器解析。
6. **key 唯一性**：同一 `TContract` 下 key 不可重复（DP020 在编译期强制）。

### 兼容基线

- 运行时 TFM：`netstandard2.0` + `net8.0`（两者均须可用并随包分发）。
- 泛型 Attribute（`RegisterFactoryAttribute<TContract>`）需要 C# 11+ / `net7.0+`；`netstandard2.0` 目标下用 `#if NET7_0_OR_GREATER` 条件编译。
- 非泛型 `RegisterFactoryAttribute` 始终可用，功能等价。
- net8.0 上 `FactoryRegistry<TKey, TProduct>` 内部使用 `FrozenDictionary` 优化查找。

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

### 诊断检测细节

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

## 已知局限

- **不做抽象工厂族**：不提供多个产品类型族（Abstract Factory）的完整框架
- **v1 无 async factory 完整框架**：异步工厂（`IAsyncFactory`）与池化（`IPooledFactoryRegistry`）在 v1 中为初步支持，后续版本完善
- **无 DI 自动注册（Core）**：Core 不引用 MSDI，需引用 `DesignPatterns.Extensions.DependencyInjection` 并调用 `RegisterDi`，或使用手动 Builder
- **不用反射扫描程序集**：工厂注册通过 `[RegisterFactory]` 源生成器在编译期完成

## 参考

- GoF: Factory Method / 注册表变体（Creational）
- [AGENTS.md](../../AGENTS.md) — 项目规则与里程碑
