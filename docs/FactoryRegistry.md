# Factory Registry — 设计与实现文档

## 概述

Factory Registry 按 key 解析**工厂委托**，每次 `Create` 调用 factory 得到新产品实例。本库 v1 提供运行时 API 与手动 `FactoryRegistryBuilder`；**无源生成器、无编译期诊断**（与 Strategy 的 `[RegisterStrategy]` 生成器对称能力留 P3）。

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

### 基本用法

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

### Builder 语义

- `Register(key, Func<TProduct>)`：每次 `Create(key)` 调用该 factory
- `Register(key, Func<TKey, TProduct>)`：factory 接收 key（内部包装为无参 factory）
- 重复 key 在 `Register` 时抛 `ArgumentException`（**运行时**检测，非编译期）

---

## 与 Strategy Registry 的对比

| | Strategy Registry | Factory Registry |
|---|---|---|
| 注册内容 | key → **实例**（或 build 时 invoke 一次的 factory） | key → **`Func<TProduct>`** |
| `Get` / `Create` | 返回已注册实例 | **每次**调用 factory |
| 典型生命周期 | Singleton | Transient / 按需新建 |
| 编译期 | `[RegisterStrategy]` → Keys + 静态 Registry | 无（v1 手动 Builder） |
| 未找到 | `StrategyNotFoundException` | `FactoryNotFoundException` |

生成器产出的 Strategy 注册表在静态初始化时对每个实现执行 `new Implementation()`，得到**单例实例**；Factory 则保持「每次 Create 新建」语义。

共享抽象 `IReadOnlyRegistry<TKey, TValue>` **尚未实现**（P3 可选）。

---

## v1 设计边界

- **无** `[RegisterFactory]` 源生成器（P3 可选：Keys + 静态 `Func` 表）
- **无** DP 诊断（重复 key、契约不匹配等仅在 Builder 运行时抛出）
- **无** DI 扩展包（P3：从 `IServiceProvider` 解析 `Create`）

---

## 示例

见 [`samples/Factory.Sample/`](../samples/Factory.Sample/)。

---

## 不做

- 不做抽象工厂族（多个产品类型族）的完整框架
- 不在 Core 中引用 DI 容器
- 不用反射扫描程序集注册 factory

---

## 参考

- GoF: Factory Method / 注册表变体（Creational）
- [Strategy.md](Strategy.md) — 策略注册表对比
- [AGENTS.md](../AGENTS.md) — 项目规则与里程碑
