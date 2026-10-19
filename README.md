# 打坐倒计时小窗（C# 版）

梦幻西游打坐倒计时悬浮窗：**无边框、逐像素透明、可置顶**，默认只占 176×54 像素，
窗口里只有一串数字，没有边框、没有进度条、没有提示音；显示格式可以在
**剩余秒数**（`180` → `179` → … → `0`）和 **分:秒**（`03:00`）之间切换。

用 **C# + WinForms + GDI+** 写成，编译产物 **约 42 KB**（对比 Qt/PySide6 版 20.7 MB），
运行时只依赖系统自带的 .NET Framework 4.x，编译只依赖 Windows 自带的 `csc.exe`。

![空闲状态](assets/preview-idle.png)

## 功能

- **无边框 + 真透明**：`WS_EX_LAYERED` + `UpdateLayeredWindow` 推 ARGB 位图，四周真正透明，没有黑底或色键毛边。
- **只有数字**：不画边框、不画进度环，背景可以调到全透明，只剩数字浮在画面上。
- **时间格式可切换**：右键菜单「时间格式」里选「剩余秒数（180）」或「分:秒（03:00）」，选完写进 `meditation.ini`。
- **锁定（鼠标穿透）**：菜单里点「锁定（鼠标穿透）」，小窗就变成一块完全不碰鼠标的贴纸 —— 点不到、拖不动、右键也无效，稳稳压在游戏最前面；解锁只能用**托盘菜单**里的同一项。
- **背景贴合文字、其余鼠标穿透**：胶囊按数字的墨迹收紧（横竖都贴），窗口里多出来的那块 alpha 为 0，鼠标会直接穿透到游戏里。3 位数字时只有约 50×21 逻辑像素能点到，其余全穿透。
- **占用极小**：默认 176×54 逻辑像素，最小 132×44，滚轮 + Ctrl 可缩放到 320×96。
- **到点提醒**：只闪字 —— 显示 `0` 并闪几下红色，不出声。
- **可调外观**：背景透明度（含全透明）、字号（自动/14~34px/自定义 8~96px）、文字颜色、描边颜色（自动反色/无描边/预设/取色板）。
- **全局快捷键**：默认 F9（可改），按下即从完整时长重新开始。
- **托盘图标**：小窗没有任务栏按钮（`WS_EX_TOOLWINDOW`），托盘图标就是它的入口 —— 图标颜色跟着状态变（灰=等待、青=计时、黄=暂停、红=完成），鼠标悬停显示剩余时间，双击重新计时，右键出「显示/隐藏、重新计时、置顶、锁定、退出」。
- **鼠标操作**：左键拖动移动，单击暂停/继续，双击重新计时，右键出菜单。
- **记住一切**：位置、尺寸、透明度、字号、颜色、时间格式、锁定、时长、快捷键、置顶开关都存在 exe 旁边的 `meditation.ini`。

| 秒数格式 | 分:秒格式 |
|---|---|
| ![运行中](assets/preview-running.png) | ![分:秒](assets/preview-clock.png) |

到点（只闪字）：

![到点](assets/preview-finished.png)

背景调到全透明（只剩数字）：

![全透明](assets/preview-transparent.png)

右键菜单（一级 / 二级都是深底浅字）：

![右键菜单](assets/preview-menu.png)
![二级菜单](assets/preview-submenu.png)

## 运行

`dist\MeditationTimer.exe` 已经预编译好，双击即可。

> **建议以管理员身份运行**（或双击 `run-as-admin.bat`）：游戏通常是管理员权限启动的，
> Windows 不允许普通进程接收高权限窗口的按键，不提权的话全局快捷键在游戏窗口聚焦时不响应。

## 构建

```powershell
build.bat
```

输出 `dist\MeditationTimer.exe`。只需要 Windows 自带的 .NET Framework 4.x 编译器
（`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`），**不用装 .NET SDK、Visual Studio 或任何第三方编译器**。

## 测试

```powershell
test.bat
```

会编译并运行 `tests\SelfTest.cs`，覆盖 41 项逻辑：倒计时状态机（开始/暂停/继续/结束幂等）、两种时间格式（秒数 / `MM:SS`）、
快捷键文本与 `RegisterHotKey` 参数换算、`meditation.ini` 读写与取值范围。

## 配置

配置写在 exe 旁边的 `meditation.ini`，右键菜单里改的任何设置都会立刻写回这个文件。
各字段的含义见 [`meditation.example.ini`](meditation.example.ini)：

```ini
opacity=0.85          # 背景透明度 0~1，0 = 全透明，只剩下数字
font_px=0             # 字号（像素），0 = 跟着窗口高度自动缩放
text_color=#ffffff    # 文字颜色
outline=auto          # 描边：auto = 跟文字反色 / none = 不描边 / #rrggbb = 固定颜色
time_format=seconds   # 时间格式：seconds = 剩余秒数（180）/ clock = 分:秒（03:00）
duration_seconds=180  # 倒计时时长
hotkey=F5             # 全局快捷键，例如 F9、Ctrl+Alt+F9
on_top=true           # 是否置顶
locked=false          # 锁定：true = 整窗鼠标穿透，只能用托盘菜单解锁
x=1788                # 窗口位置（逻辑像素），删掉就回到屏幕上方居中
y=36
width=176             # 窗口尺寸（逻辑像素）
height=54
```

删掉 `meditation.ini` 就回到出厂默认。

> 背景是逐像素透明的：胶囊以外的地方 alpha 为 0，Windows 会把鼠标事件直接交给下面的窗口，
> 所以小窗不会挡住游戏里的点击。透明度调到 0 时只剩数字，数字那块留了 1/255 的不可见底，
> 保证还能拖动、右键、双击；想完全穿透（连数字也不挡）可以点托盘菜单里的「隐藏小窗」。

## 目录结构

```
build.bat                 编译（用系统自带 csc）
test.bat                  编译并运行逻辑自检
run-as-admin.bat          以管理员身份启动（全局快捷键需要）
meditation.example.ini    配置示例
assets/                   截图
dist/                     编译产物 MeditationTimer.exe
src/
  Program.cs              入口，声明 DPI 感知
  Countdown.cs            倒计时状态机（毫秒、单调时钟）
  OverlayForm.cs          分层窗口自绘、右键菜单、鼠标交互、快捷键
  OverlaySettings.cs      meditation.ini 读写
  HotkeyBinding.cs        RegisterHotKey 与快捷键文本换算
  Dialogs.cs              时长 / 字号 / 快捷键弹窗
  DarkMenuRenderer.cs     深色菜单渲染器（一级、二级菜单共用）
  TrayIcon.cs             托盘图标与托盘菜单
  app.manifest            DPI 感知清单
tests/
  SelfTest.cs             逻辑自检（41 项）
```

## 说明

- 需要 **.NET Framework 4.x**：Windows 10 1903 以后和 Windows 11 都是系统自带。
- 程序按系统 DPI 缩放自绘（`SetProcessDPIAware` + `GetDpiForWindow`），150% 缩放下文字不会被位图拉伸糊掉。
- 只支持 Windows。
- 托盘图标默认可能在任务栏的 **^ 溢出区**里（Windows 对新出现的图标默认折叠），把它拖到任务栏上就常驻了。
  拖动它之前也可以直接从溢出区里点到，菜单一样能用。
- 同一份功能还有一个 Qt/PySide6 版本（无 .NET 依赖、体积 20.7 MB），在 `mhxy` 仓库里，
  配置键与这里一一对应；本程序首次运行时会从它的 `config.json` 里导入一次设置。




