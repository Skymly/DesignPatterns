# 贡献指南

本仓库处于**早期阶段**，公共 API 与生成器产出可能在没有 major 版本策略的情况下变更。贡献前请阅读根目录 [README.md](README.md) 中的「项目状态」说明。

- **Issue**：使用 [GitHub Issue 表单](https://github.com/Skymly/DesignPatterns/issues/new/choose)（英语）；范围限定单一模块，见 [AGENTS.md](AGENTS.md)。
- **Pull Request**：填写 [PR 模板](.github/pull_request_template.md)；一个 PR 只改一个模块。

## 环境

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0+（见仓库根目录 [`global.json`](global.json)）
- 克隆后执行：

```powershell
./build.ps1 --target Ci --configuration Release
```

（与 [`.github/workflows/ci.yml`](.github/workflows/ci.yml) 相同；打包校验用 `--target CiPack`。）

## 分支与提交

- 功能：`feature/<short-description>`
- 修复：`fix/<short-description>`
- 提交信息说明 **why**（中英文均可）

## 测试

| 类型 | 项目 | 何时更新 |
|------|------|----------|
| 运行时单元 / 集成 | `tests/DesignPatterns.Tests` | 修改 Core API 或生成器消费路径 |
| 源生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests` | 修改生成代码；运行测试后接受 Verify 快照变更 |
| Analyzer / CodeFix | `tests/DesignPatterns.Analyzers.Tests` | 修改 DP006 或 CodeFix 行为 |

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
- `DesignPatterns.SourceGenerators` — 增量源生成器（DP001–005、DP007–015）
- `DesignPatterns.Analyzers` — `DiagnosticAnalyzer`（DP006）
- `DesignPatterns.CodeFixes` — `CodeFixProvider`（需 Workspaces）

修改诊断 ID 时同步更新 `DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md`。

## 代码风格

仓库根目录 [`Directory.Build.props`](Directory.Build.props) 与 [`.editorconfig`](.editorconfig) 定义默认约定（nullable、缩进等）。

## 文档

- 新模式：增加 `samples/<Pattern>.Sample/`、更新 `docs/` 与 [`AGENTS.md`](AGENTS.md)
- 架构 backlog：见 [`docs/ROADMAP.md`](docs/ROADMAP.md)
