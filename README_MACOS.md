# 苹果派 AppuruPie · macOS 跨平台改版

> 本文件描述**这个 fork 自己的状态**：把 Windows 版鼠标轮盘重做成跨平台形态，并在 Apple Silicon macOS 上真跑起来。
> 上游 Windows 版（原 `StarPie`）的完整功能介绍请看 [README.md](README.md) / [README_EN.md](README_EN.md)。
> 深度依据在 [`ARCHITECTURE.md`](ARCHITECTURE.md)（架构与进度快照）、[`PORTING_FEASIBILITY.md`](PORTING_FEASIBILITY.md)（可行性 12 问）、[`PORTING_REFERENCE.md`](PORTING_REFERENCE.md)（能力/许可证审计）。

**English:** [README_MACOS_EN.md](README_MACOS_EN.md)

---

## 一句话

**同一份业务代码，Windows 上继续跑 WPF，macOS 上跑 Avalonia + 原生 CGEventTap —— 而老工程一行没改。**

苹果派不是"把 WPF 翻译成 Avalonia"。它的做法是把轮盘、手势、配置、动作这些**产品逻辑**沉到一个平台中立的 `StarPie.Core`，
让 macOS 侧只需要重写"和操作系统打交道"的那一层。Windows 的 WPF 工程通过 `<Compile Link>` 引用**同一批磁盘文件**，
所以两边共用逻辑不是靠复制粘贴维持一致的。

---

## 现在能跑什么（2026-10-03 本机实测）

| 门禁 | 命令 | 实测结果 |
| :--- | :--- | :--- |
| 构建 | `dotnet build StarPie.sln -c Release` | **0 警告 0 错误** |
| 端到端自检 | `dotnet src/StarPie.App/bin/Release/net8.0/StarPie.dll --selftest` | **144 条 PASS → `SELFTEST APPROVED`**（rc=0） |
| 并集护栏 | `python3 tools/check_system_presets.py` | `[OK]`（Windows 63 个 case 标签 ↔ Core 63 ↔ 枚举 42 ↔ macOS 42 全登记） |
| 打包 | `sh tools/bundle-macos.sh` | 产出 `AppuruPie.app/`，ad-hoc 签名，`codesign --verify --deep --strict` rc=0 |
| 老工程回归面 | `git status WinPieGestures plugin/sdk` | **无任何修改行** |

规模（前两行为本次现测；后两行取自 `PORTING_FEASIBILITY.md` 的度量基线，我未独立复测去重口径）：

| 指标 | 值 |
| :--- | :--- |
| `StarPie.Core` 编译单元 | **69 个 / 12,658 行**（56 个链接自 `WinPieGestures/` 与插件 SDK，13 个 Core 自有） |
| 原生桥导出符号 | `libstarpie_macos.dylib` 现测 **60 个 `starpie_*` 导出**（输入 + 系统 + overlay 策略） |
| 需要重写的 Win32 面 | **79 个唯一入口点**（分布在 25 个文件的 184 个 `[DllImport]` 声明里） |
| UI 重写范围 | 20 个 `*.xaml.cs` / 33,542 行（按 Avalonia 重建，不是逐个翻译 XAML） |

### macOS 侧已经可用的能力

- **轮盘本体**：`CGEventTap` filter-tap 拦截 → 极坐标命中 → Avalonia 透明 overlay 绘制 → 松开执行；支持 4 / 8 / 12 扇区、逐层装配的多级轮盘、滚轮切层与层徽标；
- **切削形态四族**：`Original`（含间隙内缩与圆角夹取）/ `Circle` / `HexagonHive` / 胶囊系，常数逐条抄自 Windows，统一出成顶点折线；
- **主题与配色**：七条主题路径 + 自定义预设，逐值对齐（含两个 Windows 的 shipped 怪癖，见下文）；
- **轨迹手势**：采样门槛、八方向量化、`Auto` 反向提示、两级夹取全部在 Core 里可断言，画法在 UI；
- **动作**：快捷键注入、启动 App、打开网址/文件夹、命令执行、窗口切换槽位、42 个系统控制预设；
- **OCR 识别引擎**：Vision `VNRecognizeTextRequest` 端到端（PNG 进、JSON 出），本机现读 33 种语言，中英文各一条往返；
- **常驻形态**：`LSUIElement` 菜单栏应用、托盘菜单、开机自启（`SMAppService`）、单实例 socket 交接（第二实例会叫起主实例的设置窗）。

---

## 这个项目真正的特色：把"跨平台"变成可证伪的工程

跨平台项目最常见的死法是把"接好了"当成"验过了"。这里对抗它的三件事：

### 1. 每条 native 边界都要配一把会红的尺子

`--selftest` 的 144 条断言不是"跑通不报错"，而是**带变异测试**：故意把实现改坏，断言必须报红。
本轮真实抓到过的例子：

- 把 `SetPosition` 的 x / y 传反 ⇒ `FAIL … 588.4,779.7 → 779.0,588.0`；
- 把 `"toggletopmost"` 的意图改成 `WindowSwitch` ⇒ 分流断言当场报 `ToggleTopmost→WindowSwitch（应为 WindowTopmost）`；
- 把 OCR 结果 y 的翻转去掉 ⇒ 第一版判据（"y 落在 `[0, 图高]` 内"）**照样全绿**，因为翻错的 y 也在范围内；换成方向敏感的顺序判据（上行 y 必须严格小于下行）后同一个突变立刻报红。

> 教训被写进了文档：**判据要与它声称防的那个缺陷同形**，"在范围内"防不住"方向反了"；
> 期望值不许再调一次被测函数（自指断言四条突变只抓到一条）。

### 2. 并集护栏：老配置一条都不许凭空失效

Windows 侧 `ExecuteSystem` 有 63 个 case 标签（稳定 ID + 同义写法 + **中文历史值**）。
`tools/check_system_presets.py` 从 Windows 源文件**现读**标签集合，与 Core 表双向比对，
并额外判"`KnownLabels` 与 switch 分支是同一份集合"——两份真相迟早漂。
少一条，就是某个用户的老配置里某个扇区在 macOS 上凭空失效。

### 3. 不许静默降级

平台不支持的东西必须**指名道姓**地失败，而不是返回一个笼统的 Unsupported：

- `ITrayService.ClickEventsSupported = false`（macOS 不派发托盘点击）；
- `PlatformFailure.PermissionRequired`（无障碍 / 输入监控 / 屏幕录制缺权限）；
- `PlatformFailure.NotSupported`（macOS 没有 Windows 的提权启动语义）；
- 屏截图那半截没接，OCR 动作现在明确回答"引擎可用，缺 ScreenCaptureKit"。

理由很实在：常驻后台工具静默失败，用户只会当成 bug 反复上报。

---

## 抽象层的三条设计约束

- **R-A 接口表达产品需求，不包装 Win32。** 反例是 `GetForegroundWindow` / `SetWindowPos`；正例是 `IWindowService.ActiveWindow`、`MoveTo(target, topLeft)`。每个成员都能回指到现有代码里的一个真实调用点。
- **R-B 热路径语义必须显式进契约。** `ArmTrigger` / `ReturnInterceptedPress` / `DiscardInterceptedPress`、`BeginExclusiveModifierRecording`、`PermissionLost + StateChanged`（macOS 撤销权限时系统**不会**打断已装的 tap，必须自己发现）；`IMouseService.SetPosition` 明确要求"不产生 move 事件"的实现，否则轮盘会被自己的定位动作再次驱动。
- **R-C 平台差异暴露成能力位。** 见上一节。

风格上这不是新发明：插件 SDK 早已用同一套语义化接口（`IHostActionInvoker` / `IHostWindowService` / `IHostWheelService` + 能力门禁）演进到 SDK 1.8，平台层照它的样子长，插件契约才能直接映射到平台服务注入。

---

## macOS 才有的那些坑（全部本机实测，不是抄来的清单）

| 事实 | 后果与对策 |
| :--- | :--- |
| `CGWarpMouseCursorPosition` **不往 tap 里灌 move 事件**（50 次原地 warp 期间 `tapCallbacks` 增量 = 0） | 原来的断言写成"warp 后事件数 ≤2"，量的其实是**用户的手**，用户动鼠标时随机红。已换成"坐标读写往返 ≤1px 且有限" |
| 一个 `NativeMenu` 导出给某个 `TrayIcon` 后**不能再赋第二个** | `open AppuruPie.app` 后 1 秒内 SIGABRT（`AvaloniaNativeMenuExporter.SetMenu`）。修法：只改 `NativeMenu.Items`，或连 TrayIcon 一起重建 |
| 产物目录**必须带 `.app` 后缀** | 没有后缀时 `open` 是"打开普通文件夹"语义，`lsregister -f` 返回 `-10811`、`open` 返回 0 却没有进程 |
| `File.Exists` 对 `.app` **恒为 false**（Unix 上它是目录） | 照它判 bundle 在位会把计算器 / 活动监视器 / 访达三个都误判成"路径不存在"。改成 `Directory.Exists` + `Contents/Info.plist` |
| `.mm` 的导出面必须整块 `extern "C"` | 漏了之后符号变成 `__Z21starpie_ocr_languagesPci`，`dotnet build` 全绿，只有运行时 P/Invoke 找不到入口点。判据：`nm -gU` 必须是不带 `__Z` 的裸名 |
| 在 run-loop source 回调里同步创建 `NSPanel` 会**递归死锁整个 runloop** | tap 回调只做纯数学（极坐标命中），UI 动作一律 `Dispatcher.UIThread.Post` |
| 跨语言回调实测成本：每事件 2.12µs 均值 / 176µs 最差 | 每帧最多一次重绘：hover 只写一个整数，8ms 定时器取变化后再 `InvalidateVisual`，否则 UI 线程被鼠标频率绑架 |
| `Path.GetFileNameWithoutExtension` 在 Unix 上**不把 `\` 当分隔符** | Windows 风格路径 `C:\VS\devenv.exe` 会被当成一整个文件名 ⇒ Per-App 配置匹配静默失效，用户看到"我明明给它配过盘" |
| 枚举 `Regular` 策略的应用才算窗口槽位，Dock 上"固定但未运行"的读不到 | 与 Windows 任务栏槽位口径不同：同一份配置的序号在两平台可能指向不同应用。这条差异写在代码注释与审计表里，不当已完成 |
| 本机 SDK 事实：`VNRecognizedTextObservation` 只有 `topCandidates:`（复数）；`CGDisplayCreateImage` 已标 macOS 15 obsoleted | 凭记忆写 API 会被编译器当场打回；截图那半截要改 ScreenCaptureKit |

---

## 上手

```bash
export PATH="/opt/homebrew/bin:$PATH"

# 1. 构建（原生 dylib 由 csproj 的 BuildNativeBridge 目标增量构建，Windows 侧完全跳过）
dotnet build StarPie.sln -c Release

# 2. 无 GUI 门禁：144 条断言
dotnet src/StarPie.App/bin/Release/net8.0/StarPie.dll --selftest

# 3. 出 .app 产物（ad-hoc 签名）
sh tools/bundle-macos.sh

# 4. 跑起来
open AppuruPie.app                    # 菜单栏常驻
open AppuruPie.app --args --settings  # 打开设置界面（当前是「外观与形态 + 触发键」那一页）

# 5. 装成正常 mac 应用（可选）
sh tools/install-macos.sh             # /Applications 不可写时退回 ~/Applications；⌘+空格 搜 AppuruPie 即可打开
```

默认唤出组合是 **Ctrl + 左键** —— 刻意不劫持右键，以便与其它手势软件共存（`SettingsWindow.cs:59`，可改成右键 / 中键 / 侧键或叠加修饰键）。
装进 `/Applications` 后它没有 Dock 图标也没有主窗口（`LSUIElement`），菜单栏那个苹果派图标是唯一常驻入口；找不到图标时从终端再启动一次即可，第二个实例不抢触发键，只叫起已在跑的那个的设置窗。

首次运行需要在 系统设置 → 隐私与安全性 里授予**辅助功能 / 输入监控**，缺权限时应用会明确报 `PermissionRequired` 并给出下一步，不会假装在工作。

运行模式：`--selftest`（端到端断言）、`--settings`（设置页，关窗即退出）、`--gui`（可见轮盘）、`--tray`（常驻形态）、`--watch`（无画面链路）、`--warp-probe`（量 warp 是否灌事件）、`--config <文件> [--app <bundle id>]`（演示配置链与 Per-App 匹配）。

---

## 移植期纪律

- **R-1 不动老工程**：这台 Mac 编译不了 `net8.0-windows` WPF，所以 Windows 侧行为只能靠"没改它"来保证。新工程与旧工程并存，靠同一批 Core 源文件连接。
- **R-2 平台判断只存在于 `StarPie.Platform.*`**：Core 与 UI 里不许出现 `#if WINDOWS` / `MACOS` 和 `OperatingSystem.IsMacOS()`。
- **R-3 每条 native 边界配证伪测试**：`spikes/macos-input` 的 A/B 两段是模板——带标记必须 0 计入、不带标记必须 300 全计入，缺一半都等于没测。
- **R-4 复用先过许可证**：见下节。
- **R-5 上游红线不因换框架而放松**：右键按下到轮盘呈现 < 16ms、回调线程不做 IO、极坐标命中禁止欧氏距离（macOS 侧坐标同为"点 + 左上原点 + Y 向下"，`atan2` 约定不变）。

---

## 当前边界（明确没做的，别当成已完成）

- **设置界面只有第一格**：「外观与形态 + 触发键」读写真实 `config.json`，右侧嵌的是运行期同一个 `WheelControl`（预览就是轮盘会画的东西）。**没做**：手势与动作页、触发与场景页、高级与系统页、四页共用的 i18n 词条。
- **屏截图未接**：OCR 引擎通了，缺 `ScreenCaptureKit` 那半截。
- **overlay 真机视觉未验**：代码已进产品（会打印回读的 window level / collection），但"全屏 Space 之上 + 不抢前台"需要人眼确认。
- **`StarPie.Platform.Windows` 尚未创建**：Core / UI 只依赖抽象，不阻塞，但 Windows 后端目前仍是老的 WPF 工程。
- **配置迁移是未解决的产品问题**：真实用户配置里的动作参数是 `G:\剪映\...` 这类 Windows 路径，在 macOS 上必然启动失败——失败有明确返回，但"怎么帮用户把这些换掉"没有答案。
- **本机只能 ad-hoc 签名**：Developer ID 与公证需要证书。
- **一处文档与代码不一致**：`AGENTS.md` §3.1 的 $\theta_i = -\pi/2 + i\Delta\theta$ 与实现不符，代码里第 0 位在**正东**（`GestureController.cs:1069-1076`、`:2310-2312` 定案，并已用 72 个采样点逐一比对通过）。该改的是文档。
- **待授权的重排 U1–U5**：四个"纯类型放错文件"的搬迁（`PluginEventService`、`SoundType` 枚举、`ProgramItem` DTO、`ICollectionView` 视图模型归位）能把约 14k 行放进 Core，但都要动 `WinPieGestures/`，与 R-1 冲突，需要维护者点头。

---

## 命名说明

本项目叫 **苹果派（AppuruPie）**，上游 Windows 版叫 `StarPie`。以下**运行期字面标识**保持原样，改了会变成假说明或需要用户重新授权：

`CFBundleIdentifier=com.starpie.menubar`、`CFBundleExecutable=StarPie`、`libstarpie_macos.dylib`、
`~/Library/Application Support/StarPie/`（日志与插件数据根）、`StarPie.Plugin.Abstractions`。

品牌层（`CFBundleName` / `CFBundleDisplayName` / 权限弹窗文案 / 托盘菜单）已经用 AppuruPie。

---

## 许可证与第三方

- 上游基线与本 fork 的许可条款见 [LICENSE](LICENSE) 与 [README.md](README.md) 的许可证一节。
- 合法复用遵循 `PORTING_REFERENCE.md` §3.3 的八条落地规则，逐文件登记在 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)（源仓库 / commit / 路径 / SPDX / 原 copyright）。
- 一个会改变计划的审计结论：**Kando 是 Electron + MIT ObjC++ N-API addon，macOS 侧完全没有 `CGEventTap`**（全局触发只有键盘快捷键，无事件拦截/重放）。所以"复用 Kando 已验证的 macOS 输入后端"无法照字面执行——可合法复用的是注入配方、窗口枚举、AX raise、键码表、手势算法与一份坑清单；**可拦截、可重放、能自动复活的 filter-tap 必须自研**。
- GPL / CC-BY-NC / 自定义许可的项目（AltTab、MiddleClick、Mos、Mac Mouse Fix）**只借鉴设计**，不复制代码。

---

## 相关文档

| 文件 | 管什么 |
| :--- | :--- |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 真实布局、抽象层约束、进度快照、下一步排序 |
| [`PORTING_FEASIBILITY.md`](PORTING_FEASIBILITY.md) | 可行性 12 问：哪些能复用 / 必须重写 / 只能降级 / 暂不迁移 |
| [`PORTING_REFERENCE.md`](PORTING_REFERENCE.md) | 平台能力对照、Kando 与 Avalonia 审计、许可证审计、本机 spike 数字 |
| [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) | 一行一文件的复用登记 |
| [`AGENTS.md`](AGENTS.md) | 上游架构与工程红线（换框架不放松） |
| [`plugin/README.md`](plugin/README.md) | 插件 SDK 与宿主服务契约 |
