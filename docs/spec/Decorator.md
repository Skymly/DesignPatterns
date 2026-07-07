# Spec: Decorator

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/Decorator.md](../design/Decorator.md)
> **关联 ADR**：ADR-XXX（如有）

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

## 诊断 ID

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP016 | Error | 同一 contract 下重复 `Order` | `Decorator order '{0}' is already used for service contract '{1}'. Assign a unique order value or remove the duplicate [Decorator] attribute.` |
| DP017 | Error | 标注 `[Decorator]` 但未实现 service contract | `Type '{0}' does not implement service contract '{1}'. Implement the contract or fix the [Decorator] service argument.` |
| DP018 | Error | 标注 `[Decorator]` 但未实现 `IDecorator<TService>` | `Type '{0}' does not implement IDecorator<{1}>. Implement IDecorator<{1}> to participate in generated decorator stacks.` |
| DP019 | Error | 标注 `[Decorator]` 的装饰器缺少 public 无参构造 | `Type '{0}' must declare a public parameterless constructor for generated decorator stacks.` |
| DP042 | Error | async 装饰器 `DecorateAsync` 签名不匹配 | `Async decorator '{0}' must implement 'ValueTask<{1}> DecorateAsync({1} inner, CancellationToken cancellationToken = default)'. Fix the IAsyncDecorator<{1}> implementation.` |
| DP043 | Warning | 装饰器无法从 DI 容器解析（无 public 无参构造） | `Decorator '{0}' has no public parameterless constructor. Register it in the DI container via RegisterDi(), or add a parameterless constructor for Build() without IServiceProvider.` |

CodeFix：DP017（加契约接口）、DP019（加无参构造）。DP018 需手写 `Decorate` 方法。

## 不变量

1. `Build(core)` 要求 `core` 非 `null`，否则抛 `ArgumentNullException`。
2. `Order` 越小越靠外（先接到调用，再委托向内）；先注册的装饰器包装在后注册的外层。
3. 无装饰器时 `Build(core)` 返回同一 `core` 引用（不创建包装）。
4. `DecoratorOrder` 常量名取装饰器**简单类型名**；同一 contract 下类型名须唯一。
5. `Add(null)` / `Add(..., null)` 谓词抛 `ArgumentNullException`。
6. 生成器产出的 `{Contract}DecoratorStack.Build(core)` 注册全部 `[Decorator]` 类型，条件开关（`Func<bool>`）仅适用于手动 `DecoratorStackBuilder` 组装。

## 兼容基线

- netstandard2.0 + net8.0（两者均须可用并随包分发）
- Roslyn 组件 4.8.0（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）

## 不在范围内

- `DispatchProxy` / 透明代理
- 装饰器 DAG 或按 key 选子集
- 生成器为 `[Decorator]` 自动接线运行时谓词（条件装饰仅手动 builder）
- DI 自动注册
