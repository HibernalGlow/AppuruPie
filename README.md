<a id="top"></a>
<p align="center">
  <img src="./assets/readme/hero.svg" width="100%" alt="苹果派 AppuruPie：把高频操作变成肌肉记忆的 Windows 鼠标轮盘。右侧按产品真实几何绘制 8 扇区轮盘，光标停在带外圈子环的高亮扇区上，并标注极坐标命中角 θ。" />
</p>

<p align="center">
  <a href="https://github.com/SoftBlack42/StarPie/releases"><img src="https://img.shields.io/github/v/release/SoftBlack42/StarPie?label=Release&style=flat-square&color=C8102E&logo=github" alt="最新正式版"></a>
  <a href="https://github.com/SoftBlack42/StarPie/releases"><img src="https://img.shields.io/github/v/release/SoftBlack42/StarPie?include_prereleases&label=Dev&style=flat-square&color=F59E0B" alt="开发线版本"></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-7B1620.svg?style=flat-square&logo=windows&logoColor=white" alt="支持平台">
  <img src="https://img.shields.io/badge/.NET-8.0%20WPF-DFA24E.svg?style=flat-square&logo=dotnet&logoColor=white" alt="技术栈">
  <img src="https://img.shields.io/badge/License-MIT-B5762A.svg?style=flat-square" alt="许可证">
  <a href="#build"><img src="https://img.shields.io/badge/GUI%20Tests-27%20cases-B5762A.svg?style=flat-square&logo=pytest" alt="GUI 回归用例数"></a>
  <a href="plugin/README.md"><img src="https://img.shields.io/badge/Plugin%20SDK-1.8-B5762A.svg?style=flat-square" alt="插件 SDK 版本"></a>
</p>

<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/简体中文-C8102E?style=for-the-badge" alt="简体中文（当前）"></a>
  <a href="README_EN.md"><img src="https://img.shields.io/badge/English-7B1620?style=for-the-badge" alt="English version"></a>
</p>

<p align="center">
  <a href="#proof">🎬 实机演示</a> •
  <a href="#mechanism">⚙️ 工作原理</a> •
  <a href="#download">🚀 下载与上手</a> •
  <a href="#features">✨ 功能特性</a> •
  <a href="#plugin">🧩 插件系统</a> •
  <a href="#i18n">🌐 多语言</a> •
  <a href="#build">🛠️ 本地构建</a> •
  <a href="#acknowledgements">💡 开发故事</a> •
  <a href="CHANGELOG.md">📋 更新日志</a>
</p>

---

## <a id="proof"></a>🎬 实机演示 / Proof

轮盘的全部手感只能实机体验。下面两张是无剪辑录屏：左边是「按住右键划出 → 松手触发」，右边是「同一套几何在不同主题与形态下的实时渲染」。

<p align="center">
  <img src="./attachments/第一张.gif" width="49%" alt="按住右键划出轮盘并触发扇区动作的实机录屏" />
  <img src="./attachments/轮盘样式.gif" width="49%" alt="多种预设主题与切削形态下的轮盘实时渲染录屏" />
</p>

<details open>
<summary><b>📺 完整讲解视频 / Narrated walkthrough（Bilibili）</b></summary>
<br/>
<p align="center">
  <a href="https://www.bilibili.com/video/BV1XjtA6KEGL" target="_blank">
    <img src="./attachments/video_cover.png" width="700" alt="苹果派实机演示与重构解说视频封面" />
  </a>
  <br/>
  <a href="https://www.bilibili.com/video/BV1cubL6WEpB"><img src="https://img.shields.io/badge/Bilibili-实机演示与重构解说-FB7299?style=for-the-badge&logo=bilibili&logoColor=white" alt="前往 Bilibili 观看"></a>
</p>
</details>

---

## <a id="intro"></a>📖 它是什么 / What it is

**苹果派（AppuruPie）** 是一款专为 Windows 10 / 11 打造的轻量级鼠标轮盘手势（Radial / Pie Menu）效率工具：按住**鼠标右键 / 中键 / 侧键或键盘触发键**，在光标附近呼出快捷轮盘，滑到目标扇区松手即执行；也支持不唤出轮盘的独立轨迹手势。

它解决的具体问题是：**高频操作不该每次都在菜单里找**。把「撤销」「切窗口」「启动常用软件」「平铺」「截屏识字」绑到固定的空间方向上，用几十次之后就是零思考的盲操。

三条工程红线（也是本项目所有取舍的判断依据）：

- ⚡ **零延迟**：鼠标按下到轮盘呈现场景 **< 16ms**；钩子回调线程内绝不执行 IO 或复杂计算，纯轻量位运算捕获；
- 🪶 **极致轻量**：原生 C# WPF + Win32 P/Invoke，不引入 Electron / MAUI / 任何浏览器内核；绘图画刷与动画对象显式 `Freezable.Freeze()`；
- 🎯 **肌肉记忆与确定性**：扇区严格绑定极坐标方向（4 / 8 / 12 档均为 360°/N 等分且**第 0 位固定正东**），杜绝误触、漂移与错选。

---

## <a id="mechanism"></a>⚙️ 工作原理 / How it works

<p align="center">
  <img src="./assets/readme/how-it-works.svg" width="100%" alt="手势执行链路四步：按住触发键、越过阈值唤出轮盘、按极坐标角距命中最近扇区、松开后由 SendInput 或插件执行动作。" />
</p>

一次划动在代码里经过三段，每段各守一条红线：

| 环节 | 实现 | 为什么这样切 |
| :--- | :--- | :--- |
| **捕获** | `MouseHook.cs`（`WH_MOUSE_LL` 独立线程） | 回调里只做位运算与时间戳记录，任何 IO 都会直接反映成掉帧 |
| **判定** | `GestureController.cs`（极坐标状态机） | 命中用**角距最近邻**而不是欧氏距离，方向即结论，左右不会颠倒 |
| **执行** | `ActionExecutor.cs` + `SendInput` | 扫描码 + 修饰键保持时延，避免宿主应用把 `Ctrl + G` 读成 `G` |

### 典型物理工作集（项目自测基准）

| 运行状态 | 物理工作集 |
| :--- | :---: |
| 🌙 静默后台守护（仅托盘与全局钩子常驻） | **15 ~ 30 MB**（系统深睡整理后约 10 ~ 20 MB） |
| 🎡 轮盘唤出与手势交互（透明硬件加速渲染） | **25 ~ 50 MB** 峰值 |
| 🖥️ 控制台全量 UI（4 标签页 + 实时画布） | **60 ~ 110 MB**，关窗 30 秒后按需释放并回落 |

> 数字来自本项目在真实运行时物理工作集上的自测口径，不是理论估算；控制台窗口采用「关闭后 30 秒才真正释放」策略，热代码常驻物理 RAM，避免下一次唤起发生硬缺页。

---

## <a id="download"></a>🚀 下载与上手 / Quick Start

**最新正式版 `v1.7.4`（2026-09-19）** ｜ **开发线 `v1.8.0-beta.5`**（插件系统与 SDK 1.8）

| 版本包 | 适用场景 | 说明 | 下载入口 |
| :--- | :--- | :--- | :--- |
| **独立免安装单文件版（推荐）** | 所有用户 | 内置 .NET 运行时，解压即可运行 | [⬇️ StarPie.exe (Standalone)](https://github.com/SoftBlack42/StarPie/releases) |
| **轻量便携版** | 已安装 .NET 8 运行时的用户 | 体积小巧，绿色便携 | [⬇️ StarPie 便携包](https://github.com/SoftBlack42/StarPie/releases) |
| **历史版本归档** | 版本回溯与对比 | 历史版本的二进制文件与说明 | [📂 浏览 Releases 归档](https://github.com/SoftBlack42/StarPie/releases) |

> ℹ️ **命名与产物来源**：本项目名为 **苹果派（AppuruPie）**，但 Windows 发行产物、可执行文件名 `StarPie.exe`、数据目录 `%LOCALAPPDATA%\StarPie\` 与契约程序集 `StarPie.Plugin.Abstractions` 目前仍沿用上游命名，且本仓库自身尚未发布 Releases —— 所以下载与克隆链接指向 `SoftBlack42/StarPie`。

### 第一次成功划出轮盘

1. 下载并运行 `StarPie.exe`，程序会在系统托盘中后台运行；
2. 双击托盘图标或右键选择「偏好设置」，在「触发与场景」中录制轮盘触发键；
3. 按住触发键拖动超过阈值，或按配置长按，即可在光标附近唤出轮盘；
4. 滑至目标扇区后松开触发键执行动作，向外甩出则可取消或执行自定义取消动作；
5. 如需轨迹手势，可启用独立手势触发键，并在动作页映射 1～3 段方向组合。

---

## <a id="features"></a>✨ 功能特性 / Features

当前能力总览：

| 能力域 | 当前能力 |
| :--- | :--- |
| **轮盘交互** | 4 / 8 / 12 扇区、中心核圆动作、多级子轮盘、蜂窝扇与外甩取消 |
| **轨迹手势** | 最多 3 段、8 方向组合、轨迹浮层、分段灵敏度与释放提示 |
| **动作与窗口** | 快捷键、程序、网址、文件夹、命令、系统控制，以及窗口切换、平铺、跨屏、置顶与透明度 |
| **屏幕 OCR** | Windows 本地离线识别、AI 视觉接口与自定义 HTTP OCR |
| **全盘秒搜** | 纯原生零依赖检索引擎，应用与系统工具毫秒级短路命中，深盘遍历受 50ms 时间预算硬控 |
| **配置体验** | 双栏聚焦编辑、实时轮盘画布、扇区拖拽换位、运行窗口捕捉与紧凑全览列表 |
| **外观定制** | 多种轮盘形态、独立一二级主题、单扇区字体 / 图标 / 文字位置覆盖、屏幕边缘适配与自定义交互音效 |
| **插件生态** | .NET 进程内 DLL 插件、能力门禁、声明式参数表单、可回收 ALC 卸载与端到端自检（开发线） |

> 以下演示动图默认折叠，点击 **🎬 查看演示** 展开。

### 🎯 轮盘与手势 / Wheel & Gestures

#### 1. ⚡ 鼠标手势快速呼出与动作触发

- 按住鼠标右键滑动超过设定阈值即呼出轮盘，滑向目标扇区后松开按键即可触发对应动作（热键、打开程序、打开文件夹或系统功能）；
- 普通右键单击依然正常弹出原生右键菜单，互不冲突；
- 支持右键、中键、侧键、键盘单键与组合修饰键作为触发键，并可选择拖动越阈值或长按延时呼出；
- 录制快捷键时可临时独占并暂停全局热键，避免 `Win + D`、`Alt + Tab` 等系统组合被意外执行。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/按键组合触发录制.gif" width="680" alt="按键组合触发录制演示" />
</p>
</details>

#### 2. 🌟 多级级联子轮盘

- **多级轮盘级联交互**：支持在任意扇区方位自由扩展 1~4 个二级子动作。光标划向扇区并在扇区内停留时，外环以弹性动画平滑展开二级子扇区，向外滑入即可极速触发；
- **一二级主题与配色完全独立定制**：支持单独调节各级尺寸、字号与图标排版；二级轮盘既可**一键同步主轮盘**，也可**完全独立定制专属风格与配色**；
- 支持外圈子环与蜂窝扇两种二级形态，加入迟滞保持和防抖判定，降低边界移动时的闪烁与误收起；
- 一级与二级动作均支持拖拽对调，并可选择拖动一级动作时是否同步交换其二级子动作。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/第三张.gif" width="49%" alt="外圈子环二级轮盘演示" />
  <img src="./attachments/蜂窝扇.gif" width="49%" alt="蜂窝扇二级轮盘演示" />
</p>
</details>

#### 3. 🚀 顺势外甩脱离取消 (Outer Escape Cancel)

- 若划出手势后不想执行任何动作，无需反向拉回中心核圆；
- 只需顺势向外快速滑动脱离轮盘边缘，轮盘自动进入半透明安全取消状态，松开右键不触发任何动作；
- 支持在设置中开启/关闭，并可通过滑块微调外甩距离灵敏度（140px ~ 320px）；`v1.6.5` 起还可为外甩取消配置独立动作与常用预设，中心核圆取消仍可保持静默。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/外甩取消.gif" width="680" alt="顺势外甩脱离取消演示" />
</p>
</details>

#### 4. 🎯 4 / 8 / 12 扇区方位自适应

- **4 键方位**：上下左右大角度，适合盲操；
- **8 键方位**：经典 8 向均衡布局（默认）；
- **12 键方位**：高密度功能映射，适合多动作工作流；中心核圆也可配置独立动作，并支持唤醒死区灵敏度。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/06_sector_counts.gif" width="680" alt="4/8/12 扇区分割自适应演示" />
</p>
</details>

#### 5. ➡️ 独立轨迹手势与可视化提示

- 可为轨迹手势单独指定右键、中键或侧键，与轮盘触发键并行使用；
- 支持最多 3 段、8 方向的轨迹组合，通过短段过滤和相邻同向合并减少快速绘制时的微小抖动误判；
- 绘制时显示透明轨迹浮层、起点和释放提示，可调节分段灵敏度及提示文字方位；
- 未达到拖动阈值的轻点会回放为原生鼠标点击，不影响日常操作。

---

### 🎨 外观与配色 / Look & Feel

#### <a id="visuals"></a>6. 🎨 多种轮盘形态与风格预设

- **4 种几何形态**：经典紧凑扇区 (Original)、独立悬浮圆形 (Circle)、圆角胶囊 (Capsule)、蜂巢六边形 (HexagonHive)；
- **多套预设主题**：跟随系统、浅色模式、深色模式、液态毛玻璃、抹茶森林、冰川透蓝、莫兰迪柔灰；
- 右侧提供 **实时交互预览画布**，支持缩放、平移、复位、点击选中与拖拽换位，调节参数即时可见；`v1.6.8` 增加屏幕边缘防溢出策略与 X / Y 安全边距。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/主题样式展示.gif" width="49%" alt="轮盘形态与主题风格切换演示" />
  <img src="./attachments/样式展示.gif" width="49%" alt="多几何形态与视觉布局展示" />
</p>
</details>

#### 7. 🎨 自定义高级配色与预设重命名

- 独立折叠面板，支持微调扇区底色、高亮光晕、边框线条与文字颜色；
- 支持十六进制颜色输入、色盘选取与屏幕吸色；
- 支持将当前颜色保存为自定义预设，并支持一键重命名与删除预设。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/04_custom_colors.gif" width="49%" alt="自定义高级配色与预设管理演示" />
  <img src="./attachments/中心图案调节.gif" width="49%" alt="中心图案调节演示" />
  <br/><br/>
  <img src="./attachments/扇区样式定制.gif" width="680" alt="扇区样式定制演示" />
</p>
</details>

#### 8. 🖼️ 自定义矢量 / 图片图标导入

- 图标库支持直接导入本地 **SVG 矢量文件** 与 **PNG / ICO / JPG** 图片；
- 导入图标自动保存在本地配置目录，支持在所有扇区中自由选用，并支持自定义图标重命名与删除。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/05_custom_icons.gif" width="680" alt="自定义图标导入与管理演示" />
</p>
</details>

---

### 🧰 动作与窗口 / Actions & Windows

#### 9. 🧰 更完整的动作类型体系

| 动作分类 | 主要用途 |
| :--- | :--- |
| **快捷热键** | 录制或拼装组合键，支持主按键搜索、Pause / Break 与独占录入 |
| **启动程序** | 启动 EXE、快捷方式或文件，可携带参数并选择常规权限启动 |
| **打开网址** | 使用系统默认、Chrome、Edge、Firefox 或自定义浏览器打开网址 |
| **打开文件夹** | 打开本地路径及桌面、下载等系统虚拟目录 |
| **运行命令** | 支持 CMD、PowerShell、WSL 及隐藏终端模式 |
| **屏幕 OCR** | 框选屏幕区域并调用本地、AI 或自定义 HTTP 识别 |
| **窗口管理** | 切换窗口、平铺、跨屏移动、置顶与透明度控制 |
| **系统控制** | 锁屏、音量、媒体、任务视图、虚拟桌面等系统功能 |

动作执行与显示图标已经解耦，同一动作可独立选择内置矢量图标、程序图标或自定义图片，不再被动作类型限制。

#### 10. 🪟 窗口切换、管理与多种平铺布局

- **切换任务栏窗口**：按当前任务栏可见顺序绑定第 N 个运行窗口，图标、标题与激活目标使用同一快照；
- **窗口平铺**：内置左右、上下、主从、网格、单窗、横向堆叠、纵向堆叠、Columns、BSP、Auto Grid 等多种布局；
- 支持循环前进 / 后退布局、恢复平铺前位置、包含最小化窗口以及自定义进程排除名单；
- 可将当前活动窗口移动到下一显示器、切换总在最前或调整窗口透明度。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/进程窗口切换.png" width="680" alt="进程窗口切换演示" />
</p>
</details>

#### 11. 📝 Windows 原生 OCR 与可扩展识别接口

- 框选任意屏幕区域后执行文字识别，支持 Windows 10 / 11 原生 `Windows.Media.Ocr` 离线引擎；
- 可选 AI 视觉接口或自定义 HTTP OCR 服务，并提供语言包环境诊断与配置面板；
- 识别结果支持自动复制、结果窗口展示、合并行、处理中日韩间空格以及浏览器搜索。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/OCR.gif" width="49%" alt="屏幕区域框选与 OCR 识别演示" />
  <img src="./attachments/OCR接口配置.png" width="49%" alt="OCR 接口配置面板" />
</p>
</details>

#### 12. 🔄 在线更新、诊断日志与贡献者信息

- 内置 GitHub Releases 更新检查，可选择更新通道、代理源、自定义代理并忽略指定版本；
- 系统日志采用后台队列异步写入，可从设置中快速打开日志目录和当日日志；
- 贡献者卡片默认使用离线名单，不在启动时主动联网，仅在用户手动刷新或检查更新时请求最新数据。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/系统内置更新与贡献展示.gif" width="680" alt="系统内置更新与贡献展示演示" />
</p>
</details>

---

### 🛡️ 方案与场景 / Profiles & Scenes

#### 13. 💼 多程序专属配置方案

- 支持针对 Chrome、VS Code、Photoshop、SolidWorks 等不同前台程序分别设置专属轮盘配置；
- 苹果派会根据当前活动程序自动匹配对应方案，没有专属方案时回退到全局配置；
- `v1.7.4` 起「继承全局方案未配置槽位」默认关闭，专属方案拥有纯净独立的配置空间，需要全局兜底时可按需一键开启；
- 支持配置方案的新建、复制、删除与一键重命名，方便复用和维护不同工作流。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/07_per_app_profiles.gif" width="680" alt="多程序专属方案演示" />
</p>
</details>

#### 14. 🎛️ 动作配置工作区、应用快捷录入与拖拽编辑

- 动作页采用「配置方案 + 聚焦编辑卡片 + 实时轮盘画布」的双栏工作区，也保留适合快速查看多个动作的紧凑全览列表；
- 提供智能应用选择器，可汇总已安装程序，并支持名称搜索与快速过滤；
- 提供运行窗口捕捉器，可直接选择当前桌面上的窗口或进程，减少手动查找可执行文件路径；
- 可直接点击画布选择目标扇区，并拖拽交换一级扇区、二级动作和中心核圆的位置；
- 中心核圆可启用独立动作，支持死区松开触发、常用预设及独立文字和图标排版；
- 每个扇区可独立覆盖布局模式、字体、字号、文字颜色、图标大小、文字位置及 X / Y 偏移。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/程序拖拽配置界面.gif" width="49%" alt="程序拖拽配置界面演示" />
  <img src="./attachments/07_1.gif" width="49%" alt="应用程序智能检索与动作配置" />
</p>
</details>

#### 15. 🛡️ 场景隔离、全屏防误触与多语言

- **全屏与游戏检测**：运行全屏独占应用或游戏时自动放行原生右键；
- **修饰键穿透**：支持按住 Ctrl / Shift / Alt 时绕过轮盘；
- **黑名单支持**：支持将指定进程加入排除名单；
- **多语言热切换**：内置简体中文、繁体中文、English、日本語，切换即时生效；`ScreenHelper` 统一处理多显示器、混合 DPI 与屏幕边缘坐标，减少副屏唤起漂移和轮盘溢出。

<details>
<summary><b>🎬 查看演示</b></summary>
<br/>
<p align="center">
  <img src="./attachments/08_settings_and_i18n.gif" width="49%" alt="防误触与多语言设置演示" />
  <img src="./attachments/边缘呼出防溢出.gif" width="49%" alt="边缘呼出防溢出配置" />
</p>
</details>

---

## <a id="plugin"></a>🧩 插件系统 / Plugin System

苹果派使用轻量的 **.NET 进程内 DLL 插件架构**。插件只需引用独立的
`StarPie.Plugin.Abstractions` 契约程序集，通过 `Initialize` 注册动作等贡献；主程序则为每个插件创建
宿主包装实例，统一管理扫描、加载、惰性激活、调用租约、异常隔离、异步停用和程序集卸载。

当前动作执行路径已经具备从注册、配置、参数校验到 Sequential / Background 调度的完整基础闭环；
交互事件与轮盘结构路径也已建立可扩展入口，后续可以继续扩展而不复制整套插件生命周期代码。

> 📖 **插件开发入口**：开发文档、当前 API、架构说明、示例工程和历史资料统一收录在 [《苹果派插件开发资源》](plugin/README.md)。
> 🔗 **官方插件仓库**：[StarPie-Official-Plugins](https://github.com/Star-Pie/StarPie-Official-Plugins)。主仓库中的 `plugin/StarPie-Official-Plugins/` 是该仓库的 Git 子模块检出目录；官方插件的源码、打包和发布流程均在这个独立仓库中维护，苹果派主仓库不重复引用、构建或随发行包携带官方插件 DLL。

- **官方插件手动安装与一次性引导知情同意例外 (1.8.0+)**：
  - **发行包零预置**：苹果派发行包不随包携带官方插件 DLL，启动与后台运行保持零自动网络请求。
  - **一次性引导知情同意例外**：为了提升新用户上手体验，1.8.0+ 在用户首次交互式打开设置窗口时（非静默启动、非自检模式，且插件系统正常开启），会弹出一次性知情引导弹窗。弹窗展示 5 项核心官方插件（`folder`, `weburl`, `launch`, `system`, `shelltool`）、官方仓库来源及所需权限（`Process` 启动进程、`InputSimulation` 按键模拟）。
  - **零网络请求原则**：用户点击「一键安装」前，宿主绝对不发起任何网络请求（不拉取 catalog、不下载包）；用户点击「一键安装」后，仅下载安装缺失项；已安装项跳过（不覆盖、不升级、保留禁用状态）；若官方目录中包含超出预告范围的权限，暂停并请求用户确认。
  - **常驻重试入口**：若用户选择「暂不安装」或部分项下载失败，在「插件与扩展」页面顶部常驻提供一键重试/安装横幅，已成功项保留，随时可补全缺失项。
- **社区插件仍可本地安装**：把社区插件 `.dll` 放进**程序目录的 `plugin` 文件夹**后点「重新扫描」，在候选卡片上点安装；或在设置里点「手动安装社区插件 (.dll)」直接挑文件。
- **⚠️ 放进 `plugin\` 只会用那一枚 `.dll`**：插件包里的其它文件（图标、资源、依赖 dll）
  不会被一起复制过去。需要**整包安装**时请改用「手动安装社区插件 (.dll)」，选中插件包目录里带
  `plugin.json` 的那一枚 —— 识别到清单后宿主会整目录复制。
- **两个目录职责分开，互不干扰**：

  | 目录 | 角色 |
  | :--- | :--- |
  | `<程序目录>\plugin\` | **社区插件候选区**：仅用于手动安装社区 `.dll`；苹果派 **只读不写**，不会在这里创建任何文件 |
  | `%LOCALAPPDATA%\StarPie\plugin-data\` | **可写数据目录**：安装副本、启用状态、插件私有数据都在这里，卸载时整目录清除 |

- **安装与启用分离**：装完默认是「已安装但未启用」，必须手动勾选才会加载 ——
  避免「放一份文件进去」等价于「放行它的代码」。安装前会展示 ID、版本、作者、目标框架、
  架构、SHA256、签名状态与声明的能力清单。
- **候选卡片会直接告诉你结论**：可安装 / 有新版本 / 版本更旧 / 已装同版本 / 内容已变 /
  ID 重复 / 无法识别。同一 ID 出现两份文件时两份都会被标成「ID 重复」且不给安装按钮。
- **失败自保护**：单个插件加载失败或连续触发异常会被隔离，不影响苹果派本体；
  连续两次启动异常会进入安全模式并临时禁用可疑插件。
- **给插件作者**：`plugin/samples/` 下有三个可直接参照的示例（`HelloAction` 为参考模板，`ScreenBrightness` 覆盖 P/Invoke、COM 与耗时 IO 三类难题，`FloatingBall` 是常驻窗口形态：插件自己画球、点球经 `IHostWheelService` 呼出宿主轮盘，外观参数同时示范动作参数与插件级设置页）。调试时可用
  `StarPie.exe --plugin-selftest <插件.dll> [报告路径] [--skip-invoke]` 在临时沙箱里跑
  全链路自检（不会碰你已装好的插件），或用 `StarPie.exe --plugin-paths` 查看当前生效的目录。

---

## <a id="i18n"></a>🌐 多语言支持 / Internationalization

可在设置页面的「⚙️ 高级与系统」中随时切换界面语言，切换即时生效：

| 语言代码 | 显示名称 | 支持状态 |
| :--- | :--- | :---: |
| `zh-CN` | 🇨🇳 简体中文 | 🟢 完整支持 |
| `zh-TW` | 🇭🇰/🇼 繁體中文 | 🟢 完整支持 |
| `en` | 🇺 English | 🟢 完整支持 |
| `ja` | 🇯🇵 日本語 | 🟢 完整支持 |
| `Auto` | 🖥️ 跟随操作系统语言 | 🟢 完整支持 |

---

## <a id="build"></a>🛠️ 本地构建与开发 / Build & Development

**环境要求**：Windows 10 / 11 (x64) ｜ [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) ｜ Python 3.10+（仅运行自动化测试套件需要）

```bash
# 1. 克隆代码仓库
git clone https://github.com/SoftBlack42/StarPie.git
cd StarPie

# 2. 编译项目 (Release)
dotnet build WinPieGestures/WinPieGestures.csproj -c Release

# 3. 运行项目
dotnet run --project WinPieGestures/WinPieGestures.csproj

# 4. 发布轻量版（需要目标电脑安装 .NET 8 Desktop Runtime）
dotnet publish WinPieGestures/WinPieGestures.csproj -c Release -r win-x64 --no-self-contained -o releases/local/Lightweight

# 5. 发布独立版（自带 .NET 运行时）
dotnet publish WinPieGestures/WinPieGestures.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o releases/local/Standalone
```

**自动化测试**：pywinauto + UIA 的 GUI 回归，**会真实弹出界面并需要 Windows 桌面会话**，当前 27 项用例（设置窗口 / 插件页 / 多语言台账三个套件）：

```bash
pip install pytest pywinauto
python -m pytest tests/ -v
```

---

## <a id="structure"></a>📂 项目结构 / Project Structure

<details>
<summary><b>展开完整目录树</b></summary>

```text
StarPie/
├── .github/                         # CI 工作流与社区配置
├── WinPieGestures/                  # 核心工程（C# / .NET 8 / WPF）
│   ├── ActionExecutor.cs            # 热键、程序、网址、命令、系统动作调度
│   ├── ActionItem.cs                # 动作模型与单扇区排版覆盖
│   ├── MouseHook.cs                 # Win32 低级鼠标 Hook 专用线程
│   ├── KeyboardHook.cs              # Win32 低级键盘 Hook 与独占录制
│   ├── GestureController.cs         # 轮盘与轨迹手势状态机
│   ├── GestureMapping*.cs           # 轨迹组合映射与配置模型
│   ├── GestureTrailOverlay.cs       # 轨迹绘制和释放提示浮层
│   ├── RadialWindow.xaml(.cs)       # 轮盘透明窗口与运行时渲染
│   ├── SettingsWindow.xaml(.cs)     # 双栏画布、聚焦编辑与系统设置
│   ├── Plugin/                      # 插件宿主（加载上下文、识别、注册、参数表单、自检）
│   ├── WindowTaskbarHelper.cs       # 任务栏顺序、窗口图标与切换快照
│   ├── WindowTiler.cs               # 窗口平铺、恢复、循环与跨屏控制
│   ├── WindowPickerWindow.xaml(.cs) # 活动窗口和进程捕捉器
│   ├── ScreenHelper.cs              # 多显示器、DPI 与边缘坐标处理
│   ├── ScreenSnipWindow.xaml(.cs)   # OCR 截屏区域选择
│   ├── OcrManager.cs                # 本地、AI 与 HTTP OCR 调度
│   ├── OcrSettingsDialog.xaml(.cs)  # OCR 引擎和接口设置
│   ├── OcrResultWindow.xaml(.cs)    # OCR 结果展示
│   ├── UpdateManager.cs             # GitHub Releases 更新检查
│   ├── AppLogger.cs                 # 异步运行日志
│   ├── ConfigManager.cs             # 配置持久化、导入导出与自启
│   ├── IconHelper.cs                # 内置 / 程序 / 自定义图标解析
│   └── WinPieGestures.csproj        # .NET 8 WPF 项目配置
├── plugin/                          # 插件开发文档、SDK、示例工程与官方插件子模块
├── releases/                        # 历史版本与发布归档
├── installer/                       # Inno Setup 安装包脚本
├── assets/                          # 品牌素材（logo / 应用图标 / README 视觉源）
├── attachments/                     # README 截图与 GIF 演示素材
├── tests/                           # pywinauto GUI 自动化测试
├── AGENTS.md                        # 架构与多轮开发继承规范
├── CHANGELOG.md                     # 完整版本更新日志
├── CONTRIBUTING.md                  # 贡献指南
├── LICENSE                          # MIT 许可证
└── README.md                        # 中文主文档（本文档）
```

</details>

---

## <a id="acknowledgements"></a>💡 开发故事与维护说明 / Story & Maintenance

### 🌟 灵感来源

本人是机械设计制造及其自动化专业的一名学生。在日常三维建模中经常使用 SolidWorks，觉得其内置的鼠标手势轮盘十分便利。

在接触到 AI Agent 辅助开发工具后，萌生了将这种轮盘操作迁移到 Windows 桌面全局的想法，希望能以此提升日常办公与操作的便利性。对于此前未接触过手势轮盘的朋友，这或许也是一种新颖高效的交互体验。

虽然开源社区已有同类轮盘项目，但在功能侧重点和交互细节上各有不同。从最初构想到发布，中途因学业课程与竞赛有所间断，与 AI Agent 协作断断续续历时约一周完成了当前版本。

项目中若有不够完善或考虑不周之处，还请多包涵。欢迎通过 GitHub Issue 提交 Bug 报告、使用反馈或改进建议！

### 🤖 人机协同开发说明

本项目由开发者主导架构设计、交互逻辑规划与系统调优，并由 AI 智能体（**AI Agent - Antigravity**）及开源贡献者协同完成代码构建、多语言支持、交互优化与 GUI 自动化测试验证。

### 📌 阶段性维护说明

- **当前状态**：截至 2026-10-03，最新正式版为 `v1.7.4`（2026-09-19），`main` 开发线已推进至 `v1.8.0-beta.5`（插件系统与 SDK 1.8）；轮盘、轨迹手势、窗口管理、OCR 与配置工作流仍在持续完善；
- **后续节奏**：项目继续采用开发者、社区贡献者与 AI Agent 协同迭代的方式维护，欢迎通过 Issue / Pull Request 提交功能建议、兼容性反馈与演示素材。

---

## <a id="license"></a>📄 开源许可证与使用须知 / License

本项目采用附加**非商业性限制条款**的 [MIT License (with Non-Commercial Restriction)](LICENSE) 授权：

- **个人与非商业用途**：完全免费开放用于个人日常、学习研究及非商业性办公场景，欢迎自由体验、定制与参与社区共建；
- **商业限制**：未经原作者（SoftBlack42）明确书面许可，严禁将本软件（含源码与二进制产物）或其衍生作品用于任何形式的付费商业售卖、二道贩卖、付费打包上架应用市场、或嵌入闭源商业收费软件中。商业授权合作请联系原作者。

---

<p align="center">
  <a href="#top"><img src="https://img.shields.io/badge/⬆️_回到顶部-7B1620?style=flat-square" alt="回到顶部"></a>
  <a href="https://github.com/SoftBlack42/StarPie/releases"><img src="https://img.shields.io/github/v/release/SoftBlack42/StarPie?label=⬇️%20下载最新构建&style=flat-square&color=C8102E&logo=github" alt="下载最新构建"></a>
</p>
