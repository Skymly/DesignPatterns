# Design Doc: Strategy

> **关联 Spec**：[docs/spec/Strategy.md](../spec/Strategy.md)

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

## 实现概览

### 运行时

运行时核心位于 `DesignPatterns/Behavioral/`，提供以下关键类型：

- **`IStrategy<TKey, TStrategy>` / `IAsyncStrategy<TInput, TOutput>`**：可选标记接口，便于表达同步/异步算法形状。注册表与生成器不依赖它们。
- **`IStrategyRegistry<TKey, TStrategy>`**：按 key 解析策略实现的只读注册表接口，约束 `where TKey : notnull`。提供 `TryGet`、`Get`（找不到抛 `StrategyNotFoundException`）、`TryGetWithGuard`、`Keys`。
- **`StrategyRegistry<TKey, TStrategy>`**：不可变实现。net8.0 上内部使用 `FrozenDictionary` 优化查找；netstandard2.0 回退到普通 `Dictionary`。
- **`StrategyRegistryBuilder<TKey, TStrategy>`**：手动注册 Builder，支持 `Register(key, strategy)` 与 `Register(key, factory)` 两种重载，`Build()` 返回 `IStrategyRegistry`。
- **`StrategyRegistryExtensions`**：提供 `ExecuteAsync` / `TryExecuteAsync` 便利方法，按 key 解析并执行异步策略。`TryExecuteAsync` 与 `ExecuteAsync` 对称，未命中 key 时返回 `false` 而不抛异常。

### 源生成器

`RegisterStrategyGenerator` 为增量源生成器（`IIncrementalGenerator`），使用 `ForAttributeWithMetadataName` 管线：

- **双注册**：同时注册泛型版（`RegisterStrategyAttribute`1`）与非泛型版（`RegisterStrategyAttribute`）两个元数据名，合并处理。消费者使用 `net7.0+` 的项目自动获得泛型版；低版本使用非泛型版，体验稍弱但功能等价。
- **按 `TContract` 分组**：对于每个 `TContract`，生成器输出三类产物：
  1. **强类型 Key 常量类**（`{ContractName}Keys`，`public const string`），命名规则为 `{接口名去掉前缀I和后缀Strategy}Keys`，可通过特性参数 override。
  2. **静态注册表**（`{ContractName}Registry`），包含 `Instance` 静态属性（`new()` 静态单例，无 DI 生命周期）。
  3. **DI 集成方法**（引用 `DesignPatterns.Extensions.DependencyInjection` 时自动启用）：`Create(IServiceProvider)` 返回 `ServiceProviderStrategyRegistry`，`RegisterDi(IServiceCollection, ...)` 注册到容器。

### 诊断

| 检测点 | 逻辑 | 报告位置 |
|--------|------|----------|
| DP003（key 重复） | 同一 `TContract` 下出现相同 key | 重复标注的类声明 |
| DP004（接口不匹配） | 标记的类未实现指定的 `TContract` | 标注特性位置 |
| DP006（未注册策略） | 实现了某策略契约但未加 `[RegisterStrategy]` | 类声明（Analyzer + CodeFix） |
| DP007（缺少无参构造） | 标记的类缺少 public 无参构造 | 类声明 |
| DP047–DP049（guard 校验） | guard 方法未找到 / 非 static / 签名错误 | `Guard` 属性位置 |

> DP006 属 Analyzer（含 CodeFix）；DP003/DP004/DP007/DP047–DP049 属生成器。DP005 属于 Handler，不属于 Strategy。

## 设计权衡

### 泛型 Attribute 兼容策略（`#if NET7_0_OR_GREATER`）

库目标 `netstandard2.0`，泛型 Attribute 需要 C# 11 + `net7.0+`。采用条件编译方案：

- **定义侧**：在 `netstandard2.0` 目标下用 `#if NET7_0_OR_GREATER` 条件编译泛型版 `RegisterStrategyAttribute<TContract>`；非泛型版始终可用。
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

### Guard 为 `Func<TKey, bool>` 而非 `Func<TInput, bool>`

guard 签名固定为 `Func<TKey, bool>`（仅接收 key），不接收 `TInput`。原因：注册表层面不知道 `TInput`（`TStrategy` 不要求实现 `IStrategy<TInput, TOutput>`），因此无法基于输入判断。基于输入的动态路由是业务逻辑，不在此库范围。

### `TStrategy` 不变（invariant）

`IStrategyRegistry<TKey, TStrategy>` 的 `TStrategy` 非协变 `out`——`TryGetWithGuard` 的 `out TStrategy` 参数阻止协变。这是 API 设计的自然结果，非刻意限制。

## 与生态的边界

### DI 集成

**当前已实现**：`DesignPatterns.Extensions.DependencyInjection`（MSDI）+ 生成器 `RegisterDi` / `Create(IServiceProvider)`（Strategy / Factory / Handler）。

- 生命周期由 DI 控制；注册表通过 `ServiceProviderStrategyRegistry` 在 `TryGet` 时解析实现类型。
- Core 不引用 DI；启用 DI 生成路径需引用扩展包（targets 打开 `DesignPatterns_EnableDiIntegration`）。

| 生命周期组合 | 行为 |
|--------------|------|
| Singleton 实现 + Singleton 注册表 | 默认；`TryGet` 每次解析同一实现实例 |
| Transient 实现 + Singleton 注册表 | `TryGet` 每次从容器取新实例（推荐需要可变策略时） |
| Transient 注册表 | 每次解析注册表时重建 `Create(sp)` |

手动 Builder 仍可用：`services.AddStrategyRegistry<string, IPaymentStrategy>(...)`（见扩展包），与生成器互不冲突。

### Autofac

`DesignPatterns.Extensions.Autofac` — `RegisterAutofac`（见 [Autofac.md](../Autofac.md)）。

### 插件程序集

多项目 `[RegisterStrategy]` 布局见 [PluginAssemblies.md](../PluginAssemblies.md)。

### 与 Factory Registry 的关系

| | Strategy Registry | Factory Registry |
|---|---|---|
| 注册内容 | key → 实例（或 build 时 invoke 一次的 factory） | key → `Func<TProduct>` |
| 生命周期 | 通常 Singleton（生成器用 `new()` 静态单例） | 每次 `Create` 调用 factory |
| 编译期 | `[RegisterStrategy]` 生成 Keys + Registry | `[RegisterFactory]` 生成 Keys + `Create()` |
| 共享抽象 | `IStrategyRegistry` 继承 `IReadOnlyRegistry` | `IFactoryRegistry` 继承 `IReadOnlyRegistry` |
| DI | `PaymentStrategyRegistry.RegisterDi(services)` | `ProductFactoryRegistry.RegisterDi(services)` |

详见 [FactoryRegistry.md](../spec/FactoryRegistry.md)。

## 已知局限

- **不做 DI 生命周期管理**：注册表不替代 DI 容器的生命周期管理；`Instance` 为 `new()` 静态单例，无 DI 生命周期。
- **不做策略路由引擎**：策略选择/路由逻辑是业务代码，不在此库范围。guard 仅基于 key，不基于输入。
- **不通过反射扫描程序集**：注册依赖编译期源生成器，不做运行时反射扫描。
- **不做 `Func<T1, T2, ..., TResult>` 的无限委托展开**。
- **不强制所有策略实现 `IStrategy<,>`**：`IStrategy<,>` / `IAsyncStrategy<,>` 为可选标记接口。

## 参考

- GoF: Strategy (Behavioral)
- [AGENTS.md](../../AGENTS.md) — 项目规则与里程碑
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md) — 通用开发约定
- [Spec: Strategy](../spec/Strategy.md) — 稳定契约（API 面、诊断 ID、不变量）
- [FactoryRegistry.md](../spec/FactoryRegistry.md) — Factory Registry 模式文档
- [Autofac.md](../Autofac.md) — Autofac 扩展
- [PluginAssemblies.md](../PluginAssemblies.md) — 插件程序集布局
