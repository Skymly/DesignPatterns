# DesignPatterns

[English](README.md) | **简体中文** | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md)

[![CI](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml/badge.svg)](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Skymly.DesignPatterns?label=NuGet)](https://www.nuget.org/packages/Skymly.DesignPatterns)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://skymly.github.io/DesignPatterns.Docs/)

**轻量级运行时原语 + Roslyn 源生成器**，用于构建 .NET 设计模式。

在实现类上标注特性，源生成器即可生成键常量、注册表与调用管线，分析器会在编译期捕获误用。即使能力与 MediatR、Polly 或 Stateless 等库有所重叠也无妨——本项目专注于以小巧原语搭配编译期胶水，而非大而全的单一框架。

> **早期预览。** 公共 API、生成代码形态与 `DP###` 诊断尚**未稳定**。在宣布稳定版之前，建议锁定具体包版本（或 Git 提交）。

## 安装

```xml
<PackageReference Include="Skymly.DesignPatterns" Version="0.2.4-preview1" />
```

```powershell
dotnet add package Skymly.DesignPatterns --version 0.2.4-preview1
```

C# 命名空间保持为 `DesignPatterns.*`。完整指南：[Getting started](https://skymly.github.io/DesignPatterns.Docs/getting-started.html) · [中文文档](https://skymly.github.io/DesignPatterns.Docs/zh/)

## 快速示例

```csharp
using DesignPatterns.Behavioral;

public interface IPaymentStrategy
{
    string Pay(decimal amount);
}

[RegisterStrategy<IPaymentStrategy>("alipay")]
public sealed class AlipayPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Alipay: {amount:C}";
}

// 生成内容：PaymentStrategyKeys + PaymentStrategyRegistry
var pay = PaymentStrategyRegistry.Instance.Get(PaymentStrategyKeys.Alipay);
Console.WriteLine(pay.Pay(100m));
```

## 包含的模式

| 模式 | 生成 / 提供的能力 |
|------|-------------------|
| [Singleton](https://skymly.github.io/DesignPatterns.Docs/singleton.html) | `[GenerateSingleton]` → 延迟初始化 `Instance` |
| [Factory Registry](https://skymly.github.io/DesignPatterns.Docs/factory-registry.html) | `[RegisterFactory]` → 键常量 + 注册表（支持异步与对象池） |
| [Strategy](https://skymly.github.io/DesignPatterns.Docs/strategy.html) | `[RegisterStrategy]` → 键常量 + 注册表（支持守卫与追踪） |
| [Chain of Responsibility](https://skymly.github.io/DesignPatterns.Docs/chain-of-responsibility.html) | `[HandlerOrder]` → 有序管线（支持守卫与追踪） |
| [Composite](https://skymly.github.io/DesignPatterns.Docs/composite.html) | `[CompositePart]` → 目录、森林构建与遍历 |
| [Decorator](https://skymly.github.io/DesignPatterns.Docs/decorator.html) | `[Decorator]` → 有序装饰器栈构建 |
| [Event Aggregator](https://skymly.github.io/DesignPatterns.Docs/event-aggregator.html) | `[RegisterEventHandler]` → 全量订阅注册表 |
| [State](https://skymly.github.io/DesignPatterns.Docs/state-transition-table.html) | `[StateMachine]` / `[Transition]` → 状态表、守卫与层级 |
| [Command Router](docs/design/CommandRouter.md) | `[RegisterCommandHandler]` → 1:1 分派 + 可选管线 |
| [Step Builder](docs/design/StepBuilder.md) | `[GenerateBuilder]` → 编译期类型状态构建器 |
| [Work Graph](docs/design/WorkGraph.md) | `[WorkGraph]` / `[WorkStep]` → 分叉-汇聚 DAG 波次调度 |

诊断（`DP###`）、CodeFix 与更多 API 说明见[用户文档站](https://skymly.github.io/DesignPatterns.Docs/)及维护者[设计文档](docs/design/README.md)。

## 文档与示例

| 资源 | 用途 |
|------|------|
| [DesignPatterns.Docs](https://skymly.github.io/DesignPatterns.Docs/) | 用户使用指南（EN / 中文） |
| [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) | 可运行控制台示例 |
| [ROADMAP](docs/ROADMAP.md) | 待办事项与探索方向 |
| [CHANGELOG](CHANGELOG.md) | 版本变更记录 |

## 参与贡献

欢迎提交 Issue 与 PR —— Issue / PR / Commit 正文请统一使用 **English**。构建、测试与模块边界详见 [CONTRIBUTING.md](CONTRIBUTING.md)。

```powershell
./build.ps1 --target Ci --configuration Release
```

## 许可证

[MIT](LICENSE)
