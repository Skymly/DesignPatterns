# Strategy 模式 — 设计与实现文档

## 概述

Strategy 模式允许在运行时按条件选择算法/行为实现，避免大量 `if/switch` 分支。本库的定位是：

- 提供 **注册表 + 编译期生成** 减少手写胶水
- **不替代** DI 容器的生命周期管理
- **不做** 策略选择路由引擎（那是业务逻辑）

## 设计目标

1. 用户只需在实现类上打一个特性，即可获得：强类型 Key 常量 + 注册表 + 可选 DI 扩展
2. 编译期检查 key 重复、接口不匹配
3. 不侵入实现类的继承链
4. 同时支持同步与异步策略

---

## 运行时 API（`DesignPatterns/Behavioral/`）

### 策略接口（可选）

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// 标记性策略接口。不强制实现，但配合源生成器时提供更好的类型约束。
/// </summary>
public interface IStrategy<in TInput, out TOutput>
{
    TOutput Execute(TInput input);
}

/// <summary>
/// 异步策略接口。
/// </summary>
public interface IAsyncStrategy<in TInput, TOutput>
{
    ValueTask<TOutput> ExecuteAsync(TInput input, CancellationToken ct = default);
}
```

> **注意**：`[RegisterStrategy]` 不要求实现 `IStrategy<,>`。任何接口/基类均可作为策略契约。
>
> `IStrategy<,>` / `IAsyncStrategy<,>` 为**可选标记接口**，便于表达同步/异步算法形状；注册表与生成器不依赖它们。用法见 `tests/DesignPatterns.Tests/Behavioral/StrategyMarkerInterfaceTests.cs`。

### 注册表

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// 按 key 解析策略实现的只读注册表。
/// </summary>
public interface IStrategyRegistry<TKey, out TStrategy>
    where TKey : notnull
{
    bool TryGet(TKey key, [MaybeNullWhen(false)] out TStrategy strategy);
    TStrategy Get(TKey key); // 找不到抛 StrategyNotFoundException
    IReadOnlyCollection<TKey> Keys { get; }
}
```

提供不可变实现 `StrategyRegistry<TKey, TStrategy>`，内部使用 `IReadOnlyDictionary` 包装 `Dictionary`（`FrozenDictionary` 优化留 P3）。

### Builder（手动注册，无生成器时）

```csharp
public sealed class StrategyRegistryBuilder<TKey, TStrategy> where TKey : notnull
{
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, TStrategy strategy);
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, Func<TStrategy> factory);
    public IStrategyRegistry<TKey, TStrategy> Build();
}
```

### 异步策略解析

`IAsyncStrategy<TInput, TOutput>` 与 `[RegisterStrategy]` 使用**同一套** Keys / Registry / `RegisterDi` 路径；契约可以是继承 `IAsyncStrategy<,>` 的专用接口，无需额外 attribute 或并行注册表类型。

`StrategyRegistryExtensions` 提供按 key 解析并执行的便利方法：

```csharp
// 注册表值为 IAsyncStrategy<TInput, TOutput>
var result = await registry.ExecuteAsync(PaymentAsyncStrategyKeys.Stripe, amount);

// 注册表值为继承 IAsyncStrategy<,> 的契约（需显式指定 TContract / TOutput / TInput）
var length = await registry.ExecuteAsync<ITextProcessor, int, string>(
    TextProcessorKeys.Length, "hello");

// 或等价写法
var length = await registry.Get(TextProcessorKeys.Length).ExecuteAsync("hello");
```

`TryExecuteAsync` 与 `ExecuteAsync` 对称，未命中 key 时返回 `false` 而不抛异常。

DI 场景：`{Contract}Registry.RegisterDi(services)` 后从容器解析 `IStrategyRegistry<string, TContract>`，再 `await registry.ExecuteAsync<...>(...)` 或 `Get(key).ExecuteAsync(...)`。

---

## 编译期：`[RegisterStrategy]` 源生成器

### 特性设计

优先使用 **泛型 Attribute**（C# 11 / .NET 7+），同时为低版本提供非泛型重载：

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// 标记一个类为某策略接口的实现，并注册到编译期生成的策略注册表中。
/// </summary>
/// <typeparam name="TContract">策略契约接口。</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterStrategyAttribute<TContract> : Attribute
{
    /// <summary>
    /// 用于解析此策略的 key。
    /// </summary>
    public string Key { get; }

    public RegisterStrategyAttribute(string key)
    {
        Key = key;
    }
}

/// <summary>
/// 非泛型版本，用于不支持泛型 Attribute 的目标框架。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterStrategyAttribute : Attribute
{
    public string Key { get; }

    /// <summary>
    /// 策略契约接口类型。
    /// </summary>
    public Type For { get; }

    public RegisterStrategyAttribute(string key, Type @for)
    {
        Key = key;
        For = @for;
    }
}
```

### 用法示例

```csharp
// 泛型 Attribute（推荐，C# 11+）
[RegisterStrategy<IPaymentStrategy>("alipay")]
public class AlipayPayment : IPaymentStrategy { ... }

[RegisterStrategy<IPaymentStrategy>("wechat")]
public class WechatPayment : IPaymentStrategy { ... }

// 非泛型（netstandard2.0 / C# 7.3）
[RegisterStrategy("alipay", typeof(IPaymentStrategy))]
public class AlipayPayment : IPaymentStrategy { ... }
```

### 生成器输出

对于每个 `TContract`，生成器输出：

#### 1. 强类型 Key 常量

```csharp
// PaymentStrategyKeys.g.cs
public static partial class PaymentStrategyKeys
{
    public const string Alipay = "alipay";
    public const string Wechat = "wechat";
}
```

命名规则：`{接口名去掉前缀I和后缀Strategy}Keys`，可通过特性参数 override。

#### 2. 静态注册表（无 DI 场景）

```csharp
// PaymentStrategyRegistry.g.cs
public static partial class PaymentStrategyRegistry
{
    private static readonly IStrategyRegistry<string, IPaymentStrategy> _instance = 
        new StrategyRegistry<string, IPaymentStrategy>(
            new Dictionary<string, IPaymentStrategy>
            {
                ["alipay"] = new AlipayPayment(),
                ["wechat"] = new WechatPayment(),
            });

    public static IStrategyRegistry<string, IPaymentStrategy> Instance => _instance;
}
```

#### 3. DI 集成（引用 `DesignPatterns.Extensions.DependencyInjection` 时）

引用扩展包会自动 Import `build/DesignPatterns.Extensions.DependencyInjection.targets`，设置 `DesignPatterns_EnableDiIntegration=true`，生成器额外输出：

```csharp
public static partial class PaymentStrategyRegistry
{
    // Instance 仍保留（new() 静态实例，无 DI 生命周期）

    public static IStrategyRegistry<string, IPaymentStrategy> Create(IServiceProvider serviceProvider) =>
        new ServiceProviderStrategyRegistry<string, IPaymentStrategy>(serviceProvider, _diEntries);

    public static IServiceCollection RegisterDi(
        IServiceCollection services,
        ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
        ServiceLifetime registryLifetime = ServiceLifetime.Singleton);
}
```

推荐用法：

```csharp
var services = new ServiceCollection();
PaymentStrategyRegistry.RegisterDi(services);
var registry = services.BuildServiceProvider()
    .GetRequiredService<IStrategyRegistry<string, IPaymentStrategy>>();
```

| 生命周期组合 | 行为 |
|--------------|------|
| Singleton 实现 + Singleton 注册表 | 默认；`TryGet` 每次解析同一实现实例 |
| Transient 实现 + Singleton 注册表 | `TryGet` 每次从容器取新实例（推荐需要可变策略时） |
| Transient 注册表 | 每次解析注册表时重建 `Create(sp)` |

手动 Builder 仍可用：`services.AddStrategyRegistry<string, IPaymentStrategy>(...)`（见扩展包），与生成器互不冲突。

---

## 诊断规则

| ID | 严重性 | 来源 | 触发条件 | 说明 |
|----|--------|------|----------|------|
| DP003 | Error | 源生成器 | 同一 `TContract` 下 key 重复 | 编译期检测冲突 |
| DP004 | Error | 源生成器 | 标记的类未实现指定的 `TContract` | 接口不匹配 |
| DP006 | Info | Analyzer | 实现了某策略契约但未加 `[RegisterStrategy]` | 建议添加特性 |
| DP007 | Error | 源生成器 | 标记的类缺少 public 无参构造 | 无法 `new()` 实例化 |

> **注意**：DP005 属于 Handler（`[HandlerOrder]` 重复 Order），不属于 Strategy。

---

## 泛型 Attribute 兼容策略

由于库目标 `netstandard2.0`，需要条件处理：

- **定义侧**：泛型 Attribute 需要 C# 11 + `net7.0+`。在 `netstandard2.0` 目标下用 `#if NET7_0_OR_GREATER` 条件编译。
- **生成器侧**：`ForAttributeWithMetadataName` 同时注册两个元数据名（泛型版与非泛型版），合并处理。
- **消费者侧**：使用 `net7.0+` 的项目自动获得泛型版；低版本使用非泛型版，体验稍弱但功能等价。

```csharp
// 条件编译示例
#if NET7_0_OR_GREATER
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterStrategyAttribute<TContract> : Attribute { ... }
#endif

// 非泛型版始终可用
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterStrategyAttribute : Attribute { ... }
```

---

## 与 DI 容器集成

**当前已实现**：`DesignPatterns.Extensions.DependencyInjection`（MSDI）+ 生成器 `RegisterDi` / `Create(IServiceProvider)`（Strategy / Factory / Handler）。

- 生命周期由 DI 控制；注册表通过 `ServiceProviderStrategyRegistry` 在 `TryGet` 时解析实现类型。
- Core 不引用 DI；启用 DI 生成路径需引用扩展包（targets 打开 `DesignPatterns_EnableDiIntegration`）。
- **后续可选**：Autofac / DryIoc 独立扩展包（与 MSDI 对称的 `RegisterDi` 包装）。

---

## 与 Factory Registry 的关系

| | Strategy Registry | Factory Registry |
|---|---|---|
| 注册内容 | key → 实例（或 build 时 invoke 一次的 factory） | key → `Func<TProduct>` |
| 生命周期 | 通常 Singleton（生成器用 `new()` 静态单例） | 每次 `Create` 调用 factory |
| 编译期 | `[RegisterStrategy]` 生成 Keys + Registry | `[RegisterFactory]` 生成 Keys + `Create()` |
| 共享抽象 | `IStrategyRegistry` 继承 `IReadOnlyRegistry` | Factory 不继承 |
| DI | `PaymentStrategyRegistry.RegisterDi(services)` | `ProductFactoryRegistry.RegisterDi(services)` |

详见 [FactoryRegistry.md](FactoryRegistry.md)。

---

## 实施步骤

| 步骤 | 内容 | 里程碑 |
|------|------|--------|
| 1 | `IStrategyRegistry<TKey, TStrategy>` + `StrategyRegistry` + Builder | M1 |
| 2 | `RegisterStrategyAttribute` / `RegisterStrategyAttribute<T>` 定义 | M1 |
| 3 | `RegisterStrategyGenerator`：扫描特性 → 生成 Keys + 注册表 | R1 |
| 4 | 诊断 DP003/DP004/DP007（生成器）+ DP006（Analyzer） | R1 |
| 5 | 示例项目 [DesignPatterns.Samples.Strategy](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.Strategy) | R1 |
| 6 | DI 扩展包 + 生成器 `RegisterDi` | 已完成（MSDI） |

---

## 规划中的能力

- Autofac / DryIoc 扩展包
- IDE CompletionProvider（维护成本高）

---

## 不做

- 不做策略选择/路由逻辑（这是业务代码）
- 不通过反射扫描程序集
- 不在注册表里管理对象生命周期（那是 DI 的事）
- 不做 `Func<T1, T2, ..., TResult>` 的无限委托展开
- 不强制所有策略实现 `IStrategy<,>`

---

## 参考

- GoF: Strategy (Behavioral)
- [AGENTS.md](../AGENTS.md) — 项目规则与里程碑
- [docs/DEVELOPMENT.md](DEVELOPMENT.md) — 通用开发约定
