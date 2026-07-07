# 文档体系标准

> **权威源**。本文档定义本仓库所有文档的类型、结构、生命周期与归档规则。人类开发者和 AI 编码助手（Agent）均须遵守。`AGENTS.md`「文档体系」章节为本文档的精简摘要。
>
> - **语言**：内部维护者文档以**中文**为主；面向库使用者的文档在 [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs) 仓库。
> - **冲突优先级**：`AGENTS.md` > `docs/DOCUMENTATION.md` > 其他文档。

---

## 1. 文档类型总览

| 类型 | 目录 | 用途 | 稳定性 | 变更门槛 |
|------|------|------|--------|----------|
| **RFC** | `docs/rfc/` | 设计提案与讨论记录 | 提案阶段，频繁迭代 | 自由修改（Review 前） |
| **ADR** | `docs/adr/` | 架构决策记录（不可变卡片） | 已决策，仅追加 | 仅 Supersede，不修改原文 |
| **Spec** | `docs/spec/` | 稳定契约（API 面、诊断 ID、不变量） | 版本化稳定 | 需 RFC + ADR 方可变更 |
| **Design Doc** | `docs/design/` | 实现细节、设计权衡、已知局限 | 随实现演进 | PR 随代码同步更新 |
| **Roadmap** | `docs/ROADMAP.md` | 功能与技术 backlog | 滚动维护 | 维护者评审 |
| **Plan** | GitHub Issue | 任务计划（目标、步骤、验收） | 短生命周期 | Issue 内自由更新 |

### 1.1 不作为独立文档类型

| 内容 | 载体 |
|------|------|
| 编码规范、兼容基线、打包、测试 | `AGENTS.md`（权威源） |
| 开发环境、构建、仓库布局 | `docs/DEVELOPMENT.md` |
| 发布流程 | `docs/PUBLISHING.md` |
| 变更日志 | `CHANGELOG.md`（Keep a Changelog 格式） |
| 贡献流程 | `CONTRIBUTING.md` |

---

## 2. RFC — Request for Comments

### 2.1 用途

对**有设计争议或影响面较大**的变更提出设计方案，供讨论与决策。小改动（bug fix、单方法新增）无需 RFC，直接 Issue + PR。

### 2.2 何时需要 RFC

| 场景 | 需要 RFC？ |
|------|-----------|
| 新增模式（新源生成器 + 诊断） | ✅ 必须 |
| 新增或变更公共 API（破坏性） | ✅ 必须 |
| 新增诊断 ID（`DP###`） | ✅ 必须 |
| 跨模块架构变更 | ✅ 必须 |
| 单模块内 bug fix | ❌ Issue + PR |
| 单模块内新增非破坏性 API | ❌ Issue + PR（但需在 Design Doc 记录） |
| 文档/测试/重构 | ❌ Issue + PR |
| 工程整改（CI、构建脚本） | ⚠️ 视影响面，由维护者判断 |

### 2.3 文件命名

```
docs/rfc/<PascalCaseName>.md
```

示例：`CompositeParallelTraversal.md`、`HierarchicalStateMachine.md`。

### 2.4 Frontmatter 标准

每份 RFC **必须**以如下元数据块开头（blockquote 格式，字段固定）：

```markdown
> **状态**：Draft | Review | Accepted | Rejected | Implemented | Superseded
> **类型**：Feature | Pattern | Architecture | Process
> **创建**：YYYY-MM-DD
> **更新**：YYYY-MM-DD
> **作者**：维护者 / 贡献者
> **关联 Roadmap**：FXX（如有）
> **关联 Issue**：#XXX（如有）
> **衍生 ADR**：ADR-XXX（Accepted 后填写）
```

### 2.5 正文章节模板

```markdown
# RFC: <标题>

> （frontmatter）

## 摘要
一段话说明提案内容。

## 动机
为什么需要这个变更？现有局限是什么？

## 非目标
明确不做什么，避免范围蔓延。

## 设计方案
### 概念模型
### API 设计
### 诊断影响
### 实现方案

## 替代方案
考虑过但否决的方案及原因。

## 开放问题
讨论中尚未决策的问题（Review 阶段）。

## 决策记录
Review 结束后记录最终决策与理由（Accepted 阶段填写）。

## 参考
```

### 2.6 生命周期

```
Draft → Review → Accepted → Implemented → (archive)
                ↘ Rejected → (archive)
```

| 状态 | 含义 | 操作 |
|------|------|------|
| **Draft** | 作者撰写中，未公开征求意见 | 可自由修改 |
| **Review** | 公开征求意见 | 更新 frontmatter `状态`；讨论在 GitHub Issue 或 PR Comments |
| **Accepted** | 决策通过，待实现 | 填写「决策记录」章节；创建衍生 ADR；关联 Roadmap 项标记「进行中」 |
| **Rejected** | 决策否决 | 记录否决理由；移入 `archive/` |
| **Implemented** | 已实现并合并 | 关联 PR 号；移入 `archive/` |
| **Superseded** | 被后续 RFC 取代 | 标注取代者链接；移入 `archive/` |

### 2.7 归档规则

- 状态变为 **Rejected**、**Implemented** 或 **Superseded** 时，文件移入 `docs/rfc/archive/`。
- 归档文件**不再修改**（除修正链接失效）。
- `docs/rfc/README.md` 状态板保留归档条目的链接。

---

## 3. ADR — Architecture Decision Record

### 3.1 用途

记录**最终架构决策**的简短不可变卡片。ADR 不是讨论场所——讨论在 RFC 中完成，ADR 只记录结论。

### 3.2 与 RFC 的关系

- RFC **Accepted** → 产出一份 ADR（在 RFC frontmatter `衍生 ADR` 字段和 ADR `关联 RFC` 字段双向链接）。
- 少数情况下，无需完整 RFC 的小决策也可直接写 ADR（如编码风格选择），但需在 ADR 中说明为何跳过 RFC。

### 3.3 文件命名

```
docs/adr/ADR-<NNN>-<kebab-case-title>.md
```

编号从 `001` 开始，零填充三位，**不复用编号**。示例：`ADR-001-roslyn-source-generators.md`。

### 3.4 格式模板

```markdown
# ADR-NNN: <标题>

| 字段 | 值 |
|------|-----|
| **状态** | Accepted | Superseded by ADR-XXX | Deprecated |
| **日期** | YYYY-MM-DD |
| **关联 RFC** | [docs/rfc/XXX.md](../rfc/XXX.md)（或「无 — 直接决策」） |

## 背景

为什么需要做这个决策？当时的约束和问题是什么？

## 决策

最终决定了什么？一句话概括 + 补充说明。

## 后果

这个决策带来的正面和负面影响。

## 参考

- 相关 ADR、文档、外部链接
```

### 3.5 不可变原则

- ADR 一旦 **Accepted**，正文**不修改**。
- 若决策被推翻，创建新 ADR 并将旧 ADR 状态改为 `Superseded by ADR-XXX`，旧 ADR 正文仍不修改。
- 编号**永不复用**。

---

## 4. Spec — 规范文档

### 4.1 用途

定义模式的**稳定契约**：公共 API 面、诊断 ID、不变量、兼容基线。Spec 是「接口」级别的文档——描述 **what**，不描述 **how**。

### 4.2 与 Design Doc 的关系

| 维度 | Spec | Design Doc |
|------|------|------------|
| 回答 | What（契约是什么） | How + Why（怎么实现、为什么这样做） |
| 稳定性 | 高——变更需 RFC + ADR | 中——随实现演进 |
| 读者 | 库使用者 + 维护者 | 维护者 |
| 位置 | `docs/spec/` | `docs/design/` |

### 4.3 文件命名

```
docs/spec/<PatternName>.md
```

与模式名一致。示例：`docs/spec/Strategy.md`。

### 4.4 章节模板

```markdown
# Spec: <模式名>

> **版本**：vX.Y（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/<PatternName>.md](../design/<PatternName>.md)
> **关联 ADR**：ADR-XXX（如有）

## API 面

### 运行时接口
（接口定义、方法签名、泛型约束）

### 特性（Attribute）
（特性类、属性、构造函数签名）

### 生成器产出
（生成的类名、方法签名、命名空间）

## 诊断 ID

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DPXXX | Warning | ... | ... |

## 不变量

1. ...
2. ...

## 兼容基线

- netstandard2.0 / net8.0
- ...

## 不在范围内

- ...
```

### 4.5 变更门槛

- **新增 API** 或 **新增诊断 ID**：需 RFC → ADR → Spec 更新。
- **破坏性变更 API**：需 RFC → ADR → Spec 更新 + CHANGELOG `Breaking` 条目。
- **文档修正**（措辞、示例）：直接 PR，无需 RFC。

---

## 5. Design Doc — 设计文档

### 5.1 用途

记录模式的**实现细节**、设计权衡、已知局限、与生态的边界。Design Doc 是「实现」级别的文档——描述 **how** 和 **why**。

### 5.2 文件命名

```
docs/design/<PatternName>.md
```

与对应 Spec 同名。示例：`docs/design/Strategy.md`。

### 5.3 章节模板

```markdown
# Design Doc: <模式名>

> **关联 Spec**：[docs/spec/<PatternName>.md](../spec/<PatternName>.md)
> **关联 RFC**：[docs/rfc/XXX.md](../rfc/XXX.md)（如有）
> **关联 ADR**：ADR-XXX（如有）

## 概述

## 设计目标

## 实现概览

### 运行时
（关键类、数据结构、算法）

### 源生成器
（增量管线阶段、EquatableArray 结构）

### 诊断
（检测逻辑、报告位置）

## 设计权衡

### 选择了 X 而非 Y，因为...
（链接到 ADR）

## 与生态的边界

## 已知局限

## 参考
```

### 5.6 变更门槛

- 随代码 PR 同步更新，无需独立 RFC。
- 但若实现变更导致 Spec 契约变更，则需走 RFC 流程。

---

## 6. Roadmap

### 6.1 用途

维护 `docs/ROADMAP.md` 作为功能与技术 backlog 的滚动清单。

### 6.2 生命周期

```
候选 → 排期 → 进行中 → 已完成（归档）
                    ↘ 暂缓 / 明确不做
```

- **候选**：在 Roadmap 中登记，未排期。
- **排期**：分配到 FXX 阶段，准备启动。
- **进行中**：创建 RFC（如需要）+ Issue + 分支。
- **已完成**：移入 Roadmap「已完成（归档）」章节。
- **暂缓 / 明确不做**：移入对应章节，记录理由。

### 6.3 与其他文档的联动

| Roadmap 状态 | 关联文档 |
|--------------|----------|
| 排期 → 进行中 | 创建 RFC（如需要）→ Accepted → 产出 ADR |
| 进行中 → 已完成 | Spec 更新 + Design Doc 更新 + CHANGELOG 条目 + RFC 归档 |

---

## 7. Plan — 任务计划

### 7.1 载体

GitHub Issue 即 Plan。不新建文档文件。

### 7.2 Issue 结构

大型特性 Issue 应包含：

```markdown
## 目标
## 关联 RFC
## 步骤
- [ ] 子任务 1
- [ ] 子任务 2
## 验收标准
## 关联 PR
- #XXX
```

### 7.3 何时需要 Issue

- 所有 PR 必须关联一个 Issue（bug fix 可使用 `bug_report` 模板，功能使用 `feature_request` 模板）。
- 大型特性（跨多 PR）使用一个主 Issue + 子任务 checklist。

---

## 8. 目录结构

```
docs/
├── DOCUMENTATION.md              # 本文件 — 文档体系标准
├── README.md                     # 文档索引
├── DEVELOPMENT.md                # 开发手册
├── ROADMAP.md                    # 路线图
├── PUBLISHING.md                 # 发布流程
├── rfc/
│   ├── README.md                 # RFC 索引 + 状态板
│   ├── _template.md              # RFC 模板
│   ├── <ActiveRFC>.md            # 进行中的 RFC
│   └── archive/
│       └── <CompletedRFC>.md     # 已实现/已否决的 RFC
├── adr/
│   ├── README.md                 # ADR 索引
│   └── ADR-NNN-<title>.md        # 架构决策记录
├── spec/
│   ├── README.md                 # Spec 索引
│   ├── _template.md              # Spec 模板
│   └── <PatternName>.md          # 模式规范
├── design/
│   ├── README.md                 # Design Doc 索引
│   └── <PatternName>.md          # 模式设计文档
└── (横切约定文档保留在 docs/ 根目录)
    ├── FactoryKeyConventions.md
    ├── AppSettings.md
    ├── Autofac.md
    ├── Configuration.md
    └── PluginAssemblies.md
```

### 8.1 现有模式文档的迁移

现有 `docs/<PatternName>.md`（Strategy.md、Composite.md 等）将逐步拆分迁移至 `docs/spec/` + `docs/design/`：

| 现有文件 | 迁移目标 |
|----------|----------|
| `docs/Strategy.md` | `docs/spec/Strategy.md` + `docs/design/Strategy.md` |
| `docs/ChainOfResponsibility.md` | `docs/spec/ChainOfResponsibility.md` + `docs/design/ChainOfResponsibility.md` |
| `docs/Composite.md` | `docs/spec/Composite.md` + `docs/design/Composite.md` |
| `docs/FactoryRegistry.md` | `docs/spec/FactoryRegistry.md` + `docs/design/FactoryRegistry.md` |
| `docs/Decorator.md` | `docs/spec/Decorator.md` + `docs/design/Decorator.md` |
| `docs/EventAggregator.md` | `docs/spec/EventAggregator.md` + `docs/design/EventAggregator.md` |
| `docs/StateTransitionTable.md` | `docs/spec/StateTransitionTable.md` + `docs/design/StateTransitionTable.md` |

迁移遵循以下规则：
- **API 面、诊断 ID 表、不变量、兼容基线** → Spec
- **实现概览、设计权衡、与生态边界、已知局限、示例** → Design Doc
- 横切约定文档（`FactoryKeyConventions.md`、`AppSettings.md` 等）保留在 `docs/` 根目录，不迁移。
- 迁移可逐模式进行，不要求一次性完成。迁移时创建一个独立 PR。

---

## 9. 工作流程

### 9.1 新功能/新模式完整流程

```
1. Roadmap 候选 → 排期
2. 创建 RFC（Draft）→ 公开 Review
3. RFC Accepted → 产出 ADR → Roadmap 标记「进行中」
4. 创建 Issue（Plan）+ 子任务
5. 实现 → PR（随代码更新 Spec + Design Doc）
6. 合并 → RFC 标记 Implemented → 移入 archive/
7. CHANGELOG 条目 → Roadmap 标记「已完成」
```

### 9.2 Bug Fix 流程

```
1. Issue（bug_report）
2. PR（修复 + 测试 + Design Doc 更新如需要）
3. 合并 → CHANGELOG 条目
```

### 9.3 文档变更流程

```
1. Issue（说明文档变更内容与原因）
2. PR（文档修改）
3. 合并
```

### 9.4 Agent 工作流约定

AI 编码助手（Agent）在本仓库工作时，除遵守 `AGENTS.md` 的全部规则外，还须：

| 场景 | Agent 行为 |
|------|-----------|
| 新增诊断 ID | 必须先确认是否有对应 RFC；无则提示需创建 RFC |
| 修改公共 API | 必须先确认是否有对应 RFC + ADR；无则提示需创建 RFC |
| 创建 RFC | 使用 `docs/rfc/_template.md` 模板；frontmatter 从 `Draft` 开始 |
| 创建 ADR | 使用 `docs/adr/` 目录；编号取 `docs/adr/README.md` 中下一个可用编号 |
| RFC 状态变更 | 更新 frontmatter `状态` + `更新` 日期；归档时移动文件到 `archive/` |
| Spec 变更 | 确认关联 RFC 已 Accepted；同步更新 Spec 版本号 |
| Design Doc 变更 | 随代码 PR 同步更新 |
| ROADMAP 变更 | 完成项移入「已完成（归档）」；新增项放入对应阶段 |
| CHANGELOG | 在 `[Unreleased]` 下添加条目 |
| 文档目录 | 不在 `docs/` 之外创建文档文件（`.Local/` 除外，见 AGENTS.md） |

### 9.5 人类开发者工作流约定

人类开发者除遵守 `CONTRIBUTING.md` 的全部规则外，还须：

| 场景 | 开发者行为 |
|------|-----------|
| 发起 RFC | 在 `docs/rfc/` 创建文件，提交 PR 标记 `Draft` → `Review` |
| 评审 RFC | 在 RFC PR 的 Comments 中讨论；决策后更新状态为 `Accepted`/`Rejected` |
| 创建 ADR | RFC Accepted 后，在 `docs/adr/` 创建 ADR 文件，双向链接 |
| 归档 RFC | 实现合并后，将 RFC 移入 `archive/`，更新 `docs/rfc/README.md` |
| Spec 变更评审 | 确认 RFC + ADR 齐备后批准 Spec 更新 |
| Roadmap 维护 | 定期评审 Roadmap，更新状态 |

---

## 10. 文档与 Git 的关系

| 文档类型 | 提交方式 |
|----------|----------|
| RFC | 独立 PR（`docs: RFC <name>`）或随实现 PR |
| ADR | 随 RFC PR 或独立 PR（`docs: ADR-NNN <title>`） |
| Spec | 随实现 PR（与代码变更同一 PR） |
| Design Doc | 随实现 PR（与代码变更同一 PR） |
| Roadmap | 随实现 PR 或独立 PR |
| CHANGELOG | 随实现 PR |

### 10.1 PR 模板中的文档 checklist

PR 模板包含文档变更 checklist，提交者须勾选：

- [ ] 新增/变更公共 API → Spec 已更新
- [ ] 新增/变更诊断 ID → Spec 已更新 + `AnalyzerReleases.Unshipped.md` 已更新
- [ ] 新增/变更实现细节 → Design Doc 已更新
- [ ] 新增 RFC → 使用 `_template.md` 模板
- [ ] RFC 状态变更 → frontmatter 已更新 + 归档操作已完成
- [ ] CHANGELOG `[Unreleased]` 已添加条目

---

## 11. 文档质量检查清单

提交文档相关 PR 前，检查：

- [ ] 文件位置正确（RFC 在 `docs/rfc/`，ADR 在 `docs/adr/`，Spec 在 `docs/spec/`，Design Doc 在 `docs/design/`）
- [ ] Frontmatter 格式符合标准（字段完整、日期格式 `YYYY-MM-DD`）
- [ ] 章节结构符合模板
- [ ] 交叉链接有效（RFC ↔ ADR ↔ Spec ↔ Design Doc 双向链接）
- [ ] `docs/README.md` 索引已更新
- [ ] `docs/rfc/README.md` 状态板已更新（RFC 状态变更时）
- [ ] `docs/adr/README.md` 索引已更新（新增 ADR 时）
- [ ] 语言：内部文档中文为主，技术术语可用英文
- [ ] 无 AI/LLM 工具名称（遵守 `AGENTS.md` 隐私规则）
- [ ] 无私有工作区路径（遵守 `AGENTS.md` 隐私规则）

---

## 12. 参考

- [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
- [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
- [RFC Process (Rust)](https://github.com/rust-lang/rfcs) — RFC 生命周期参考
- [ADR (Michael Nygard)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) — ADR 概念起源
