# 文档约定

> **权威源**。定义本仓库文档载体与维护规则。人类开发者与 AI 编码助手均须遵守。`AGENTS.md`「文档体系」为本文摘要。
>
> - **语言**：内部维护者文档以**中文**为主；面向库使用者的文档在 [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs)。
> - **冲突优先级**：`AGENTS.md` > `docs/DOCUMENTATION.md` > 其他文档。

## 1. 文档载体

| 载体 | 位置 | 用途 |
|------|------|------|
| **ADR** | `docs/adr/` | 架构决策（不可变卡片） |
| **Design Doc** | `docs/design/` | 每域一份：API 面 + 诊断/契约 + 实现细节 + 设计权衡 |
| **Roadmap** | `docs/ROADMAP.md` | 宏观规划与 backlog 排序 |
| **Issue** | GitHub Issues | 需求、Bug、任务追踪 |
| **PR** | GitHub Pull Requests | 变更审查 |
| **Release** | GitHub Releases | 版本历史 |

### 不作为独立文档类型

| 内容 | 载体 |
|------|------|
| 编码规范、兼容基线、打包、测试 | `AGENTS.md` |
| 开发环境、构建、仓库布局 | `docs/DEVELOPMENT.md` |
| 发布流程 | `docs/PUBLISHING.md` |
| 变更日志 | `CHANGELOG.md` |
| 贡献流程 | `CONTRIBUTING.md` |

**判断原则**：若信息在 Issue / PR / Release 中已完整表达，不必再写仓内文件。仓内只记跨多个 Issue/PR 的累积知识，以及不应随讨论漂移的决策。

## 2. ADR

- **何时写**：破坏性 API、跨模块架构、诊断语义边界、与已有 ADR 冲突需改决策时。
- **编号**：从 ADR-001 起，三位零填充，**不复用**；下一个编号见 [adr/README.md](adr/README.md)。
- **正文不可变**：Accepted 后不改决策正文；仅允许修正失效链接。
- **变更方式**：新决策写新 ADR，旧 ADR 标 `Superseded by ADR-NNN`。
- **模板**：[adr/_template.md](adr/_template.md)。

## 3. Design Doc

- **何时更新**：公开 API、诊断 ID、生成器行为或实现权衡变化时，在**同一 PR** 更新对应 `docs/design/<Domain>.md`。
- **内容**：API 面、诊断表、不变量/兼容基线、实现概览、设计权衡、已知局限。
- **模板**：[design/_template.md](design/_template.md)；索引：[design/README.md](design/README.md)。

## 4. Roadmap

- 维护宏观排序与探索候选；完成项移入「已完成（归档）」。
- 具体任务与验收在 **GitHub Issue** 跟踪，不在仓内另建 Plan 文档。

## 5. 同步 checklist

变更公共 API / 诊断 / 生成代码时，按需同步：

1. 本仓 `docs/design/<Domain>.md`
2. `DesignPatterns.Diagnostics/DiagnosticIds.cs` + descriptors + `AnalyzerReleases.*.md`
3. `AGENTS.md` 诊断表（若新增 `DP###`）
4. `CHANGELOG.md` `[Unreleased]`
5. 用户向文档：[DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs)（**独立 PR**）
6. 示例：[DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples)（若行为可见变化）

## 6. 目录结构

```
docs/
├── DOCUMENTATION.md
├── README.md
├── ROADMAP.md
├── adr/
│   ├── README.md
│   ├── _template.md
│   └── ADR-NNN-*.md
└── design/
    ├── README.md
    ├── _template.md
    └── <Domain>.md
```
