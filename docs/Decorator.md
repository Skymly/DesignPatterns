# Decorator — 设计与实现文档

## 概述

Decorator（装饰器）在**不修改核心类型**的前提下，为同一契约接口叠加横切或增强行为（日志、缓存、指标等）。本库提供 **可组合的包装栈 primitives** 与 **`[Decorator]` 编译期排序**，不替代 ASP.NET Core 中间件、动态代理或完整 AOP 框架。

## 设计目标

1. **同一契约、显式内核**：`DecoratorStackBuilder.Build(core)` 必须传入真实核心实现
2. **顺序可预测**：先注册 / `Order` 越小越靠外（先接到调用，再委托向内）
3. **零 DI 依赖**：手动组装；DI 扩展留 P3
4. **编译期胶水**：生成 `{Contract}DecoratorStack.Build(core)`，不生成装饰方法体

## 与 Chain of Responsibility 的边界

| 维度 | Chain | Decorator |
|------|-------|-----------|
| 抽象 | `IHandler<TContext>` | 装饰器与核心均实现 **`TService`** |
| 调用 | `next` 可短路 | 通常委托 `inner` |
| 核心 | 无单一内核服务 | 显式 **`core`** 实例 |
| 特性 | `[HandlerOrder<TContext>]` | `[Decorator<TService>]` |

## 运行时 API（`DesignPatterns/Structural/`）

| 类型 | 说明 |
|------|------|
| `IDecorator<TService>` | `TService Decorate(TService inner)` |
| `IDecoratorOf<TService>` | 可选标记接口（生成器不强制） |
| `DecoratorStackBuilder<TService>` | `Add<TDecorator>()` / `Add(instance)` / `Build(core)` |

### 手动组装

```csharp
var service = new DecoratorStackBuilder<IPaymentService>()
    .Add<LoggingPaymentDecorator>()
    .Add<TimingPaymentDecorator>()
    .Build(new PaymentService());
```

### 边界行为

| 场景 | 行为 |
|------|------|
| `Build(null)` / `Add(null)` | `ArgumentNullException` |
| 无装饰器 | 返回同一 `core` 引用 |

## 编译期：`[Decorator]` 源生成器

### 特性

```csharp
// 泛型（推荐，C# 11+ / net8.0）
[Decorator<IPaymentService>(10)]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService> { ... }

// 非泛型（netstandard2.0）
[Decorator(10, typeof(IPaymentService))]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService> { ... }
```

### 生成输出

对 `IPaymentService` → `PaymentServiceDecoratorStack`：

```csharp
public static partial class PaymentServiceDecoratorStack
{
    public static IPaymentService Build(IPaymentService core) { ... }
}
```

### 诊断

| ID | 说明 |
|----|------|
| DP016 | 同一 contract 下重复 Order |
| DP017 | 未实现 service contract |
| DP018 | 未实现 `IDecorator<TService>` |
| DP019 | 缺少 public 无参构造 |

CodeFix：DP017（加契约接口）、DP019（加无参构造）。DP018 需手写 `Decorate` 方法。

## 示例

见 [samples/Decorator.Sample](../samples/Decorator.Sample/)：日志 + 耗时装饰，对比 core 与 `PaymentServiceDecoratorStack.Build(core)`。

## v1 不支持

- `DispatchProxy` / 透明代理
- 装饰器 DAG 或按 key 选子集
- `PaymentServiceDecoratorOrder` 常量表（仅生成 `DecoratorStack`）
- DI 自动注册

## 参考

- GoF: Decorator (Structural)
- [ChainOfResponsibility.md](ChainOfResponsibility.md)
- [docs/DEVELOPMENT.md](DEVELOPMENT.md)
