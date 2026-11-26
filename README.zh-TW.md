# DesignPatterns

[English](README.md) | [简体中文](README.zh-CN.md) | **繁體中文** | [日本語](README.ja.md)

[![CI](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml/badge.svg)](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Skymly.DesignPatterns?label=NuGet)](https://www.nuget.org/packages/Skymly.DesignPatterns)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://skymly.github.io/DesignPatterns.Docs/)

**輕量級執行階段原語 + Roslyn 原始碼產生器**，專為 .NET 設計模式打造。

在實作類別上標註特性，產生器便會輸出鍵常數、登錄表與管線，分析器則於編譯期攔截誤用。即使功能與 MediatR、Polly 或 Stateless 等套件有所重疊也無妨——本專案專注於以精巧原語搭配編譯期膠水，而非大而全的單一框架。

> **早期預覽版本。** 公開 API、產生程式碼形態與 `DP###` 診斷尚**未穩定**。在正式宣告穩定版之前，建議固定套件版本（或 Git 提交）。

## 安裝

```xml
<PackageReference Include="Skymly.DesignPatterns" Version="0.2.4-preview1" />
```

```powershell
dotnet add package Skymly.DesignPatterns --version 0.2.4-preview1
```

C# 命名空間維持 `DesignPatterns.*`。完整指南：[Getting started](https://skymly.github.io/DesignPatterns.Docs/getting-started.html) · [中文文件](https://skymly.github.io/DesignPatterns.Docs/zh/)

## 快速範例

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

// 產生內容：PaymentStrategyKeys + PaymentStrategyRegistry
var pay = PaymentStrategyRegistry.Instance.Get(PaymentStrategyKeys.Alipay);
Console.WriteLine(pay.Pay(100m));
```

## 包含的模式

| 模式 | 產生 / 提供的能力 |
|------|-------------------|
| [Singleton](https://skymly.github.io/DesignPatterns.Docs/singleton.html) | `[GenerateSingleton]` → 延遲初始化 `Instance` |
| [Factory Registry](https://skymly.github.io/DesignPatterns.Docs/factory-registry.html) | `[RegisterFactory]` → 鍵常數 + 登錄表（支援非同步與物件集區） |
| [Strategy](https://skymly.github.io/DesignPatterns.Docs/strategy.html) | `[RegisterStrategy]` → 鍵常數 + 登錄表（支援守衛與追蹤） |
| [Chain of Responsibility](https://skymly.github.io/DesignPatterns.Docs/chain-of-responsibility.html) | `[HandlerOrder]` → 有序管線（支援守衛與追蹤） |
| [Composite](https://skymly.github.io/DesignPatterns.Docs/composite.html) | `[CompositePart]` → 目錄、森林建置與走訪 |
| [Decorator](https://skymly.github.io/DesignPatterns.Docs/decorator.html) | `[Decorator]` → 有序裝飾器堆疊建置 |
| [Event Aggregator](https://skymly.github.io/DesignPatterns.Docs/event-aggregator.html) | `[RegisterEventHandler]` → 全量訂閱登錄表 |
| [State](https://skymly.github.io/DesignPatterns.Docs/state-transition-table.html) | `[StateMachine]` / `[Transition]` → 狀態表、守衛與階層 |
| [Command Router](docs/design/CommandRouter.md) | `[RegisterCommandHandler]` → 1:1 分派 + 選用管線 |
| [Step Builder](docs/design/StepBuilder.md) | `[GenerateBuilder]` → 編譯期型別狀態建構器 |
| [Work Graph](docs/design/WorkGraph.md) | `[WorkGraph]` / `[WorkStep]` → 分叉-彙整 DAG 波次排程 |

診斷（`DP###`）、CodeFix 與深入 API 說明請參閱[文件網站](https://skymly.github.io/DesignPatterns.Docs/)與維護者[設計文件](docs/design/README.md)。

## 文件與範例

| 資源 | 用途 |
|------|------|
| [DesignPatterns.Docs](https://skymly.github.io/DesignPatterns.Docs/) | 使用者指南（EN / 中文） |
| [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) | 可執行主控台範例 |
| [ROADMAP](docs/ROADMAP.md) | 待辦項目與探索方向 |
| [CHANGELOG](CHANGELOG.md) | 版本更新紀錄 |

## 參與貢獻

歡迎提交 Issue 與 PR —— Issue / PR / Commit 文字請統一使用 **English**。建置、測試與模組邊界請參閱 [CONTRIBUTING.md](CONTRIBUTING.md)。

```powershell
./build.ps1 --target Ci --configuration Release
```

## 授權條款

[MIT](LICENSE)
