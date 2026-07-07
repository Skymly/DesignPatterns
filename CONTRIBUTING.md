# 贡献指南

本仓库处于**早期阶段**，公共 API 与生成器产出可能在没有 major 版本策略的情况下变更。贡献前请阅读根目录 [README.md](README.md) 中的「项目状态」说明。

> **开发规范的权威源是 [AGENTS.md](AGENTS.md)**（编码标准、兼容基线、打包、测试与覆盖率、诊断 ID、语言、版本与发布）。本文与之冲突时以 `AGENTS.md` 为准；功能 backlog 见 [docs/ROADMAP.md](docs/ROADMAP.md)。

- **Issue**：使用 [GitHub Issue 表单](https://github.com/Skymly/DesignPatterns/issues/new/choose)（英语）；范围限定单一模块，见 [AGENTS.md](AGENTS.md)。
- **Pull Request**：填写 [PR 模板](.github/pull_request_template.md)；一个 PR 只改一个模块。PR 描述用**英语**，且与 commit 相同——**不要**写入 AI/Agent/Cursor 等工具 attribution 或 IDE 自动插入的 summary 块。

## 环境

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0+（见仓库根目录 [`global.json`](global.json)）
- 克隆后执行：

```powershell
./build.ps1 --target Ci --configuration Release
```

（与 [`.github/workflows/ci.yml`](.github/workflows/ci.yml) 相同；打包与消费者校验用 `--target CiPack`。）

NuGet 消费者 smoke：[`eng/nuget-smoke/MetaPackage.Consumer/`](eng/nuget-smoke/MetaPackage.Consumer/)（`CiPack` 使用本地 pack；对已发布包运行 `NuGetConsumerSmokePublished --consumer-feed Published`）。发版流程见 [`docs/PUBLISHING.md`](docs/PUBLISHING.md)。

## 分支与提交

- 功能：`feature/<short-description>`
- 修复：`fix/<short-description>`
- 提交信息说明 **why**，使用**英语**（语言规则见 [AGENTS.md](AGENTS.md)）

## 测试

| 类型 | 项目 | 何时更新 |
|------|------|----------|
| 运行时单元 / 集成 | `tests/DesignPatterns.Tests` | 修改 Core API 或生成器消费路径 |
| 源生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests` | 修改生成代码；运行测试后接受 Verify 快照变更 |
| Analyzer / CodeFix | `tests/DesignPatterns.Analyzers.Tests` | 修改 DP006 / DP023 / DP024 / DP025 或 CodeFix 行为 |

### 生成器快照

修改 `DesignPatterns.SourceGenerators` 后：

```bash
dotnet test tests/DesignPatterns.SourceGenerators.Tests --filter FullyQualifiedName~YourGeneratorTests
```

若输出符合预期，将 `*.received.*` 重命名为对应的 `*.verified.txt`（或使用 Verify 推荐工作流）。

### 生成器测试引用程序集

`SourceGeneratorTestContext` 依赖 `TRUSTED_PLATFORM_ASSEMBLIES` 解析 BCL 引用。若本地测试编译失败，请使用 .NET 8 SDK 运行测试。

## Roslyn 组件结构

- `DesignPatterns.Diagnostics` — 诊断 ID 常量（`DiagnosticIds`）
- `DesignPatterns.SourceGenerators` — 增量源生成器（生成器诊断：DP001–005、DP007–022 中的生成器区段）
- `DesignPatterns.Analyzers` — `DiagnosticAnalyzer`（DP006 / DP023 / DP024 / DP025）
- `DesignPatterns.CodeFixes` — `CodeFixProvider`（需 Workspaces）

完整的 `DP###` 归类以 [AGENTS.md](AGENTS.md)「编译期诊断 ID」为唯一登记源。修改诊断 ID 时同步更新 `DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md`。

## 代码风格

仓库根目录 [`Directory.Build.props`](Directory.Build.props) 与 [`.editorconfig`](.editorconfig) 定义默认约定（nullable、缩进等）；完整编码标准见 [AGENTS.md](AGENTS.md)「编码标准」。

## 文档

本仓库文档体系分为 RFC、ADR、Spec、Design Doc、Roadmap 五种类型，完整规范见 [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)。人类开发者和 AI 编码助手均须遵守。

### 文档工作流

| 变更类型 | 需要的文档操作 |
|----------|----------------|
| 新增模式 / 诊断 ID / 破坏性 API | 创建 RFC → Accepted 后产出 ADR → 实现 PR 中更新 Spec + Design Doc |
| Bug fix | Issue + PR；如影响实现细节则更新 Design Doc |
| 公共 API 变更 | Spec 更新 + CHANGELOG 条目 |
| 诊断 ID 变更 | Spec 更新 + `AnalyzerReleases.Unshipped.md` 更新 |
| 用户可见变更 | 更新 [`CHANGELOG.md`](CHANGELOG.md)（英语，Keep a Changelog；发版时将 `[Unreleased]` 条目迁入版本节） |
| 新模式 | 在 [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) 增加示例项目、更新本仓 `docs/` 与 [`AGENTS.md`](AGENTS.md) |
| key 命名 | 遵循 [`docs/FactoryKeyConventions.md`](docs/FactoryKeyConventions.md) |
| 架构 backlog | 见 [`docs/ROADMAP.md`](docs/ROADMAP.md) |

### 文档目录

| 目录 | 用途 |
|------|------|
| [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) | 文档体系标准（类型、生命周期、模板、归档） |
| [`docs/rfc/`](docs/rfc/README.md) | RFC — 设计提案与讨论 |
| [`docs/adr/`](docs/adr/README.md) | ADR — 架构决策记录 |
| [`docs/spec/`](docs/spec/README.md) | Spec — 稳定契约 |
| [`docs/design/`](docs/design/README.md) | Design Doc — 实现细节 |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | 路线图 |
