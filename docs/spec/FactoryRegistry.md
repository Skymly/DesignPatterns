# Spec: Factory Registry

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/FactoryRegistry.md](../design/FactoryRegistry.md)
> **关联 ADR**：ADR-XXX（如有）

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

## 诊断 ID

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

## 不变量

1. **每次 Create 返回新实例**：`Create(key)` / `TryCreate(key, out _)` 每次调用均执行 factory 委托，得到新产品实例（与 Strategy 注册表返回同一实例不同）。
2. **重复 key 抛 ArgumentException**：`FactoryRegistryBuilder.Register` 在运行时检测重复 key，抛 `ArgumentException`（`"A factory is already registered for key '{key}'."`）。编译期由 DP020 检测。
3. **`IFactoryRegistry` 继承 `IReadOnlyRegistry`**：与 `IStrategyRegistry` 共享 `IReadOnlyRegistry<TKey, TValue>` 抽象（提供 `Keys` 和 `TryGet`）。
4. **标记的类须实现指定的 `TContract`**（DP021）。
5. **标记的类须有 public 无参构造**：生成器使用 `new()` 实例化（DP022），或启用 DI 集成由容器解析。
6. **key 唯一性**：同一 `TContract` 下 key 不可重复（DP020 在编译期强制）。

## 兼容基线

- 运行时 TFM：`netstandard2.0` + `net8.0`（两者均须可用并随包分发）。
- 泛型 Attribute（`RegisterFactoryAttribute<TContract>`）需要 C# 11+ / `net7.0+`；`netstandard2.0` 目标下用 `#if NET7_0_OR_GREATER` 条件编译。
- 非泛型 `RegisterFactoryAttribute` 始终可用，功能等价。
- net8.0 上 `FactoryRegistry<TKey, TProduct>` 内部使用 `FrozenDictionary` 优化查找。

## 不在范围内

- 不做抽象工厂族（多个产品类型族）的完整框架
- 不在 Core 中引用 DI 容器
- 不用反射扫描程序集注册 factory
