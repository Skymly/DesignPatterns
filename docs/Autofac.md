# Autofac 集成 — 设计与实现文档

## 概述

`DesignPatterns.Extensions.Autofac` 为 **Autofac** 提供与 MSDI `RegisterDi` 对称的源生成器集成。引用扩展包后，对 `[RegisterStrategy]`、`[RegisterFactory]`、`[HandlerOrder]` 生成的 `*Registry` / `*HandlerPipeline` 额外输出 **`RegisterAutofac(ContainerBuilder)`** 与 **`Create(ILifetimeScope)`**。

扩展包**不包含**在元包 `Skymly.DesignPatterns` 中；按需单独引用。

---

## 启用方式

```xml
<PackageReference Include="Skymly.DesignPatterns.Extensions.Autofac" Version="0.1.0-preview5" />
```

或项目引用 + Import targets：

```xml
<Import Project="path/to/DesignPatterns.Extensions.Autofac/build/DesignPatterns.Extensions.Autofac.targets" />
```

targets 设置 `DesignPatterns_EnableAutofacIntegration=true`，生成器追加 Autofac 注册方法。

可与 `DesignPatterns.Extensions.DependencyInjection` **同时引用**（分别生成 `RegisterDi` 与 `RegisterAutofac`）。

---

## 生成 API

以 Strategy 为例（Factory / Handler 同理）：

```csharp
public static partial class PaymentStrategyRegistry
{
    // Instance 仍保留（静态 new() 注册表，无容器生命周期）

    public static IStrategyRegistry<string, IPaymentStrategy> Create(ILifetimeScope lifetimeScope) =>
        new ComponentContextStrategyRegistry<string, IPaymentStrategy>(lifetimeScope, _diEntries);

    public static void RegisterAutofac(
        ContainerBuilder builder,
        InstanceSharing sharing = InstanceSharing.Shared,
        object serviceKey = null);
}
```

### `RegisterAutofac` 参数

| 参数 | 默认 | 行为 |
|------|------|------|
| `sharing` | `Shared` | `SingleInstance()` vs `InstancePerDependency()`（实现类型） |
| `serviceKey` | `null` | `null` 时 `Register<TRegistry>(...).As<TRegistry>()`；非 null 时 `Keyed<TRegistry>(serviceKey)` |

实现类型以 **具体类型** 注册（与 MSDI `RegisterDi` 一致），注册表通过 `ComponentContextStrategyRegistry` 在 `TryGet` 时从 `ILifetimeScope` 解析。

---

## 推荐用法

```csharp
var builder = new ContainerBuilder();
PaymentStrategyRegistry.RegisterAutofac(builder);

using var container = builder.Build();
var registry = container.Resolve<IStrategyRegistry<string, IPaymentStrategy>>();

registry.TryGet(PaymentStrategyKeys.Alipay, out var strategy);
```

薄模块包装（供应商程序集）：

```csharp
public sealed class AlphaProviderModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        CardMotionRegistry.RegisterAutofac(builder);
        // 供应商特有的 Named / Keyed 注册仍在此手动补充
    }
}
```

---

## 运行时类型

| 类型 | 说明 |
|------|------|
| `InstanceSharing` | `Shared` / `None` 生命周期枚举 |
| `ComponentContextStrategyRegistry<TKey,TStrategy>` | 从 `ILifetimeScope` 延迟解析策略实现 |

注册表持有 **`ILifetimeScope`**（非 `IComponentContext`），避免 Autofac 在解析结束后上下文失效。

---

## 与 MSDI 对比

| | `RegisterDi` | `RegisterAutofac` |
|---|--------------|-------------------|
| 容器 | `IServiceCollection` | `ContainerBuilder` |
| 注册表工厂 | `Create(IServiceProvider)` | `Create(ILifetimeScope)` |
| 延迟解析 | `ServiceProviderStrategyRegistry` | `ComponentContextStrategyRegistry` |
| 生命周期参数 | `ServiceLifetime` × 2 | `InstanceSharing` + 可选 `serviceKey` |

---

## 非目标（v1）

- DryIoc / 其他第三方容器
- 多接口 `As<TContract>().As<TAdditional>()` 链的自动生成（可在 Module 内手动补充）
- 装饰器栈自动注册

多程序集插件模式见 [PluginAssemblies.md](PluginAssemblies.md)。

---

## 测试

`tests/DesignPatterns.Extensions.Autofac.Tests` — Strategy / Factory / Handler 的 `RegisterAutofac` 解析、生命周期与 Keyed 注册表。
