# 文档体系标准

> **权威源**。本文档定义本仓库**文档驱动开发（Documentation-Driven Development）**体系：所有文档的类型、结构、生命周期、归档规则，以及以文档为先导的开发流程。人类开发者和 AI 编码助手（Agent）均须遵守。`AGENTS.md`「文档体系」章节为本文档的精简摘要。
>
> - **核心原则**：**先文档后代码**——任何非琐碎变更，先确定它需要哪些文档、文档达到要求状态后才动代码（决策表见 [§11](#11-文档驱动开发流程)）。
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
| **Plan** | `docs/plans/`（大型）/ GitHub Issue（小型） | 任务计划（目标、步骤、验收） | 短生命周期 | 计划内自由更新 |
| **Review** | `docs/review/` | 评审记录（设计 / 实现 / 阶段回顾） | Final 后不可变 | 仅勾选行动项与修复链接 |

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

### 5.4 变更门槛

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

### 7.1 载体（双轨）

| 规模 | 载体 | 判断标准 |
|------|------|----------|
| **小型任务** | GitHub Issue | 单 PR 可完成（bug fix、单方法新增、文档修正） |
| **大型任务** | `docs/plans/<PascalCaseName>.md` + 主 Issue | 跨多 PR、跨多阶段、或由 RFC 衍生的实现计划 |

大型任务的 Plan 文档是**执行的唯一真相源**：目标、里程碑拆解、验收标准、进度状态都记录在文档中；主 Issue 仅作 GitHub 侧跟踪入口，链接到 Plan 文档。

### 7.2 文件命名

```
docs/plans/<PascalCaseName>.md
```

与关联 RFC 同名（如有）。示例：`docs/plans/HierarchicalStateMachine.md`。

### 7.3 Frontmatter 标准

```markdown
> **状态**：Active | Done | Cancelled
> **创建**：YYYY-MM-DD
> **更新**：YYYY-MM-DD
> **关联 RFC**：[docs/rfc/XXX.md](../rfc/XXX.md)（如有）
> **关联 Issue**：#XXX
> **关联 Roadmap**：FXX（如有）
```

### 7.4 正文章节模板

```markdown
# Plan: <标题>

> （frontmatter）

## 目标
一段话说明要交付什么。

## 非目标
本计划不覆盖的内容。

## 里程碑拆解

| 阶段 | 内容 | 模块（AGENTS.md 边界） | 状态 | PR |
|------|------|------------------------|------|-----|
| P1 | ... | Runtime | [ ] | — |
| P2 | ... | SourceGenerators | [ ] | — |

## 验收标准
- [ ] ...

## 风险与依赖

## 变更记录
计划执行中的重大调整（范围增减、顺序变更）在此追加，不删除原文。
```

### 7.5 生命周期与归档

```
Active → Done → archive/
       ↘ Cancelled → archive/（记录取消原因）
```

- 所有里程碑完成且验收标准全部满足 → 状态改 `Done`，移入 `docs/plans/archive/`。
- 中途取消 → 状态改 `Cancelled`，在「变更记录」中写明原因后归档。
- 小型任务的 Issue 不归档（GitHub close 即完成）。

### 7.6 何时需要 Issue

- 所有 PR 必须关联一个 Issue（bug fix 可使用 `bug_report` 模板，功能使用 `feature_request` 模板）。
- 大型特性（跨多 PR）使用一个主 Issue + `docs/plans/` 计划文档。

---

## 8. Review — 评审记录

### 8.1 用途

记录**结构化评审结论**，使评审意见可追溯、行动项可跟踪。区别于 PR 内的行内 code review（即时、随 PR 关闭而结束），Review 文档记录**跨 PR 或里程碑级**的评审。

### 8.2 何时需要 Review 文档

| 场景 | 需要 Review 文档？ |
|------|--------------------|
| RFC 进入 Review 阶段的设计评审 | ✅（评审结论落文档，RFC「决策记录」引用） |
| 大型 Plan 完成后的实现回顾 | ✅ |
| 发版前的 API 面 / 打包审查 | ✅ |
| 定期技术债 / 代码质量审查 | ✅ |
| 单 PR 的常规 code review | ❌ PR Comments 即可 |

### 8.3 文件命名

```
docs/review/<YYYY-MM-DD>-<kebab-case-topic>.md
```

示例：`docs/review/2026-07-08-composite-schema-validation-design.md`。

### 8.4 Frontmatter 标准

```markdown
> **状态**：Draft | Final
> **类型**：Design | Implementation | Release | Retrospective
> **日期**：YYYY-MM-DD
> **评审对象**：RFC / Plan / PR 范围 / 版本号
> **评审人**：维护者 / Agent（注明）
```

### 8.5 正文章节模板

```markdown
# Review: <标题>

> （frontmatter）

## 评审范围
评审了什么（文档、代码范围、版本）。

## 结论
通过 | 有条件通过 | 不通过，一句话概括。

## 发现

| # | 级别 | 发现 | 建议 |
|---|------|------|------|
| 1 | Blocker / Major / Minor / Nit | ... | ... |

## 行动项

- [ ] #1 → Issue #XXX / PR #XXX
- [ ] #2 → ...

## 参考
```

### 8.6 生命周期与归档

```
Draft → Final → （行动项全部关闭后）archive/
```

- **Final 后正文不可变**：新发现另起新 Review；仅允许勾选行动项 checkbox、补充 Issue/PR 链接。
- 行动项全部关闭后移入 `docs/review/archive/`。
- 级别定义：**Blocker**（不修复不得合并/发版）、**Major**（须建 Issue 跟踪）、**Minor**（建议修复）、**Nit**（可忽略）。

---

## 9. 归档机制（统一规则）

各类型文档共用同一套归档纪律：

| 类型 | 归档目录 | 归档触发 | 归档后可改动 |
|------|----------|----------|--------------|
| RFC | `docs/rfc/archive/` | Implemented / Rejected / Superseded | 仅修失效链接 |
| Plan | `docs/plans/archive/` | Done / Cancelled | 仅修失效链接 |
| Review | `docs/review/archive/` | Final 且行动项全部关闭 | 仅修失效链接 |
| ADR | 不移动（原地 Supersede） | — | 仅状态字段改 `Superseded by ADR-XXX` |
| Spec / Design Doc | 不归档（活文档） | 模式被移除时随代码删除 | 随实现演进 |
| Roadmap 条目 | `ROADMAP.md`「已完成（归档）」章节 | 功能落地 | 仅追加 |

通用规则：

1. **归档 = 移动文件 + 更新状态字段 + 更新对应 README 索引**，三者必须在同一 PR 完成。
2. 归档文件**正文不再修改**（唯一例外：修正失效链接）。
3. 归档不删除——历史决策与讨论过程是资产；需要检索时从各目录 README 的索引进入。
4. 引用归档文件时使用 `archive/` 路径；归档时须全仓搜索旧路径并修正引用。

---

## 10. 目录结构

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
├── plans/
│   ├── README.md                 # Plan 状态板
│   ├── _template.md              # Plan 模板
│   ├── <ActivePlan>.md           # 进行中的大型任务计划
│   └── archive/
│       └── <DonePlan>.md         # 已完成/已取消的计划
├── review/
│   ├── README.md                 # Review 索引
│   ├── _template.md              # Review 模板
│   ├── <YYYY-MM-DD>-<topic>.md   # 行动项未关闭的评审
│   └── archive/
│       └── <ClosedReview>.md     # 行动项全部关闭的评审
└── (横切约定文档保留在 docs/ 根目录)
    ├── FactoryKeyConventions.md
    ├── AppSettings.md
    ├── Autofac.md
    ├── Configuration.md
    └── PluginAssemblies.md
```

### 10.1 现有模式文档的迁移（已完成）

所有 7 个模式文档已从 `docs/<PatternName>.md` 拆分迁移至 `docs/spec/` + `docs/design/`：

| 原文件 | Spec | Design Doc |
|--------|------|------------|
| `docs/Strategy.md` | `docs/spec/Strategy.md` | `docs/design/Strategy.md` |
| `docs/ChainOfResponsibility.md` | `docs/spec/ChainOfResponsibility.md` | `docs/design/ChainOfResponsibility.md` |
| `docs/Composite.md` | `docs/spec/Composite.md` | `docs/design/Composite.md` |
| `docs/FactoryRegistry.md` | `docs/spec/FactoryRegistry.md` | `docs/design/FactoryRegistry.md` |
| `docs/Decorator.md` | `docs/spec/Decorator.md` | `docs/design/Decorator.md` |
| `docs/EventAggregator.md` | `docs/spec/EventAggregator.md` | `docs/design/EventAggregator.md` |
| `docs/StateTransitionTable.md` | `docs/spec/StateTransitionTable.md` | `docs/design/StateTransitionTable.md` |

迁移遵循以下规则：
- **API 面、诊断 ID 表、不变量、兼容基线** → Spec
- **实现概览、设计权衡、与生态边界、已知局限、示例** → Design Doc
- 横切约定文档（`FactoryKeyConventions.md`、`AppSettings.md` 等）保留在 `docs/` 根目录，不迁移。
- 旧文件已删除。

---

## 11. 文档驱动开发流程

**先文档后代码**：动手写代码前，先按下表判定变更所需的文档前置条件；前置文档未达到要求状态，不进入实现阶段。人类开发者与 Agent 一体遵守。

### 11.1 变更类型 → 文档前置条件决策表

| 变更类型 | RFC | ADR | Plan | Review | 实现 PR 须同步 |
|----------|-----|-----|------|--------|----------------|
| 新增模式（生成器 + 诊断） | ✅ 必须 Accepted | ✅ RFC 衍生 | ✅ `docs/plans/` | ✅ 设计评审 | Spec 新建 + Design Doc 新建 + CHANGELOG |
| 破坏性公共 API 变更 | ✅ 必须 Accepted | ✅ | 视规模 | ✅ 设计评审 | Spec + CHANGELOG `Breaking` |
| 新增诊断 ID | ✅ 必须 Accepted | ✅ | 视规模 | 建议 | Spec + `AnalyzerReleases.Unshipped.md` + CHANGELOG |
| 非破坏性 API 新增（单模块） | ❌ | ❌ | ❌（Issue 即可） | ❌ | Design Doc + CHANGELOG |
| Bug fix | ❌ | ❌ | ❌（Issue 即可） | ❌ | CHANGELOG（如用户可见） |
| 重构（无行为变更） | ❌ | 视架构影响 | ❌ | ❌ | Design Doc（如实现结构变化） |
| 发版 | ❌ | ❌ | ❌ | ✅ Release 审查 | CHANGELOG 版本化 |

### 11.2 新功能/新模式完整流程

```
1. Roadmap 候选 → 排期
2. 创建 RFC（Draft）→ 公开 Review
3. 设计评审 → Review 文档（Final）→ RFC Accepted → 产出 ADR → Roadmap 标记「进行中」
4. 创建 Plan（docs/plans/，跨多 PR 时）+ 主 Issue
5. 按 Plan 里程碑实现 → 每个 PR 随代码更新 Spec + Design Doc
6. 全部合并 → RFC 标记 Implemented → 移入 rfc/archive/
7. Plan 标记 Done → 移入 plans/archive/；可选实现回顾 Review
8. CHANGELOG 条目 → Roadmap 标记「已完成」
```

### 11.3 Bug Fix 流程

```
1. Issue（bug_report）
2. PR（修复 + 测试 + Design Doc 更新如需要）
3. 合并 → CHANGELOG 条目
```

### 11.4 文档变更流程

```
1. Issue（说明文档变更内容与原因）
2. PR（文档修改）
3. 合并
```

### 11.5 发版流程（文档侧）

```
1. Release Review（docs/review/，检查 API 面 / CHANGELOG / 打包）
2. Blocker 行动项清零
3. 按 docs/PUBLISHING.md 执行发版
4. CHANGELOG [Unreleased] → 版本化章节
```

### 11.6 Agent 工作流约定

AI 编码助手（Agent）在本仓库工作时，除遵守 `AGENTS.md` 的全部规则外，还须：

| 场景 | Agent 行为 |
|------|-----------|
| 新增诊断 ID | 必须先确认是否有对应 RFC；无则提示需创建 RFC |
| 修改公共 API | 必须先确认是否有对应 RFC + ADR；无则提示需创建 RFC |
| 接到跨多 PR 的大型任务 | 先确认 `docs/plans/` 是否有对应 Plan；无则先建 Plan（经用户确认）再实现 |
| 创建 RFC | 使用 `docs/rfc/_template.md` 模板；frontmatter 从 `Draft` 开始 |
| 创建 ADR | 使用 `docs/adr/` 目录；编号取 `docs/adr/README.md` 中下一个可用编号 |
| 创建 Plan | 使用 `docs/plans/_template.md`；里程碑对齐 AGENTS.md 单模块 PR 边界 |
| 创建 Review | 使用 `docs/review/_template.md`；评审人注明为 Agent |
| RFC / Plan / Review 状态变更 | 更新 frontmatter `状态` + 日期；归档时移动文件到对应 `archive/` 并更新 README 索引 |
| Spec 变更 | 确认关联 RFC 已 Accepted；同步更新 Spec 版本号 |
| Design Doc 变更 | 随代码 PR 同步更新 |
| ROADMAP 变更 | 完成项移入「已完成（归档）」；新增项放入对应阶段 |
| CHANGELOG | 在 `[Unreleased]` 下添加条目 |
| 文档目录 | 不在 `docs/` 之外创建文档文件（`.Local/` 除外，见 AGENTS.md） |

### 11.7 人类开发者工作流约定

人类开发者除遵守 `CONTRIBUTING.md` 的全部规则外，还须：

| 场景 | 开发者行为 |
|------|-----------|
| 发起 RFC | 在 `docs/rfc/` 创建文件，提交 PR 标记 `Draft` → `Review` |
| 评审 RFC | 在 RFC PR 的 Comments 中讨论；重大设计评审落 `docs/review/` 文档；决策后更新状态为 `Accepted`/`Rejected` |
| 创建 ADR | RFC Accepted 后，在 `docs/adr/` 创建 ADR 文件，双向链接 |
| 启动大型任务 | 创建 `docs/plans/` 计划文档 + 主 Issue |
| 归档 RFC / Plan / Review | 达到归档条件后移入对应 `archive/`，更新 README 索引 |
| Spec 变更评审 | 确认 RFC + ADR 齐备后批准 Spec 更新 |
| Roadmap 维护 | 定期评审 Roadmap，更新状态 |

---

## 12. 文档与 Git 的关系

| 文档类型 | 提交方式 |
|----------|----------|
| RFC | 独立 PR（`docs: RFC <name>`）或随实现 PR |
| ADR | 随 RFC PR 或独立 PR（`docs: ADR-NNN <title>`） |
| Spec | 随实现 PR（与代码变更同一 PR） |
| Design Doc | 随实现 PR（与代码变更同一 PR） |
| Plan | 独立 PR（`docs: plan <name>`）；状态更新可随实现 PR |
| Review | 独立 PR（`docs: review <topic>`） |
| Roadmap | 随实现 PR 或独立 PR |
| CHANGELOG | 随实现 PR |

### 12.1 PR 模板中的文档 checklist

PR 模板包含文档变更 checklist，提交者须勾选：

- [ ] 新增/变更公共 API → Spec 已更新
- [ ] 新增/变更诊断 ID → Spec 已更新 + `AnalyzerReleases.Unshipped.md` 已更新
- [ ] 新增/变更实现细节 → Design Doc 已更新
- [ ] 新增 RFC → 使用 `_template.md` 模板
- [ ] RFC / Plan / Review 状态变更 → frontmatter 已更新 + 归档操作已完成
- [ ] CHANGELOG `[Unreleased]` 已添加条目

---

## 13. 文档质量检查清单

提交文档相关 PR 前，检查：

- [ ] 文件位置正确（RFC 在 `docs/rfc/`，ADR 在 `docs/adr/`，Spec 在 `docs/spec/`，Design Doc 在 `docs/design/`，Plan 在 `docs/plans/`，Review 在 `docs/review/`）
- [ ] Frontmatter 格式符合标准（字段完整、日期格式 `YYYY-MM-DD`）
- [ ] 章节结构符合模板
- [ ] 交叉链接有效（RFC ↔ ADR ↔ Spec ↔ Design Doc ↔ Plan ↔ Review 双向链接）
- [ ] `docs/README.md` 索引已更新
- [ ] `docs/rfc/README.md` 状态板已更新（RFC 状态变更时）
- [ ] `docs/adr/README.md` 索引已更新（新增 ADR 时）
- [ ] `docs/plans/README.md` 状态板已更新（Plan 状态变更时）
- [ ] `docs/review/README.md` 索引已更新（Review 状态变更时）
- [ ] 语言：内部文档中文为主，技术术语可用英文
- [ ] 无 AI/LLM 工具名称（遵守 `AGENTS.md` 隐私规则）
- [ ] 无私有工作区路径（遵守 `AGENTS.md` 隐私规则）

---

## 14. 参考

- [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
- [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
- [RFC Process (Rust)](https://github.com/rust-lang/rfcs) — RFC 生命周期参考
- [ADR (Michael Nygard)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) — ADR 概念起源
