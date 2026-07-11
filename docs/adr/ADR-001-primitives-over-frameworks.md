# ADR-001: Primitives over frameworks

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-14 |
| **关联 Issue** | —（项目创立时直接决策） |

## 背景

DesignPatterns 定位为技术探索库，需要选择运行时库的设计哲学：是提供厚重基类体系（abstract base class + inheritance hierarchy），还是提供轻量可组合的 primitives。

## 决策

采用 **primitives 而非厚重基类体系**：运行时库提供接口、特性、轻量工具类，用户通过组合而非继承使用。Core 不引入强制基类。

## 后果

**正面**：
- 用户可选择性采用，无需全量 commit 到框架
- 与现有 DI 容器、其他库兼容性好
- 编译期胶水（源生成器）可针对 primitives 做静态分析

**负面**：
- 无法提供「开箱即用」的一站式框架体验
- 用户需理解 primitives 的组合方式

## 参考

- [AGENTS.md](../../AGENTS.md)「项目是什么」章节
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)「架构原则」章节
