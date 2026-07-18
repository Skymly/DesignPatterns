# Design Doc: Decorator

> **版本**：v0.2.3-preview1（与 NuGet 包版本对齐）
> **关联 ADR**：ADR-XXX（如有）

## 概述

Decorator（装饰器）在**不修改核心类型**的前提下，为同一契约接口叠加横切或增强行为（日志、缓存、指标等）。本库提供 **可组合的包装栈 primitives** 与 **`[Decorator]` 编译期排序**，不替代 ASP.NET Core 中间件、动态代理或完整 AOP 框架。

## 设计目标

1. **同一契约、显式内核**：`DecoratorStackBuilder.Build(core)` 必须传入真实核心实现
2. **顺序可预测**：先注册 / `Order` 越小越靠外（先接到调用，再委托向内）
3. **零 DI 依赖**：手动组装；DI 扩展留 P3
4. **编译期胶水**：生成 `{Contract}DecoratorStack.Build(core)` 与 `{Contract}DecoratorOrder` 常量，不生成装饰方法体

## API 面

### 运行时接口

运行时类型位于 `DesignPatterns/Structural/`。

| 类型 | 说明 |
|------|------|
| `IDecorator<TService>` | `TService Decorate(TService inner)` |
| `IDecoratorOf<TService>` | 可选标记接口（生成器不强制） |
| `DecoratorStackBuilder<TService>` | `Add<TDecorator>()` / `Add(instance)` / `Add(..., Func<bool>)` / `Build(core)` |

`DecoratorStackBuilder<TService>` 方法签名：

| 方法 | 说明 |
|------|------|
| `Add<TDecorator>()` where `TDecorator : TService, IDecorator<TService>, new()` | 注册一个装饰器类型（无参构造） |
| `Add(TService decorator)` | 注册一个装饰器实例（须实现 `IDecorator<TService>`） |
| `Add<TDecorator>(Func<bool> predicate)` | 注册一个带运行时谓词的装饰器类型；谓词在 `Build` 时求值，为 `false` 时跳过该装饰器 |
| `Add(TService decorator, Func<bool> predicate)` | 注册一个带运行时谓词的装饰器实例 |
| `Build(TService core)` | 按 `Add` 顺序（`Order` 越小越靠外）包装 `core`，返回最外层 `TService` |

### 特性（Attribute）

```csharp
// 泛型（推荐，C# 11+ / net8.0）
[Decorator<IPaymentService>(10)]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService> { ... }

// 非泛型（netstandard2.0）
[Decorator(10, typeof(IPaymentService))]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService> { ... }
```

`[Decorator]` / `[Decorator<TService>]` 构造函数签名：

| 特性 | 构造函数 | 说明 |
|------|----------|------|
| `DecoratorAttribute<TService>` | `(int order)` | 泛型形式，C# 11+ / net8.0 |
| `DecoratorAttribute` | `(int order, Type serviceType)` | 非泛型形式，netstandard2.0 |

`Order` 数值越小越靠外（先接到调用，再委托向内）。

### 生成器产出

对 `IPaymentService` → `PaymentServiceDecoratorStack` 与 `PaymentServiceDecoratorOrder`：

```csharp
public static partial class PaymentServiceDecoratorStack
{
    public static IPaymentService Build(IPaymentService core) { ... }
}

public static partial class PaymentServiceDecoratorOrder
{
    public const int LoggingPaymentDecorator = 10;
    public const int MetricsPaymentDecorator = 20;
}
```

- `{Contract}DecoratorStack.Build(core)`：按 `[Decorator]` 的 `Order` 升序包装 `core`，返回最外层 `TService`。注册全部 `[Decorator]` 类型，**不**应用运行时谓词。
- `{Contract}DecoratorOrder`：为每个装饰器生成 `public const int`，常量名取装饰器**简单类型名**。同一 contract 下类型名须唯一（跨命名空间同名会导致重复常量编译错误）。

特性中可引用常量替代魔法数：

```csharp
[Decorator<IPaymentService>(PaymentServiceDecoratorOrder.LoggingPaymentDecorator)]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService> { ... }
```

## 诊断

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP016 | Error | 同一 contract 下重复 `Order` | `Decorator order '{0}' is already used for service contract '{1}'. Assign a unique order value or remove the duplicate [Decorator] attribute.` |
| DP017 | Error | 标注 `[Decorator]` 但未实现 service contract | `Type '{0}' does not implement service contract '{1}'. Implement the contract or fix the [Decorator] service argument.` |
| DP018 | Error | 标注 `[Decorator]` 但未实现 `IDecorator<TService>` | `Type '{0}' does not implement IDecorator<{1}>. Implement IDecorator<{1}> to participate in generated decorator stacks.` |
| DP019 | Error | 标注 `[Decorator]` 的装饰器缺少 public 无参构造 | `Type '{0}' must declare a public parameterless constructor for generated decorator stacks.` |
| DP042 | Error | async 装饰器 `DecorateAsync` 签名不匹配 | `Async decorator '{0}' must implement 'ValueTask<{1}> DecorateAsync({1} inner, CancellationToken cancellationToken = default)'. Fix the IAsyncDecorator<{1}> implementation.` |
| DP043 | Warning | 装饰器无法从 DI 容器解析（无 public 无参构造） | `Decorator '{0}' has no public parameterless constructor. Register it in the DI container via RegisterDi(), or add a parameterless constructor for Build() without IServiceProvider.` |

CodeFix：DP017（加契约接口）、DP019（加无参构造）。DP018 需手写 `Decorate` 方法。

## 不变量 / 兼容基线

1. `Build(core)` 要求 `core` 非 `null`，否则抛 `ArgumentNullException`。
2. `Order` 越小越靠外（先接到调用，再委托向内）；先注册的装饰器包装在后注册的外层。
3. 无装饰器时 `Build(core)` 返回同一 `core` 引用（不创建包装）。
4. `DecoratorOrder` 常量名取装饰器**简单类型名**；同一 contract 下类型名须唯一。
5. `Add(null)` / `Add(..., null)` 谓词抛 `ArgumentNullException`。
6. 生成器产出的 `{Contract}DecoratorStack.Build(core)` 注册全部 `[Decorator]` 类型，条件开关（`Func<bool>`）仅适用于手动 `DecoratorStackBuilder` 组装。

### 兼容基线

- netstandard2.0 + net8.0（两者均须可用并随包分发）
- Roslyn 组件 4.8.0（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）

## 实现概览

### 运行时

**`DecoratorStackBuilder<TService>`**：可组合的包装栈 builder。通过 `Add<TDecorator>()` / `Add(instance)` / `Add(..., Func<bool>)` 注册装饰器，`Build(core)` 按 `Add` 顺序（`Order` 越小越靠外）逐层包装 `core`，返回最外层 `TService`。

手动组装：

```csharp
var service = new DecoratorStackBuilder<IPaymentService>()
    .Add<LoggingPaymentDecorator>()
    .Add<TimingPaymentDecorator>()
    .Build(new PaymentService());
```

**条件装饰（`Func<bool>`）**：`Add` 的重载可传入 `Func<bool>`；谓词在 **`Build` 时**求值，为 `false` 时跳过该装饰器（返回 inner 不变）：

```csharp
var enableMetrics = configuration.GetValue<bool>("Metrics:Enabled");

var service = new DecoratorStackBuilder<IPaymentService>()
    .Add<LoggingPaymentDecorator>()
    .Add<MetricsPaymentDecorator>(() => enableMetrics)
    .Build(new PaymentService());
```

生成器产出的 `{Contract}DecoratorStack.Build(core)` 仍注册全部 `[Decorator]` 类型；条件开关仅适用于手动 `DecoratorStackBuilder` 组装。

**边界行为**：

| 场景 | 行为 |
|------|------|
| `Build(null)` / `Add(null)` | `ArgumentNullException` |
| `Add(..., null)` 谓词 | `ArgumentNullException` |
| 无装饰器 | 返回同一 `core` 引用 |

### 源生成器

**`[Decorator]` 排序**：`DecoratorGenerator` 扫描标注 `[Decorator<TService>(order)]` 或 `[Decorator(order, typeof(TService))]` 的装饰器类，按 `order` 数值升序排列（数值越小越靠外，先接到调用）。

**`{Contract}DecoratorStack.Build` 生成**：对每种 `TService` 生成 `public static partial class {Contract}DecoratorStack`，暴露 `Build(core)` 方法，按 `Order` 升序逐层包装 `core`。生成器注册全部 `[Decorator]` 类型，**不**应用运行时谓词。

**`{Contract}DecoratorOrder` 常量生成**：为每个装饰器生成 `public const int`，常量名取装饰器**简单类型名**；同一 contract 下类型名须唯一（跨命名空间同名会导致重复常量编译错误）。重复 `Order` 仍由 **DP016** 在编译期报告。

特性中可引用常量替代魔法数：

```csharp
[Decorator<IPaymentService>(PaymentServiceDecoratorOrder.LoggingPaymentDecorator)]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService> { ... }
```

### 诊断检测细节

| ID | 说明 |
|----|------|
| DP016 | 同一 contract 下重复 Order |
| DP017 | 未实现 service contract |
| DP018 | 未实现 `IDecorator<TService>` |
| DP019 | 缺少 public 无参构造 |
| DP042 | async 装饰器 `DecorateAsync` 签名不匹配 |
| DP043 | 装饰器无法从 DI 容器解析（无 public 无参构造） |

CodeFix：DP017（加契约接口）、DP019（加无参构造）。DP018 需手写 `Decorate` 方法。

## 设计权衡

### Decorator vs Chain of Responsibility

| 维度 | Chain | Decorator |
|------|-------|-----------|
| 抽象 | `IHandler<TContext>` | 装饰器与核心均实现 **`TService`** |
| 调用 | `next` 可短路 | 通常委托 `inner` |
| 核心 | 无单一内核服务 | 显式 **`core`** 实例 |
| 特性 | `[HandlerOrder<TContext>]` | `[Decorator<TService>]` |

Decorator 与核心实现**同一契约**，装饰器通过 `IDecorator<TService>.Decorate(inner)` 包装并委托向内；Chain 的 handler 实现 `IHandler<TContext>`，通过 `next` 委托且可短路。Decorator 要求**显式 `core`** 实例作为内核，Chain 无单一内核服务。

### 选择了显式 core 而非无 core，因为...

Decorator 语义上需要一个真实的核心实现作为包装起点；`Build(core)` 必须传入非 `null` 的 `core`。无装饰器时返回同一 `core` 引用，保持透明。这与 Chain（无单一内核服务、终端 handler 即处理者）形成对比。

### 条件装饰仅在手动 builder

`Func<bool>` 谓词仅在手动 `DecoratorStackBuilder` 组装时可用；生成器产出的 `{Contract}DecoratorStack.Build(core)` 注册全部 `[Decorator]` 类型，不在编译期胶水中嵌入运行时谓词。这保持生成器产出的确定性与简单性，条件开关留给调用方在手动组装时决定。

## 与生态的边界

| 维度 | Chain | Decorator |
|------|-------|-----------|
| 抽象 | `IHandler<TContext>` | 装饰器与核心均实现 **`TService`** |
| 调用 | `next` 可短路 | 通常委托 `inner` |
| 核心 | 无单一内核服务 | 显式 **`core`** 实例 |
| 特性 | `[HandlerOrder<TContext>]` | `[Decorator<TService>]` |

Decorator 聚焦「同一契约的横切增强」，不替代 ASP.NET Core 中间件、动态代理（`DispatchProxy`）或完整 AOP 框架。

## 已知局限

- **无 `DispatchProxy` / 透明代理**：装饰器须显式实现 `IDecorator<TService>` 并手写 `Decorate` 方法，不提供透明代理式自动包装。
- **无 DI 自动注册**：Core 不引用 MSDI，装饰器不会自动注册到容器；DI 扩展留 P3。
- **条件装饰不在生成栈中**：生成器产出的 `{Contract}DecoratorStack.Build(core)` 注册全部 `[Decorator]` 类型，运行时谓词（`Func<bool>`）仅适用于手动 `DecoratorStackBuilder` 组装。

示例见 [DesignPatterns.Samples.Decorator](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.Decorator)：日志 + 耗时装饰、`DecoratorOrder` 常量引用、条件 `Add` 与 `PaymentServiceDecoratorStack.Build(core)` 对比。

## 参考

- GoF: Decorator (Structural)
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)
- [AGENTS.md](../../AGENTS.md)
