# 打坐倒计时小窗

梦幻西游打坐倒计时悬浮窗。窗口里只有一串数字，没有边框、没有进度条、没有提示音；
无边框、逐像素透明、可置顶、可锁定成一张鼠标穿透的贴纸。

- **单个 exe，约 45 KB**，双击就能用，不需要安装、不写注册表
- 运行时只依赖系统自带的 **.NET Framework 4.x**；编译只依赖 Windows 自带的 `csc.exe`
- 支持 Windows 7 SP1 / 10 / 11，32 位和 64 位都可以

![空闲状态](assets/preview-idle.png)

## 目录

- [功能特性](#功能特性)
- [快速开始](#快速开始)
- [操作一览](#操作一览)
- [界面预览](#界面预览)
- [构建](#构建)
- [测试](#测试)
- [配置](#配置)
- [分发给别人](#分发给别人)
- [系统要求](#系统要求)
- [实现要点](#实现要点)
- [目录结构](#目录结构)
- [常见问题](#常见问题)
- [更新日志](#更新日志)

> 面向使用者的完整手册（每一项菜单的作用、常见问题、复位方法）在 **[docs/使用说明.md](docs/使用说明.md)**。

## 功能特性

- **无边框 + 真透明**：`WS_EX_LAYERED` + `UpdateLayeredWindow` 直接推一张 ARGB 位图，四周是真正的逐像素透明，没有黑底、没有色键毛边。
- **只显示数字**：剩余时间直接显示成秒数（`180` → `179` → … → `0`），也可以切成 `分:秒`（`03:00`）。
- **背景贴合文字**：胶囊按数字的墨迹收紧，窗口里多出来的那块 alpha 为 0 —— 鼠标在那上面直接穿透到游戏，不挡点击。3 位数字时只有约 50×21 逻辑像素能点到。
- **锁定（鼠标穿透）**：锁定后点不到、拖不动、右键也无效，稳稳压在最前面；解锁只能用托盘菜单。
- **到点提醒**：只闪字 —— 数字变红闪 6 下，不出声。
- **占用极小**：默认 176×54 逻辑像素，可缩放到 132×44 ~ 320×96。
- **托盘图标**：没有任务栏按钮，托盘图标是它的入口；图标颜色跟着状态变（灰=等待、青=计时、黄=暂停、红=完成），悬停显示剩余时间。
- **全局快捷键**：默认 `F9`，可改成 `Ctrl+Alt+F9` 之类。
- **记住一切**：位置、尺寸、透明度、字号、颜色、时间格式、锁定、时长、快捷键、置顶都存在 exe 旁边的 `meditation.ini`。

## 快速开始

1. 双击 `dist\MeditationTimer.exe`（或者把 exe 拷到任意可写目录再双击）。
2. 屏幕上方中间出现 `180`；按 `F9` 开始 / 重新计时，或者右键出菜单点「重新计时」。
3. 想要它不碍事：右键 →「背景透明度」→「全透明」，再 →「锁定（鼠标穿透）」。

> **想让快捷键在游戏里生效，就要以管理员身份运行**（或双击 `run-as-admin.bat`）。
> 游戏通常是管理员权限启动的，Windows 不允许普通进程接收高权限窗口的按键；
> 不提权时程序能正常跑，但游戏窗口聚焦时按 F9 没反应。

## 操作一览

| 操作 | 效果 |
|---|---|
| 左键拖动 | 移动位置 |
| 左键单击 | 暂停 / 继续（仅在计时中 / 暂停时） |
| 左键双击 | 从完整时长重新计时 |
| `Ctrl` + 滚轮 | 缩放窗口（每次 ±8%，132×44 ~ 320×96） |
| 右键 | 打开菜单 |
| `F9`（可改） | 全局重新计时 |
| 托盘图标双击 | 重新计时 |
| 托盘图标右键 | 显示/隐藏、重新计时、置顶、锁定、退出 |

## 界面预览

| 秒数格式 | 分:秒格式 |
|---|---|
| ![运行中](assets/preview-running.png) | ![分:秒](assets/preview-clock.png) |

到点（只闪字，不出声）：

![到点](assets/preview-finished.png)

背景调到全透明（只剩数字）：

![全透明](assets/preview-transparent.png)

右键菜单（一级、二级都是深底浅字）：

![右键菜单](assets/preview-menu.png)
![二级菜单](assets/preview-submenu.png)

## 构建

```powershell
build.bat
```

输出 `dist\MeditationTimer.exe`。只需要 Windows 自带的 .NET Framework 4.x 编译器
（`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`），**不用装 .NET SDK、Visual Studio 或任何第三方编译器**。

编译参数就是一条 `csc` 命令，见 [`build.bat`](build.bat)：`/target:winexe`、`/optimize+`、`/win32manifest:src\app.manifest`。

## 测试

```powershell
test.bat
```

编译并运行 `tests\SelfTest.cs`，覆盖 52 项逻辑：

- 倒计时状态机：开始 / 暂停 / 继续 / 结束幂等 / 秒数文本 / 剩余比例
- 两种时间格式：`Format`（秒数，向上取整）与 `FormatClock`（`MM:SS`）
- 快捷键文本 ↔ `RegisterHotKey` 参数的换算（含非法输入）
- `meditation.ini` 读写：透明度、字号、颜色、描边、时间格式、锁定、时长、快捷键、置顶、位置尺寸
- 配置解析的边界：行内注释、颜色值里的 `#` 不被当注释
- `meditation.example.ini` 能直接解析，且解析结果与本文档写的默认值一致（防止文档和程序漂移）

界面部分（分层窗口、菜单、托盘、锁定）用探针程序验证，见 [实现要点](#实现要点)。

## 配置

配置写在 exe 旁边的 `meditation.ini`，右键菜单里改任何设置都会立刻写回这个文件。
完整示例见 [`meditation.example.ini`](meditation.example.ini)，删掉 `meditation.ini` 就回到出厂默认。

| 字段 | 默认 | 取值范围 | 说明 |
|---|---|---|---|
| `opacity` | `0.85` | `0` ~ `1` | 背景透明度，`0` = 全透明只剩数字 |
| `font_px` | `0` | `0`，或 `8` ~ `96` | 字号（像素）；`0` = 跟随窗口高度自动（约 40%） |
| `text_color` | `#ffffff` | `#rrggbb` | 文字颜色 |
| `outline` | `auto` | `auto` / `none` / `#rrggbb` | 描边；`auto` = 跟文字反色，`none` = 不描边 |
| `time_format` | `seconds` | `seconds` / `clock` | `seconds` = 剩余秒数，`clock` = `分:秒` |
| `duration_seconds` | `180` | `1` ~ `10800` | 单次倒计时秒数（菜单里按分钟调，1 ~ 180） |
| `hotkey` | `F9` | 见下 | 全局快捷键 |
| `on_top` | `true` | `true` / `false` | 是否置顶 |
| `locked` | `false` | `true` / `false` | 锁定 = 整窗鼠标穿透 |
| `x` `y` | 无 | 逻辑像素 | 窗口位置；删掉这两行 = 回到屏幕上方居中 |
| `width` `height` | `176` `54` | `132`~`320` / `44`~`96` | 窗口尺寸（逻辑像素） |

- **颜色**写成 `#rrggbb`；值后面可以用空格加 `#` 或 `;` 写行内注释（颜色值里的 `#` 不会被误判）。
- **快捷键**用 `+` 连接修饰键和主键，不区分大小写：修饰键 `Ctrl` / `Alt` / `Shift` / `Win`，
  主键 `F1`~`F24`、`A`~`Z`、`0`~`9`、`Backspace` / `Tab` / `Enter` / `Pause` / `Esc` / `Space` /
  `PageUp` / `PageDown` / `End` / `Home` / `Left` / `Up` / `Right` / `Down` / `Print` / `Insert` / `Delete`。
  单键也可以（如 `F9`）。写错的话菜单里会显示「快捷键：未注册」。
- 首次运行时如果 exe 旁边或上一级目录有 Python 版的 `config.json`，会从里面读一次 `meditation/*` 的设置（只读不写）。

## 分发给别人

`dist\MeditationTimer.exe` 是单个自包含文件，直接发就行。对方需要知道的四件事：

1. **系统要有 .NET Framework 4.x** —— Win10 1903 以后和 Win11 系统自带；Win7 SP1 / Win8.1 需要装。
2. **没有代码签名**，第一次运行 SmartScreen 会拦：「Windows 已保护你的电脑」→ 点 **更多信息 → 仍要运行**。
3. **放在可写目录**（桌面、D 盘文件夹）。放 `Program Files` 也能跑，但设置写不进去（程序不会崩，只是不保存）。
4. **要用快捷键**就以管理员身份运行。

想连配置一起发，就把 `meditation.ini` 和 exe 放在一起；只想给程序就只发 exe（首次运行是默认值）。

## 系统要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 7 SP1 / 8.1 / 10 / 11 |
| 运行时 | .NET Framework 4.x（Win10 1903+、Win11 自带 4.8） |
| 架构 | AnyCPU，32 位 / 64 位都能跑 |
| 磁盘 | 一个 45 KB 的 exe；外加一个 200 字节左右的 `meditation.ini` |
| 权限 | 普通用户可运行；要用全局快捷键需与游戏同级（通常管理员） |

## 实现要点

- **为什么只有 45 KB**：不用 Qt、不打包运行时。WinForms + GDI+ 来自系统自带的 .NET Framework，
  程序本身只有 9 个源文件；对比同功能的 Qt/PySide6 版 20.7 MB，小了 400 多倍。
- **逐像素透明**：窗口加 `WS_EX_LAYERED`，每帧把 32 位 ARGB 位图通过 `UpdateLayeredWindow` 推给系统，
  不是 `WS_EX_TRANSPARENT` 那种整窗虚化。alpha = 0 的地方 Windows 会把鼠标事件交给下面的窗口。
- **命中区域**：胶囊按 `GraphicsPath.AddString` 量出来的墨迹范围收紧，可点区域就等于看到的背景；
  全透明时仍在这块留 `1/255` 的不可见底，否则整窗穿透就拖不动了。
- **锁定**：锁定后加 `WS_EX_TRANSPARENT`，整窗不再接收鼠标；解锁入口只留在托盘菜单。
- **DPI**：`app.manifest` 声明 DPI 感知 + `SetProcessDPIAware`，缩放比用 `GetDpiForWindow` 现取
  （老系统取不到时退回 `GetDeviceCaps`），文字按实际像素自绘，150% 缩放下不会糊。
- **倒计时**：用 `Stopwatch`（单调时钟）算剩余时间，不受系统时间调整影响；界面 200 ms 刷新一次。
- **菜单配色**：`ToolStripProfessionalRenderer` 的子类挂在 `ToolStripManager.Renderer` 上，
  所以一级、二级菜单都是深底浅字（子菜单是独立窗口，默认会退回浅色渲染器）。

## 目录结构

```
build.bat                 编译（用系统自带 csc）
test.bat                  编译并运行逻辑自检
run-as-admin.bat          以管理员身份启动（全局快捷键需要）
meditation.example.ini    配置示例（逐项注释）
README.md                 本文件
docs/使用说明.md           面向使用者的完整手册
assets/                   截图
dist/                     编译产物 MeditationTimer.exe
src/
  Program.cs              入口，声明 DPI 感知
  Countdown.cs            倒计时状态机（Stopwatch、毫秒、结束幂等）
  OverlayForm.cs          分层窗口自绘、右键菜单、鼠标交互、锁定、快捷键
  OverlaySettings.cs      meditation.ini 读写（含行内注释、旧版 config.json 导入）
  HotkeyBinding.cs        RegisterHotKey 与快捷键文本换算
  Dialogs.cs              时长 / 字号 / 快捷键弹窗
  DarkMenuRenderer.cs     深色菜单渲染器（一级、二级菜单共用）
  TrayIcon.cs             托盘图标与托盘菜单
  AssemblyInfo.cs         版本信息（属性里显示"打坐倒计时小窗 1.0.0.0"）
  app.manifest            DPI 感知清单
tests/
  SelfTest.cs             逻辑自检（52 项）
```

## 常见问题

| 问题 | 处理 |
|---|---|
| 按快捷键没反应 | 用管理员身份运行；或快捷键被别的软件占用（菜单底部会显示「快捷键：未注册」），换一个键 |
| 找不到窗口 | 如果已是全透明，右侧菜单看不到就点托盘图标；位置存错可以删掉 `meditation.ini` 里的 `x` / `y` 让它回到屏幕上方居中 |
| 点不到、拖不动 | 处于锁定状态，托盘菜单里取消「锁定（鼠标穿透）」 |
| 白字在浅色背景上看不见 | 右键「描边颜色」→「自动（跟文字反色）」，或把文字换成亮黄 / 青色 |
| 托盘图标找不到 | Windows 11 默认把新图标折叠在任务栏 `^` 溢出区，点开或拖到任务栏上 |
| 设置没保存 | exe 放在不可写目录了，挪到桌面或 D 盘 |
| 首次运行被 SmartScreen 拦 | 「更多信息 → 仍要运行」 |
| 杀软报可疑 | 未签名的小 exe + 注册全局热键 + 分层窗口容易进启发式规则，加信任即可 |

更多问题见 [docs/使用说明.md](docs/使用说明.md#10-常见问题)。

## 更新日志

- **1.0.0** — 首版：无边框逐像素透明、剩余秒数 / 分:秒两种格式、背景贴合文字且其余鼠标穿透、
  锁定（整窗穿透）、到点闪字提醒、托盘图标、全局快捷键、配置持久化。

## 相关

- 同功能的 Qt / PySide6 版（无 .NET 依赖、20.7 MB）在 `mhxy` 仓库的 `mhxy_assistant` 里，配置键与这里一一对应。
- 本项目暂未添加开源许可；如果要公开发布，建议先选一个（MIT / Apache-2.0 等）再加 `LICENSE`。


