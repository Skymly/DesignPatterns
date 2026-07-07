# Spec: Strategy

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/Strategy.md](../design/Strategy.md)

## API 面

### 运行时接口

命名空间：`DesignPatterns.Behavioral`

#### 策略接口（可选标记接口）

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

#### 注册表

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// 按 key 解析策略实现的只读注册表。
/// </summary>
public interface IStrategyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    bool TryGet(TKey key, [MaybeNullWhen(false)] out TStrategy strategy);
    TStrategy Get(TKey key); // 找不到抛 StrategyNotFoundException
    bool TryGetWithGuard(TKey key, [MaybeNullWhen(false)] out TStrategy strategy);
    IReadOnlyCollection<TKey> Keys { get; }
}
```

> **注意**：`TStrategy` 为不变（invariant），非协变 `out`——`TryGetWithGuard` 的 `out TStrategy` 参数阻止协变。

提供不可变实现 `StrategyRegistry<TKey, TStrategy>`，net8.0 上内部使用 `FrozenDictionary` 优化查找。

#### Builder（手动注册，无生成器时）

```csharp
public sealed class StrategyRegistryBuilder<TKey, TStrategy> where TKey : notnull
{
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, TStrategy strategy);
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, Func<TStrategy> factory);
    public IStrategyRegistry<TKey, TStrategy> Build();
}
```

#### 异步策略解析

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

#### Guard 谓词

`TryGetWithGuard` 在解析策略时额外评估注册的 guard 谓词。guard 返回 `false` 时该策略视为未注册（返回 `false`）。

```csharp
var builder = new StrategyRegistryBuilder<string, IPaymentStrategy>()
    .Register("alipay", new AlipayPayment(), guard: key => isEnabled("alipay"));
var registry = builder.Build();

// guard 通过时返回策略；guard 返回 false 时返回 false
if (registry.TryGetWithGuard("alipay", out var strategy)) { ... }
```

> **设计约束**：guard 签名为 `Func<TKey, bool>`（仅接收 key），非 `Func<TInput, bool>`。注册表层面不知道 `TInput`（`TStrategy` 不要求实现 `IStrategy<TInput, TOutput>`），因此无法基于输入判断。基于输入的动态路由是业务逻辑，不在此库范围。

源生成器支持：`[RegisterStrategy<TContract>("key", Guard = nameof(CanEnable))]`，生成器校验 guard 方法签名（DP047-DP049）。

### 特性（Attribute）

命名空间：`DesignPatterns.Behavioral`

#### 泛型版本（C# 11 / .NET 7+）

```csharp
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

    /// <summary>
    /// 可选：实现类上的 static guard 方法名。设置后该方法须有签名
    /// <c>static bool Method(TKey key)</c>。guard 返回 false 时该策略视为未注册。
    /// </summary>
    public string? Guard { get; set; }

    public RegisterStrategyAttribute(string key)
    {
        Key = key;
    }
}
```

#### 非泛型版本（netstandard2.0 / C# 7.3）

```csharp
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

    /// <summary>
    /// 可选：实现类上的 static guard 方法名。签名须为 <c>static bool Method(TKey key)</c>。
    /// </summary>
    public string? Guard { get; set; }

    public RegisterStrategyAttribute(string key, Type @for)
    {
        Key = key;
        For = @for;
    }
}
```

#### 用法示例

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

### 生成器产出

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

## 诊断 ID

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP003 | Error | 同一 `TContract` 下 key 重复 | 编译期检测冲突 |
| DP004 | Error | 标记的类未实现指定的 `TContract` | 接口不匹配 |
| DP006 | Info | 实现了某策略契约但未加 `[RegisterStrategy]` | 建议添加特性 |
| DP007 | Error | 标记的类缺少 public 无参构造 | 无法 `new()` 实例化 |
| DP047 | Error | `Guard` 指定的方法在实现类上未找到 | 添加 static 方法或移除 Guard |
| DP048 | Error | `Guard` 指定的方法非 static | 改为 static |
| DP049 | Error | `Guard` 指定的方法签名错误（须 `static bool Method(TKey key)`） | 修正参数类型或返回类型 |

> **注意**：DP005 属于 Handler（`[HandlerOrder]` 重复 Order），不属于 Strategy。

## 不变量

1. **key 唯一性**：同一 `TContract` 下 key 不可重复（DP003 在编译期强制）。
2. **`TStrategy` 不变（invariant）**：`IStrategyRegistry<TKey, TStrategy>` 的 `TStrategy` 非协变 `out`——`TryGetWithGuard` 的 `out TStrategy` 参数阻止协变。
3. **guard 签名固定为 `Func<TKey, bool>`**：guard 仅接收 key，不接收 `TInput`。注册表层面不知道 `TInput`（`TStrategy` 不要求实现 `IStrategy<TInput, TOutput>`）。
4. **`[RegisterStrategy]` 不要求实现 `IStrategy<,>`**：任何接口/基类均可作为策略契约。
5. **标记的类须有 public 无参构造**：生成器使用 `new()` 实例化（DP007）。
6. **标记的类须实现指定的 `TContract`**（DP004）。
7. **`IStrategy<,>` / `IAsyncStrategy<,>` 为可选标记接口**：注册表与生成器不依赖它们。

## 兼容基线

- 运行时 TFM：`netstandard2.0` + `net8.0`（两者均须可用并随包分发）。
- 泛型 Attribute（`RegisterStrategyAttribute<TContract>`）需要 C# 11+ / `net7.0+`；`netstandard2.0` 目标下用 `#if NET7_0_OR_GREATER` 条件编译。
- 非泛型 `RegisterStrategyAttribute` 始终可用，功能等价。
- net8.0 上 `StrategyRegistry<TKey, TStrategy>` 内部使用 `FrozenDictionary` 优化查找。

## 不在范围内

- 不做策略选择/路由逻辑（这是业务代码）
- 不通过反射扫描程序集
- 不在注册表里管理对象生命周期（那是 DI 的事）
- 不做 `Func<T1, T2, ..., TResult>` 的无限委托展开
- 不强制所有策略实现 `IStrategy<,>`
