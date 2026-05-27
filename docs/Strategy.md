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

提供不可变实现 `StrategyRegistry<TKey, TStrategy>`，内部 `ImmutableDictionary` 或 `FrozenDictionary`（net8.0）。

### Builder（手动注册，无生成器时）

```csharp
public sealed class StrategyRegistryBuilder<TKey, TStrategy> where TKey : notnull
{
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, TStrategy strategy);
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, Func<TStrategy> factory);
    public IStrategyRegistry<TKey, TStrategy> Build();
}
```

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

#### 3. DI 扩展方法（可选生成）

```csharp
// PaymentStrategyServiceCollectionExtensions.g.cs
public static class PaymentStrategyServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentStrategies(this IServiceCollection services)
    {
        services.AddSingleton<AlipayPayment>();
        services.AddSingleton<WechatPayment>();
        services.AddSingleton<IStrategyRegistry<string, IPaymentStrategy>>(sp => ...);
        return services;
    }
}
```

DI 扩展的生成需要检测项目是否引用了对应 DI 包，或通过 MSBuild 属性开关控制。

---

## 诊断规则

| ID | 严重性 | 触发条件 | 说明 |
|----|--------|----------|------|
| DP003 | Error | 同一 `TContract` 下 key 重复 | 编译期检测冲突 |
| DP004 | Error | 标记的类未实现指定的 `TContract` | 接口不匹配 |
| DP005 | Warning | `[RegisterStrategy]` 用在非 class 上 | 无效目标 |
| DP006 | Info | 实现了某策略接口但未注册 | 建议添加特性 |

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

## 与 DI 容器的集成优先级

按 `AGENTS.md` 约定，DI 扩展包按以下顺序支持：

1. **Autofac** — `DesignPatterns.Autofac`
2. **MSDI** — `DesignPatterns.Extensions.DependencyInjection`
3. **DryIoc** — `DesignPatterns.DryIoc`

各扩展包提供：

- `builder.RegisterStrategies()` / `services.AddStrategies()` 等一行注册
- 生命周期由 DI 控制（Transient/Scoped/Singleton），注册表只负责 key→type 映射
- 不在 Core 中引入 DI 依赖

---

## 与 Factory Registry 的关系

Strategy Registry 和 Factory Registry 共享底层：

| | Strategy Registry | Factory Registry |
|---|---|---|
| 注册内容 | key → 实例/委托 | key → `Func<T>` |
| 生命周期 | 通常 Singleton | 每次 Create 新实例 |
| 共享基础 | `IRegistry<TKey, TValue>` | 同左 |

实现时可抽取 `IReadOnlyRegistry<TKey, TValue>` 作为共享接口，Strategy 和 Factory 分别包装。

---

## 实施步骤

| 步骤 | 内容 | 里程碑 |
|------|------|--------|
| 1 | `IStrategyRegistry<TKey, TStrategy>` + `StrategyRegistry` + Builder | M1 |
| 2 | `RegisterStrategyAttribute` / `RegisterStrategyAttribute<T>` 定义 | M1 |
| 3 | `RegisterStrategyGenerator`：扫描特性 → 生成 Keys + 注册表 | R1 |
| 4 | 诊断 DP003/DP004/DP005/DP006 | R1 |
| 5 | 示例项目 `samples/Strategy.Sample/` | R1 |
| 6 | DI 扩展包（Autofac → MSDI → DryIoc） | M2 |

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
