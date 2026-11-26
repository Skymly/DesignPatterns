# DesignPatterns

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | **日本語**

[![CI](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml/badge.svg)](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Skymly.DesignPatterns?label=NuGet)](https://www.nuget.org/packages/Skymly.DesignPatterns)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://skymly.github.io/DesignPatterns.Docs/)

.NET デザインパターン向けの**軽量ランタイムプリミティブ + Roslyn ソースジェネレーター**です。

実装クラスに属性を付与すると、ジェネレーターがキー定数・レジストリ・パイプラインを生成し、アナライザーが誤用をコンパイル時に検出します。MediatR・Polly・Stateless などと機能が重なっても構いません。本プロジェクトは巨大な単一フレームワークではなく、小さなプリミティブとコンパイル時グルーコードの探求に焦点を当てています。

> **初期プレビュー版です。** 公開 API、生成コード形状、`DP###` 診断はまだ**安定していません**。安定版のアナウンスまではパッケージバージョン（または Git コミット）を固定することを推奨します。

## インストール

```xml
<PackageReference Include="Skymly.DesignPatterns" Version="0.2.4-preview1" />
```

```powershell
dotnet add package Skymly.DesignPatterns --version 0.2.4-preview1
```

C# 名前空間は `DesignPatterns.*` のままです。完全ガイド：[Getting started](https://skymly.github.io/DesignPatterns.Docs/getting-started.html) · [中文ドキュメント](https://skymly.github.io/DesignPatterns.Docs/zh/)

## クイックサンプル

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

// 生成コード：PaymentStrategyKeys + PaymentStrategyRegistry
var pay = PaymentStrategyRegistry.Instance.Get(PaymentStrategyKeys.Alipay);
Console.WriteLine(pay.Pay(100m));
```

## 含まれるパターン

| パターン | 生成・提供される機能 |
|----------|----------------------|
| [Singleton](https://skymly.github.io/DesignPatterns.Docs/singleton.html) | `[GenerateSingleton]` → 遅延初期化 `Instance` |
| [Factory Registry](https://skymly.github.io/DesignPatterns.Docs/factory-registry.html) | `[RegisterFactory]` → キー定数 + レジストリ（非同期 / プール対応） |
| [Strategy](https://skymly.github.io/DesignPatterns.Docs/strategy.html) | `[RegisterStrategy]` → キー定数 + レジストリ（ガード / トレース対応） |
| [Chain of Responsibility](https://skymly.github.io/DesignPatterns.Docs/chain-of-responsibility.html) | `[HandlerOrder]` → 順序付きパイプライン（ガード / トレース対応） |
| [Composite](https://skymly.github.io/DesignPatterns.Docs/composite.html) | `[CompositePart]` → カタログ、フォレスト構築と走査 |
| [Decorator](https://skymly.github.io/DesignPatterns.Docs/decorator.html) | `[Decorator]` → 順序付きデコレータースタック構築 |
| [Event Aggregator](https://skymly.github.io/DesignPatterns.Docs/event-aggregator.html) | `[RegisterEventHandler]` → 全購読レジストリ |
| [State](https://skymly.github.io/DesignPatterns.Docs/state-transition-table.html) | `[StateMachine]` / `[Transition]` → 遷移表、ガード、階層 |
| [Command Router](docs/design/CommandRouter.md) | `[RegisterCommandHandler]` → 1:1 ディスパッチ + 任意パイプライン |
| [Step Builder](docs/design/StepBuilder.md) | `[GenerateBuilder]` → コンパイル時型状態ビルダー |
| [Work Graph](docs/design/WorkGraph.md) | `[WorkGraph]` / `[WorkStep]` → フォーク・ジョイン DAG ウェーブ |

診断（`DP###`）、CodeFix、詳細な API ノートは[ドキュメントサイト](https://skymly.github.io/DesignPatterns.Docs/)およびメンテナー向け[設計ドキュメント](docs/design/README.md)を参照してください。

## ドキュメントとサンプル

| リソース | 用途 |
|----------|------|
| [DesignPatterns.Docs](https://skymly.github.io/DesignPatterns.Docs/) | ユーザーガイド（EN / 中文） |
| [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) | 実行可能なコンソールサンプル |
| [ROADMAP](docs/ROADMAP.md) | バックログと検証候補 |
| [CHANGELOG](CHANGELOG.md) | リリースノート |

## コントリビューション

Issue と PR を歓迎します — Issue / PR / コミットメッセージは **English** を使用してください。ビルド・テスト・モジュール境界は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

```powershell
./build.ps1 --target Ci --configuration Release
```

## ライセンス

[MIT](LICENSE)
