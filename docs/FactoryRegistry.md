# Factory Registry — 设计与实现文档

## 概述

Factory Registry 按 key 解析**工厂实现**，每次 `Create` 调用对应 factory 委托得到新产品实例。本库提供：

- **运行时**：`IFactoryRegistry`、`FactoryRegistryBuilder` 手动注册
- **编译期**：`[RegisterFactory]` / `[RegisterFactory<TContract>]` 源生成器 → 强类型 Keys + 静态 Registry
- **Analyzer**：DP023 提示未标记的工厂实现（与 Strategy DP006 对称）

## 设计目标

1. 最小 API：`TryCreate` / `Create` / `Keys`
2. 显式失败：`FactoryNotFoundException` 带 key 信息
3. 不侵入 Core 的 DI 依赖；生命周期由 factory 委托或 DI 扩展包负责

---

## 运行时 API（`DesignPatterns/Creational/`）

| 类型 | 职责 |
|------|------|
| `IFactoryRegistry<TKey, TProduct>` | 只读注册表 + 按 key 创建 |
| `FactoryRegistry<TKey, TProduct>` | 不可变实现 |
| `FactoryRegistryBuilder<TKey, TProduct>` | 手动注册 `Func<TProduct>` 或 `Func<TKey, TProduct>` |
| `FactoryNotFoundException` | key 未注册时抛出 |

> `IFactoryRegistry` **不**继承 `IReadOnlyRegistry<TKey, TValue>`；该共享抽象仅由 `IStrategyRegistry` 使用。

### 手动 Builder（无生成器时）

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

- `Register(key, Func<TProduct>)`：每次 `Create(key)` 调用该 factory
- `Register(key, Func<TKey, TProduct>)`：factory 接收 key
- 重复 key 在 `Register` 时抛 `ArgumentException`（**运行时**检测）

---

## 编译期：`[RegisterFactory]` 源生成器

### 特性

```csharp
// 泛型 Attribute（C# 11+ / net7+ 消费者）
[RegisterFactory<IProductFactory>("standard")]
public class StandardFactory : IProductFactory { ... }

// 非泛型（netstandard2.0）
[RegisterFactory("standard", typeof(IProductFactory))]
public class StandardFactory : IProductFactory { ... }
```

工厂契约一般为 **接口**（或基类）；实现类需 **public 无参构造**，以便生成器注册 `() => new Implementation()`。

### 生成器输出

对每个契约 `TContract`（命名规则与 Strategy 相同：去掉前缀 `I` 等）：

#### 1. 强类型 Key 常量

```csharp
// ProductFactoryKeys.g.cs
public static partial class ProductFactoryKeys
{
    public const string Standard = "standard";
    public const string Premium = "premium";
}
```

#### 2. 静态注册表

```csharp
// ProductFactoryRegistry.g.cs
public static partial class ProductFactoryRegistry
{
    public static IFactoryRegistry<string, IProductFactory> Create() { ... }
}
```

`Create()` 返回的注册表在每次 `Create(key)` 时 **新建** 产品实例（与 Strategy 注册表返回单例实例不同）。

---

## 诊断规则

| ID | 严重性 | 来源 | 触发条件 |
|----|--------|------|----------|
| DP020 | Error | 源生成器 | 同一 `TContract` 下 key 重复 |
| DP021 | Error | 源生成器 | 标记的类未实现指定的 `TContract` |
| DP022 | Error | 源生成器 | 标记的类缺少 public 无参构造 |
| DP023 | Info | Analyzer | 实现了某工厂契约但未加 `[RegisterFactory]` |

CodeFix：DP023 可一键添加 `[RegisterFactory("suggested-key", typeof(TContract))]`（与 DP006 对称）。

---

## 与 Strategy Registry 的对比

| | Strategy Registry | Factory Registry |
|---|---|---|
| 注册内容 | key → **实例** | key → **每次 Create 新建** |
| 编译期 | `[RegisterStrategy]` | `[RegisterFactory]` |
| 共享抽象 | `IStrategyRegistry` → `IReadOnlyRegistry` | 无 |
| 未找到 | `StrategyNotFoundException` | `FactoryNotFoundException` |
| 未注册实现 | DP006 (Info) | DP023 (Info) |

详见 [Strategy.md](Strategy.md)。

---

## DI 集成

引用 `DesignPatterns.Extensions.DependencyInjection` 后，生成器为 `{Contract}FactoryRegistry` 增加：

```csharp
ProductFactoryRegistry.RegisterDi(services); // TryAdd 各实现 + IFactoryRegistry<,>
// 或
services.AddFactoryRegistry<string, IProduct>(builder => { ... }); // 手动 Builder
```

`Create(IServiceProvider sp)` 使用 `() => sp.GetRequiredService<TImpl>()` 注册工厂委托；**每次** `registry.Create(key)` 调用委托。实现类型为 Singleton 时，多次 `Create` 返回同一实例；需要新产品实例时对实现使用 `ServiceLifetime.Transient`：

```csharp
ProductFactoryRegistry.RegisterDi(services, implementationLifetime: ServiceLifetime.Transient);
```

无 DI 包时仍可使用静态 `Create()`（`new Impl()`）。

---

## 示例

- 手动 Builder：[DesignPatterns.Samples.Factory](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.Factory)
- 生成器 + 集成测试：`tests/DesignPatterns.Tests/Integration/FactoryRegistryIntegrationTests.cs`

---

## 不做

- 不做抽象工厂族（多个产品类型族）的完整框架
- 不在 Core 中引用 DI 容器
- 不用反射扫描程序集注册 factory

---

## 参考

- GoF: Factory Method / 注册表变体（Creational）
- [Strategy.md](Strategy.md) — 策略注册表对比
- [FactoryKeyConventions.md](FactoryKeyConventions.md) — 字符串 key 命名约定（含 `"{outer}:{inner}"` 复合 key 形态）
- [AGENTS.md](../AGENTS.md) — 项目规则与里程碑
