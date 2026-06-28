# NuGet 发布流程

元包 **`Skymly.DesignPatterns`**（`DesignPatterns.Package/`）通过 GitHub Actions 与 Nuke `Publish` 目标发布。版本号权威源：[`Directory.Build.props`](../Directory.Build.props) 的 `VersionPrefix` / `VersionSuffix`；发版前须与 [`CHANGELOG.md`](../CHANGELOG.md) 一致。

## 包 ID 与渠道

| 项 | 值 |
|----|-----|
| **PackageId** | `Skymly.DesignPatterns` |
| **nuget.org** | 需仓库 secret `NUGET_API_KEY` |
| **GitHub Packages** | 使用 `GITHUB_TOKEN`（workflow 已注入） |

旧 ID `DesignPatterns`（仅 GitHub Packages 的 preview1/2）已弃用；新消费方请使用 nuget.org 上的 **`Skymly.DesignPatterns`**。

## 预览版发版（当前常态）

1. 在 `main` 上完成变更，更新 [`CHANGELOG.md`](../CHANGELOG.md)（`[Unreleased]` → 新版本节，Keep a Changelog 格式，**英语**）。
2. 更新 [`Directory.Build.props`](../Directory.Build.props)：`VersionPrefix` / `VersionSuffix`（例如 `0.2.0` + `preview2`）。
3. 本地校验：

```powershell
./build.ps1 --target CiPack --configuration Release
```

`CiPack` = 单元测试 + pack + 本地 NuGet 消费者 smoke（[`eng/nuget-smoke/MetaPackage.Consumer/`](../eng/nuget-smoke/MetaPackage.Consumer/)）。

4. 提交并 push 到 `main`。
5. 打 tag 并推送（tag 名 **`v` + 完整 SemVer**，与包版本一致）：

```powershell
git tag v0.2.0-preview2
git push origin v0.2.0-preview2
```

6. [`.github/workflows/release.yml`](../.github/workflows/release.yml) 在 tag `v*` push 时触发，执行 Nuke `Publish`（依赖 `Test` + `PackVerify`）。
7. 预览版**不**创建 GitHub Release；可在 Actions 摘要与 artifact 中确认 `.nupkg`。
8. （可选）对已发布包做远端 smoke：

```powershell
./build.ps1 --target NuGetConsumerSmokePublished --consumer-feed Published --configuration Release
```

## 手动触发

workflow 支持 `workflow_dispatch`，输入 `version`（如 `0.2.0-preview2`）。仍须保证该版本与 tag / `Directory.Build.props` 策略一致；推荐以 **tag push** 为唯一发版入口。

## 稳定版（未来）

当 API 稳定并离开 preview 后缀时：

- 去掉 `VersionSuffix`，按 SemVer 发 `v1.0.0` 等。
- 可在同一 workflow 上扩展 GitHub Release 步骤（当前未启用）。
- [`README.md`](../README.md)「项目状态」与 DesignPatterns.Docs 安装说明同步更新。

## 发版前检查清单

- [ ] `CHANGELOG.md` 已写入本版本条目（英语）
- [ ] `Directory.Build.props` 版本与 tag 一致
- [ ] `./build.ps1 --target CiPack` 通过
- [ ] 诊断 / 公共 API 变更已同步 DesignPatterns.Docs
- [ ] 产品仓 commit / PR **不**提及 AI 工具（见 [`../AGENTS.md`](../AGENTS.md) commit 纪律）

## 相关

- Nuke 目标定义：[`build/Program.cs`](../build/Program.cs)（`Publish`、`CiPack`、`NuGetConsumerSmoke`）
- CI（非发布）：[`.github/workflows/ci.yml`](../.github/workflows/ci.yml)
- 开发环境总览：[DEVELOPMENT.md](DEVELOPMENT.md)
