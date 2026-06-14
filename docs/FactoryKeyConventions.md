# Key 命名约定 — `[RegisterFactory]` / `[RegisterStrategy]` / `[HandlerOrder]`

本约定统一**生成器管理的注册表**的 key 选型与命名,确保消费方在跨项目、跨模块调用 key 时**不漂移**。它不是新功能,只是把现有 `[RegisterFactory]` / `[RegisterStrategy]` / `[HandlerOrder]` 的隐含规则显式化,供生成器产物(`{Contract}Keys`)与人工 Builder 在同一语境下协作。

> 范围: 字符串 key(`[RegisterFactory("...")]` / `[RegisterStrategy("...")]`)。**不**覆盖 enum / Type / int 等强类型 key(当前生成器尚不支持;若引入会另开文档)。

---

## 1. 基本规则

| 维度 | 规则 | 说明 |
|------|------|------|
| **大小写** | 推荐 `kebab-case` 或 `lower_snake_case` | DP025 字面量键校验对**精确字面量**做匹配;大小写不一致会被判为「未知 key」。`kebab-case` 视觉分组最稳 |
| **字符集** | 仅 `[a-z0-9_-]` | 避免空白、点、斜杠、反斜杠、Unicode(防止 JSON 序列化 / 文件名 / 环境变量传参时出错) |
| **前缀** | 推荐按**业务域 / 子系统**加 `2-4` 字符前缀 | 防止不同模块用同一个扁平 key 空间互踩(如 `format` vs `payment` 的 `refund`) |
| **不可变** | 一旦 `[RegisterFactory]` 标注,key 即成该实现的**公共契约**;改 key 等于改公共 API | 改 key 视作 breaking change,按 SemVer 处理 |
| **去重** | 同一契约下不可重复(DP020/DP003 等由生成器 Error 拦下) | Builder 手动 `Register` 时抛 `ArgumentException` |

> 例:`"format:json"`、`"image:convert"`、`"http-client-retry"`、`"auth-oauth2-callback"`(前缀 `auth-` 区分其它子系统)。

---

## 2. 复合 key(outer / inner)

当业务天然有两层结构(「按工具找能力」、「按租户找策略」、「按路由找处理器」),**不要**把两层拍扁成一个 key,也不要在生成器层做 native 二维 attribute — 而是用**单层 key + Consumer 侧分组适配器**(下文 § 4)。

### 推荐形态:`"{outer}:{inner}"`

```csharp
[RegisterFactory<ITextFormatter>("format:json")]
public sealed class JsonTextFormatter : ITextFormatter { ... }

[RegisterFactory<ITextFormatter>("format:xml")]
public sealed class XmlTextFormatter : ITextFormatter { ... }

[RegisterFactory<IImageCapability>("image:convert")]
public sealed class ConvertCapability : IImageCapability { ... }
```

- 单一契约 `IAnyToolCapability`(或等价根接口)
- 单层 key,生成器照常工作(DP020 / DP025 校验照常生效)
- 消费方在 `Internal/CapabilityOuterRegistry`(类似形态)按 `:` 切前缀分组

### 为什么不直接做二维 attribute

| 备选 | 缺点 |
|------|------|
| `[RegisterFactory<Outer, Inner, T>]` | 破坏现有 `{Contract}Keys` / `{Contract}Registry` 形态;所有 SG 路径要重写;DP026 占用 |
| 嵌套 `IFactoryRegistry<string, IFactoryRegistry<string, T>>` | 失去单一 key 字面量校验;DP025 无法作用在内层;Builder API 不一致 |
| 多契约注册(`[RegisterFactory<ITextFormatter>] [RegisterFactory<IImageCapability>]` 写在同一类) | 一个实现承担多身份,与"一个类一个职责"冲突;SG 难以去重 |

> **底线**: 库的二维概念不进入公共 API。Consumer 用单层 key + 分组适配器拼出二维;**库侧零改动**。

---

## 3. 案例:AnyTool 二维 capability

AnyTool 现有 `PluginRegistry._capabilitiesByToolAndContract`(二维 dict)用 `toolId`(外)+ `capabilityId`(内)。改用本约定:

| 维度 | 改前 | 改后 |
|------|------|------|
| 契约 | `ITextFormatter` / `IImageCapability`(两个根) | `IAnyToolCapability`(一个根) |
| Key | `toolId` 与 `capabilityId` 各自一个字段 | `"format:json"` / `"image:convert"` 单层 |
| 分组 | AnyTool 自己写二维 dict | SG 产 `{Capability}Keys` / `{Capability}Registry`,AnyTool 写 `Internal/CapabilityOuterRegistry` 按 `:` 切前缀 |

参考完整方案:[`#100`](https://github.com/Skymly/DesignPatterns/issues/100) 与 [AnyTool#1](https://github.com/Skymly/AnyTool/issues/1)。

---

## 4. Consumer 侧分组适配器模板

不强制形态,但**强烈推荐**放在 `Internal/` 命名空间、标 `internal`,避免污染 Consumer 公共 API:

```csharp
namespace AnyTool.Core.Plugins.Internal;

internal sealed class CapabilityOuterRegistry
{
    private readonly IReadOnlyDictionary<string, IFactoryRegistry<string, IAnyToolCapability>> _byTool;

    public CapabilityOuterRegistry(IFactoryRegistry<string, IAnyToolCapability> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _byTool = inner.Keys
            .GroupBy(ExtractToolId, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(
                g => g.Key,
                g => BuildSubRegistry(inner, g),
                StringComparer.OrdinalIgnoreCase);
    }

    public IFactoryRegistry<string, IAnyToolCapability>? TryGet(string toolId) =>
        _byTool.TryGetValue(toolId, out var sub) ? sub : null;

    private static string ExtractToolId(string key)
    {
        var i = key.IndexOf(':');
        return i < 0 ? key : key[..i];
    }

    private static IFactoryRegistry<string, IAnyToolCapability> BuildSubRegistry(
        IFactoryRegistry<string, IAnyToolCapability> inner, IGrouping<string, string> keys)
    {
        var builder = new FactoryRegistryBuilder<string, IAnyToolCapability>();
        foreach (var k in keys)
        {
            builder.Register(k, _ => inner.Create(k));
        }
        return builder.Build();
    }
}
```

要点:
- 接收**已构造**的 `IFactoryRegistry`,不参与 Builder 链
- 分组逻辑在 ctor 一次性完成;之后 `TryGet` 是 O(1)(net8.0 上 `FrozenDictionary` 优化)
- 不写 SG、不改库;`netstandard2.0` 退化为 `Dictionary`

---

## 5. 与 DP025 的关系

[DP025](https://github.com/Skymly/DesignPatterns/blob/main/DesignPatterns.Diagnostics/DiagnosticIds.cs) 校验生成器管理的注册表(`IStrategyRegistry` / `IFactoryRegistry`)调用点的**字面量字符串 key**;常量字符串(由 `{Contract}Keys` 提供)始终放行,任意裸字符串若与已知 key 不匹配会触发 Info 级诊断并提供「最近键」CodeFix。

复合 key `"format:json"` 同样受 DP025 保护 — 它就是一个普通字符串,生成器把它放进 `{Capability}Keys.FormatJson` 常量,DP025 校验照常工作。**外层前缀**(`"format"`)是 Consumer 解析的细节,生成器/Analyzer 不可见。

---

## 6. 与 HandlerOrder / Decorator 的关系

| 特性 | key 维度 | 命名规则 |
|------|----------|----------|
| `[RegisterFactory]` | 字符串(常带 `:` 分组) | 本约定 |
| `[RegisterStrategy]` | 字符串(常带 `:` 分组) | 同上,DP006 + DP025 同样保护 |
| `[HandlerOrder]` | **整数 Order**(非字符串) | 见 [`ChainOfResponsibility.md`](ChainOfResponsibility.md);**不**适用本约定 |
| `[Decorator]` / `[CompositePart]` | 整数 Order(同 Handler) | 同上 |
| `[GenerateSingleton]` | 无 key(单例) | 不适用 |

---

## 7. 不做

- **不做原生二维 key attribute**(`[RegisterFactory<Outer, Inner, T>]`)。见 § 2 表。
- **不做 enum key**。当前生成器仅接受 `string`;若要 enum,需扩 `RegisterFactoryAttribute` + SG,经 F2/F3 评审。
- **不做运行时 key 重命名**。改 key 即改公共 API,人工迁移。
- **不做自动前缀注入**。前缀由 Consumer 显式写出,避免生成器越权改 key。

---

## 8. 验证清单(PR 自检)

新增 / 修改 `[RegisterFactory]` / `[RegisterStrategy]` 标注时,过一遍:

- [ ] key 全小写 + 数字 + `-` 或 `_`,无其它字符
- [ ] key 含业务前缀(跨模块时)
- [ ] 复合语义用 `:` 分组(若有 outer/inner 概念)
- [ ] 改 key = 改公共 API = PR 描述里点出 breaking
- [ ] DP025 不在调用点报「未知 key」(调用方用 `{Contract}Keys.*` 常量)
- [ ] `Directory.Build.props` `TreatWarningsAsErrors=true` 通过(零警告)
- [ ] 单测覆盖:重名 key 抛错 / key 大小写不敏感 / 复合 key 前缀分组正确

---

## 9. 参考

- [`FactoryRegistry.md`](FactoryRegistry.md) — `[RegisterFactory]` SG 产出与诊断
- [`Strategy.md`](Strategy.md) — `[RegisterStrategy]` SG 产出与诊断
- [`AGENTS.md`](../AGENTS.md) § 诊断 ID 规范 — DP020–DP025 与 DP026 下一个可用 ID
- [`ROADMAP.md`](../ROADMAP.md) — F1/F2/F3 路线图
- [#100](https://github.com/Skymly/DesignPatterns/issues/100) — AnyTool 二维 capability 讨论
