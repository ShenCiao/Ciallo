# Facepunch.Steamworks 三端 NuGet 集成计划

## 目标
参考C:\dev\godot的 CI nuget包分发技术
建立一个公开、可复现的三端 NuGet 包，供 Ciallo 通过单条 `PackageReference` 使用。Facepunch 源码保持上游内容，版本同步由维护者手动完成；fork 只增加发布所需的 CI 和打包模板。

公开包：

- 仓库：`ShenCiao/Facepunch.Steamworks`
- NuGet ID：`ShenCiao.Facepunch.Steamworks`
- 首个版本：`2.5.2-ciallo`
- Feed：`https://shenciao.github.io/Facepunch.Steamworks/index.json`
- 支持 RID：`win-x64`、`linux-x64`、`osx-arm64`
- 托管目标：`net6.0`，兼容 Ciallo 当前的 `net10.0`

Steamworks 原生库的公开再分发许可已由项目负责人确认；包中保留 Facepunch MIT 许可、Valve redistributable 许可说明和上游来源信息。

## Fork 与打包

在 fork 中保留上游项目结构和源码，不修改 `Win64.csproj`、`Posix.csproj` 及其源码。新增内容仅位于 `.github/`：

- `.github/workflows/publish-nuget.yml`：手动发布入口。
- `.github/packaging/package.props`：包 ID、版本、上游 commit 和元数据。
- `.github/packaging/package.targets`：消费端平台选择和原生库复制规则。
- `.github/packaging/LICENSES.md` 与 `README.md`：许可和来源说明。

CI 使用上游项目构建：

- Windows 使用 `Facepunch.Steamworks.Win64.csproj`。
- Linux/macOS 使用 `Facepunch.Steamworks.Posix.csproj`。
- Windows 原生库来自 `steam_api64.dll`。
- Linux 原生库来自 `libsteam_api.so`。
- macOS 原生库来自 universal `libsteam_api.dylib`。

NuGet 包使用标准 `runtimes/<rid>/native` 布局，并通过 `buildTransitive` targets：

- 按显式 `RuntimeIdentifier` 选择程序集；没有 RID 时按当前主机平台选择。
- 将正确的 Win64 或 Posix 程序集加入引用。
- 将对应原生库复制到 build、publish 和 Godot 导出输出。
- 对未支持的平台在解析引用阶段失败。

包不把 Win32 作为 Ciallo 支持目标，也不包含 Debug、Unity 或无关架构产物。

## 发布 CI

`publish-nuget.yml` 只启用 `workflow_dispatch`，不创建定时同步器和自动上游更新器。输入只有：

- `upstream_tag`：要发布的上游稳定 tag，例如 `2.5.2`。
- `force_republish`：默认 `false`，仅用于人工确认后的同版本覆盖。

工作流步骤：

1. Checkout 手动同步后的 fork 分支，并验证当前源码对应 `upstream_tag`。
2. 在 Windows、Linux、macOS runner 构建托管程序集并上传构建产物。
3. 组装 `ShenCiao.Facepunch.Steamworks` nupkg，版本由 `upstream_tag` 自动生成：`<tag>-ciallo`。
4. 在三端创建临时消费者项目，Restore、Build、Publish，并加载对应托管程序集和原生库。
5. 校验 nupkg 文件列表、许可文件、上游 commit、包 ID、版本和 SHA-256。
6. 使用 Sleet 发布到独立 `gh-pages` 静态 NuGet feed，并部署 GitHub Pages。

发布约束：

- 使用固定版本的 Sleet 工具和固定 major 版本的 GitHub Actions。
- 使用 `facepunch-nuget-publish` concurrency group，串行更新 feed。
- 正常发布不使用 `--force`。
- feed 已存在相同版本且 SHA-256 相同：报告成功并跳过上传，保证重试幂等。
- feed 已存在相同版本但 SHA-256 不同：默认失败，不静默替换。
- 只有 `force_republish=true` 才允许覆盖；工作流必须输出旧、新 SHA-256，并在摘要中提示开发者清理 NuGet 全局缓存和 CI 缓存。

同版本覆盖的风险是已知且受控的：NuGet 按 ID+版本缓存包，GitHub Pages/CDN 也可能短时间缓存旧内容；无条件覆盖会造成相同 commit 得到不同二进制、开发机与 CI 结果不一致、旧缓存难以撤回。因此相同版本只用于完全相同包的幂等重试，内容变化必须由维护者显式强制发布并记录。

## Ciallo 接入

修改 Ciallo 根目录 `NuGet.Config`：

- 增加 `shenciao-facepunch` 源，指向独立 feed。
- 使用 package source mapping 将 `ShenCiao.Facepunch.Steamworks` 精确映射到该源。
- 保留现有 Godot 源和 `nuget.org` 源。

在 `Ciallo/Ciallo.csproj` 增加精确版本引用：

```xml
<PackageReference Include="ShenCiao.Facepunch.Steamworks"
                  Version="[2.5.2-ciallo]" />
```

Ciallo 不再维护 Facepunch submodule，不复制 Steamworks 原生库；包的 targets 负责构建输出，现有 Godot 导出流程负责将输出纳入最终包。

## 验收测试

- 干净环境仅配置 NuGet feed 后可以 Restore Ciallo。
- Windows 构建输出包含 `Facepunch.Steamworks.Win64.dll` 和 `steam_api64.dll`。
- Linux 构建输出包含 `Facepunch.Steamworks.Posix.dll` 和 `libsteam_api.so`。
- macOS 构建输出包含 `Facepunch.Steamworks.Posix.dll` 和 `libsteam_api.dylib`。
- 每个平台的 smoke test 能加载对应原生库；不调用需要 Steam 客户端、App ID 或网络服务的初始化流程。
- Ciallo 的 Windows、Linux、macOS Godot export 全部成功，且导出产物不混入其他平台的托管程序集或原生库。
- 同版本同内容的 workflow 重试不产生新的 feed 内容。
- 同版本不同内容的 workflow 在默认设置下失败；显式强制覆盖时记录前后 SHA-256。
- 手动同步新的上游稳定 tag 后，只需传入新的 `upstream_tag` 即可生成对应的 `<tag>-ciallo` 包。

## 操作约定

1. 维护者手动将上游稳定 tag 同步到 fork 发布分支。
2. 在 fork 上手动触发 `publish-nuget.yml`。
3. CI 三端验证通过后发布 feed。
4. Ciallo 单独提交版本号更新 PR，并运行项目级 Restore、Build 和三端导出。
