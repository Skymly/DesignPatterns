# ADR-004: Core does not reference MSDI

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-14 |
| **关联 Issue** | —（项目创立时直接决策） |

## 背景

DesignPatterns 运行时库（`DesignPatterns/`）需要决定是否直接依赖 `Microsoft.Extensions.DependencyInjection`（MSDI）。直接引用可简化 DI 集成，但会强制所有消费者引入 MSDI 依赖。

## 决策

**Core 不引用 MSDI**。DI 集成归 `DesignPatterns.Extensions.DependencyInjection` 独立项目，消费者按需引用。Autofac 集成同理归 `DesignPatterns.Extensions.Autofac`。

## 后果

**正面**：
- Core 可在不使用 MSDI 的环境中使用（Autofac、其他容器、无容器）
- 消费者不被迫引入 MSDI 依赖
- DI 扩展可独立演进和发布

**负面**：
- 生成器需通过 `build/*.targets` MSBuild Import 机制在 DI 扩展项目中注册生成器路径
- DI 集成相关诊断（DP060–DP062）需放在 Analyzer 而非生成器中，因为 Analyzer 可引用 MSDI 类型

## 参考

- [AGENTS.md](../../AGENTS.md)「编码标准」章节
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)「架构原则」章节
- [docs/Autofac.md](../Autofac.md)
