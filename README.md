# DesignPatterns

面向 .NET 的**设计模式工具库**：提供可组合的运行时 primitives，并通过 Roslyn（源生成器、分析器、代码补全）减少样板代码、在编译期引导正确用法。

## 目标

- 帮助开发者在真实项目中**更快、更一致**地应用常见设计模式
- **组合优于继承**：轻量接口与扩展，而非厚重基类体系
- **编译期与运行时协同**：属性标记 + 源生成 + 诊断，而非魔法框架

## 仓库结构（规划）

```
DesignPatterns.slnx
├── DesignPatterns/                 # 运行时核心（netstandard2.0+）
├── DesignPatterns.Analyzers/       # 源生成器、诊断、CodeFix、Completion（规划中）
├── tests/                          # 单元测试与 Roslyn 快照测试（规划中）
├── samples/                        # 按模式划分的示例（规划中）
└── docs/                           # 开发与设计文档
```

## 文档

| 文档 | 说明 |
|------|------|
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | 环境、构建、测试、贡献流程、架构约定 |
| [AGENTS.md](AGENTS.md) | 供 AI 编码助手使用的项目上下文与操作指南 |

## 快速开始

```bash
dotnet build DesignPatterns.slnx
```

## 许可证

待定（贡献前请在 Issue 中讨论许可证选择）。
