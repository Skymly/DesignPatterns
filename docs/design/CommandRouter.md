# Design Doc: Command Router

> **版本**：v0.2.3-preview2
> **关联 Issue**：[#256](https://github.com/Skymly/DesignPatterns/issues/256)（Spec）、[#260](https://github.com/Skymly/DesignPatterns/issues/260)（本 Design Doc）

## 概述

Command Router 提供进程内、轻量的 **1:1 命令分发**：按命令 CLR 类型路由到**唯一**已注册的 handler，支持无返回值与带 `TResult` 两种契约，以及显式失败的 `SendAsync` / `TrySendAsync`。

与 Event Aggregator（1:N pub/sub）互补：后者按事件类型扇出到多个订阅者；本域按命令类型双射到单个 handler。

## 设计目标

1. 最小 API：`SendAsync` / `TrySendAsync` + 手动 `CommandRouterBuilder`
2. **1:1 双射**：每个命令 CLR 类型至多一个 handler；编译期用 DP073/DP074 强制，运行时用 builder 拒绝重复注册
3. 异步一等：`HandleAsync` / `SendAsync` 均返回 `ValueTask`，并接受 `CancellationToken`
4. 显式失败：缺失 handler 时 `SendAsync` 抛 `CommandHandlerNotFoundException`；`TrySendAsync` 返回 `false` / `CommandSendAttempt.Failed`
5. 不侵入 Core 的 DI 依赖；可选 `AddCommandRouter` 与生成器 `RegisterDi`
6. 编译期胶水：`[RegisterCommandHandler]` → `{Command}CommandHandlerRegistry`；无字符串 `*Keys`（路由键为 CLR 类型）

## API 面

### 运行时接口

| 类型 | 职责 |
|------|------|
| `ICommand` / `ICommand<out TResult>` | 可选标记接口（约定/文档用；**不**参与路由） |
| `ICommandHandler<in TCommand>` | 无返回值 handler |
| `ICommandHandler<in TCommand, TResult>` | 带结果 handler |
| `ICommandRouter` | 按命令类型分发 |
| `CommandRouter` | 默认不可变实现 |
| `CommandRouterBuilder` | 手动注册 → `Build()` |
| `CommandSendAttempt<TResult>` | `TrySendAsync` 结果包装 |
| `CommandHandlerNotFoundException` | `SendAsync` 缺失 handler |

命名空间：`DesignPatterns.Behavioral`。

#### 标记（可选）

```csharp
public interface ICommand { }
public interface ICommand<out TResult> { }
```

路由键是 **`TCommand` 的 CLR 类型**，不要求实现上述标记。

#### Handler 契约

```csharp
public interface ICommandHandler<in TCommand>
{
    ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface ICommandHandler<in TCommand, TResult>
{
    ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
```

#### 路由器

```csharp
public interface ICommandRouter
{
    ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default);

    ValueTask<TResult> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<bool> TrySendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<CommandSendAttempt<TResult>> TrySendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default);
}
```

| API | 缺失 handler | 已注册但契约不符 |
|-----|--------------|------------------|
| `SendAsync*` | `CommandHandlerNotFoundException` | （通常不会到达；注册路径保证契约） |
| `TrySendAsync`（void） | `false` | `InvalidOperationException` |
| `TrySendAsync`（result） | `CommandSendAttempt<TResult>.Failed` | `InvalidOperationException` |

#### 手动构建

```csharp
var router = new CommandRouterBuilder()
    .Register(new PingHandler())
    .Register<GetTotalCommand, decimal>(new GetTotalHandler())
    .Build();

await router.SendAsync(new PingCommand());
var total = await router.SendAsync<GetTotalCommand, decimal>(new GetTotalCommand());
```

- Builder **非**线程安全；`Build()` 后的 `CommandRouter` 对并发 `Send*` 安全。
- 同一 `TCommand` 重复 `Register` → `ArgumentException`。
- net8.0 实现可用 `FrozenDictionary` 优化只读映射。

### 特性（Attribute）

| 特性 | 形态 | 适用 TFM |
|------|------|----------|
| `[RegisterCommandHandler(typeof(FooCommand))]` | 非泛型 | 全部（netstandard2.0+） |
| `[RegisterCommandHandler<FooCommand>]` | 泛型 | `#if NET7_0_OR_GREATER`（C# 11+ generic attributes） |

`AttributeUsage`：`Class`，`Inherited = false`，`AllowMultiple = true`。

同一 handler 上泛型 + 非泛型指向**同一**命令类型时，生成器会去重；**不同** handler 争用同一命令 → **DP073**。

### 生成器产出

`RegisterCommandHandlerGenerator` 扫描 `[RegisterCommandHandler]`，按命令类型生成 `{Command}CommandHandlerRegistry`（剥离末尾 `Command` 后缀后再拼接，例如 `PingCommand` → `PingCommandHandlerRegistry`）：

| 成员 | 路径 | 条件 |
|------|------|------|
| `RegisterAll(CommandRouterBuilder)` | 静态：`new Handler()` + `Register` | handler 有公共无参构造 |
| `CreateRouter()` | 静态：内部 `RegisterAll` + `Build` | 同上 |
| `RegisterDi(IServiceCollection, ServiceLifetime)` | DI：注册 handler 实现 | `DesignPatterns_EnableDiIntegration=true` |
| `RegisterAll(CommandRouterBuilder, IServiceProvider)` | DI：从容器解析 + `Register` | 同上 |
| `RegisterAutofac(ContainerBuilder)` | Autofac 胶水 | Autofac 集成 flag |
| `RegisterAll(CommandRouterBuilder, ILifetimeScope)` | Autofac 胶水 | 同上 |

**不**生成字符串 `*Keys`（与 Strategy / Factory 不同）：路由键仅为 CLR 类型。

静态路径仅含公共无参构造的 handler；DI/Autofac 路径包含全部有效 handler（与其它生成器同源约定）。

#### 静态路径

```csharp
[RegisterCommandHandler<PingCommand>]
public sealed class PingHandler : ICommandHandler<PingCommand>
{
    public ValueTask HandleAsync(PingCommand command, CancellationToken ct = default) => default;
}

var router = PingCommandHandlerRegistry.CreateRouter();
await router.SendAsync(new PingCommand());
```

#### DI 路径（两步）

```csharp
PingCommandHandlerRegistry.RegisterDi(services); // 默认 Transient
services.AddCommandRouter((builder, sp) =>
    PingCommandHandlerRegistry.RegisterAll(builder, sp));
```

`AddCommandRouter` 默认将 `ICommandRouter` 注册为 **Singleton**：构建时冻结 handler 映射。`RegisterDi` 默认 Transient **不**表示每次 `Send` 新实例——Singleton router 在 `Build` 时解析并持有 handler（captive 语义与 Event Aggregator 的 `SubscribeAll(aggregator, provider)` 同类；相关诊断见 DP060–DP062 / DP066）。

#### Autofac 路径（两步）

```csharp
var builder = new ContainerBuilder();
PingCommandHandlerRegistry.RegisterAutofac(builder); // 默认 InstancePerDependency
builder.RegisterCommandRouter((routerBuilder, scope) =>
    PingCommandHandlerRegistry.RegisterAll(routerBuilder, scope));
```

`RegisterCommandRouter` 默认 `InstanceSharing.Shared`（singleton），captive 语义与 MSDI `AddCommandRouter` / Event Aggregator Autofac `SubscribeAll(aggregator, lifetimeScope)` 同类。

## 诊断

| ID | 级别 | 归属 | 触发条件 | 消息要点 |
|----|------|------|----------|----------|
| DP072 | Info | Analyzer + CodeFix | 实现 `ICommandHandler<*>` 但未标 `[RegisterCommandHandler]`，且编译内已存在该命令类型的**同伴**注册 | 提示补特性 |
| DP073 | Error | Generator | 两个**不同** handler 声明同一命令类型 | 保持 1:1 双射 |
| DP074 | Error | Generator | 标注了特性但未实现对应 `ICommandHandler` 契约 | 修正契约或 `For` 类型 |

### DP072 peer-presence（与 DP044 同构）

1. 扫描当前编译（含引用程序集）中已有 `[RegisterCommandHandler]` 的命令类型集合。
2. 若集合为空 → **不报告**任何 DP072（避免在尚未采用生成器路径的项目中噪声）。
3. 否则：对实现该集合中命令契约、却缺少匹配特性的具体非抽象类报告 Info。
4. 跳过抽象类、私有嵌套类。

CodeFix：在 C# 11+ 且元数据可用时优先插入 `[RegisterCommandHandler<TCommand>]`，否则 `[RegisterCommandHandler(typeof(TCommand))]`。

## 不变量 / 兼容基线

1. **1:1**：每个命令 CLR 类型至多一个 handler（builder 与 DP073）。
2. Router 在 `Build` 后**不可变**；并发 `Send*` 安全。
3. 缺失 handler：`SendAsync` 抛异常；`TrySendAsync` 不抛（仅缺失场景）。
4. 已注册但 void / result 契约不符：`TrySendAsync` 抛 `InvalidOperationException`。
5. `ICommand` / `ICommand<TResult>` **不**强制；生成器与运行时均不校验标记接口。

### 兼容基线

- netstandard2.0 / net8.0（运行时核心，两者均须可用并随包分发）
- Roslyn 组件基线 4.8.0
- DI：独立包 `DesignPatterns.Extensions.DependencyInjection`，`services.AddCommandRouter(...)`
- Autofac：独立包 `DesignPatterns.Extensions.Autofac`，生成器 `RegisterAutofac` / `RegisterAll(..., ILifetimeScope)` + 打包扩展 `RegisterCommandRouter(...)`（[#263](https://github.com/Skymly/DesignPatterns/issues/263)）

## 实现概览

### 运行时

`CommandRouter` 持有 `IReadOnlyDictionary<Type, object>`（net8.0 可冻结）。`CommandRouterBuilder` 在字典中写入 handler 实例；`Build` 复制为只读映射。

分发时按 `typeof(TCommand)` 查找；void 与 result 变体分别转型为 `ICommandHandler<TCommand>` / `ICommandHandler<TCommand, TResult>`。

### 源生成器

`RegisterCommandHandlerGenerator`（`IIncrementalGenerator` + `ForAttributeWithMetadataName`）：

1. 收集非泛型 / 泛型特性标注。
2. 按命令类型分组；去重同 handler 双形态；异 handler 冲突 → DP073，且不为该命令发 registry。
3. 契约不匹配 → DP074。
4. 生成 `{Command}CommandHandlerRegistry`（命名空间 = 命令类型命名空间）。

### 诊断检测逻辑

| ID | 归属 | 语义 |
|----|------|------|
| DP072 | Analyzer | 未注册实现（peer-presence Info）+ CodeFix |
| DP073 | Generator | 重复命令（双射破坏） |
| DP074 | Generator | 契约不匹配 |

## 设计权衡

### CLR 类型键，无 `*Keys`

命令分发天然是封闭类型集合；字符串键会复制 Strategy/Factory 的横切约定却无收益。因此不生成 `*Keys`，也不引入 DP025 类字面量键校验。

### 不可变 Router vs 可变 Aggregator

Command Router 在构建后冻结映射，适合「启动时装配、运行时只读发送」。Event Aggregator 保留运行时 `Subscribe` / `Unsubscribe`，适合动态订阅。两者故意不对齐可变性模型。

### `SendAsync` / `TrySendAsync` 命名

公开 API 统一 `*Async` 后缀（异步一等）。规格叙述里的 `Send` / `TrySend` 指语义族，而非同步重载——**MVP 无同步 Send**。

### AllowMultiple + 双射

`AllowMultiple = true` 允许同一类声明多个命令，或泛型/非泛型双写；双射约束落在「每个命令类型恰好一个 handler 类型」，由 DP073 在编译期强制。

## 与生态的边界

> 本库以技术探索为目的，**允许与 MediatR 能力重叠**。下表用于说明当前实现的设计取向差异，供选型参考，**非**「不实现」的理由（见 [AGENTS.md](../../AGENTS.md)「项目是什么」）。

### vs Event Aggregator（本库内）

| | Command Router | Event Aggregator |
|---|---|---|
| 基数 | **1:1** 命令 → handler | **1:N** 事件 → handlers |
| 枢纽 | `ICommandRouter` | `IEventAggregator` |
| 分发 | `SendAsync` / `TrySendAsync`（可有 `TResult`） | `PublishAsync`（无请求/响应） |
| 可变性 | `Build` 后不可变 | 运行时 Subscribe / Unsubscribe |
| 重复语义 | DP073：异 handler 同命令 → Error | DP045：同 handler 同事件重复标注 → Error；**允许多 handler** |
| 未注册 | DP072（peer-presence） | DP044（peer-presence） |
| 契约 | DP074 | DP046 |
| DI / Autofac | `AddCommandRouter` / `RegisterCommandRouter` | `AddEventAggregator`（Autofac 仅生成器 `SubscribeAll`） |

### vs MediatR（生态）

| | Command Router（MVP） | 典型 MediatR |
|---|---|---|
| 范围 | 进程内、编译期双射 + 显式 Try* | 请求/通知、行为管道、流式请求等完整媒介 |
| 路由 | CLR 命令类型 → 单 handler | Request/Notification + pipeline behaviors |
| 编译期 | `[RegisterCommandHandler]` + DP072–074 | 通常约定扫描或显式注册；诊断模型不同 |
| 管道 / 流 | **未**进入 MVP；规划为**本域内能力**（非独立模式域），见 [#264](https://github.com/Skymly/DesignPatterns/issues/264) / [#265](https://github.com/Skymly/DesignPatterns/issues/265) | `IPipelineBehavior`、`IStreamRequest` 等一等公民 |

重叠被允许：本域的探索重点是「编译期双射证明 + 显式失败 primitives」，而非替代 MediatR 产品面。

## 已知局限

- 不做跨进程 / 持久化 / 重试策略
- 不做请求/响应关联 ID
- **MVP 不含** pipeline behaviors、stream send、traced send（后续域内能力，见 ROADMAP 出局附录「并入 Command Router」与 #264 / #265）
- Samples 在 sibling 仓跟踪 [#266](https://github.com/Skymly/DesignPatterns/issues/266)

## 参考

- 进程内 Mediator / Command 变体（Behavioral）
- [EventAggregator.md](EventAggregator.md) — 1:N 对照
- [AGENTS.md](../../AGENTS.md) — 项目规则与诊断表
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md) — 通用开发约定
- [docs/ROADMAP.md](../ROADMAP.md) F3 Top-1
- Spec：[#256](https://github.com/Skymly/DesignPatterns/issues/256)
- 落地切片：#257–#263（Diagnostics → Runtime → Generator → Analyzer → DI → Autofac）
