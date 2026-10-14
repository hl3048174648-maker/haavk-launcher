# 构建与测试

所有命令从仓库根目录运行。项目使用 Windows 自带的 .NET Framework 编译器，直接生成 WPF EXE；不需要 NuGet、Node.js 或额外的 .NET SDK。

## 环境

- Windows 10/11 的 64 位桌面环境。
- .NET Framework 4.x 及其 WPF 组件。
- 系统可播放 3840×2160、30 fps 的 H.264 High Level 5.1 / AAC 文件；播放器为 WPF `MediaElement`。
- PowerShell；创建快捷方式另需 Windows Script Host。
- 测试播放和窗口交互时，需要已登录的交互式桌面会话。

浏览器不随包提供。预览和自动化测试只处理本地视频，不应打开 DeepSeek 网页。

## 准备 4K 视频

完整发布 ZIP 已包含视频。`git clone` 和 GitHub 自动生成的 Source code 压缩包不含视频 MP4；它们提供源码、媒体清单与下载脚本。运行播放测试或创建快捷方式前，先下载视频：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\download-videos.ps1
```

脚本使用 `app/videos/manifest.json` 中的发布地址、文件大小及 SHA-256 校验值，将三个 4K 发布文件保存为播放器使用的名称：

| 发布文件 | 本地播放路径 | 字节数 |
| --- | --- | ---: |
| `HAAVK_01_4K.mp4` | `app/videos/HAAVK_01.mp4` | 708,946,980 |
| `HAAVK_02_4K.mp4` | `app/videos/HAAVK_02.mp4` | 743,761,309 |
| `HAAVK_03_4K.mp4` | `app/videos/HAAVK_03.mp4` | 657,647,987 |

媒体来自作者提供的 3840×2160、30 fps 文件；发布和下载仅映射文件名，不重新编码视频或改动音轨。三个文件合计约 2.11 GB。SHA-256 和字节数一致用于确认文件完整，分辨率不应根据启动窗口或录屏尺寸推断。既有 1080p 文件未修改。

下载脚本会跳过大小和 SHA-256 均已符合清单的文件；不符合的文件仅在新下载通过这两项校验后替换。脚本支持 `-DestinationDirectory` 和 `-ManifestPath`，默认使用 `app/videos` 及其中的 `manifest.json`。

## 构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\build.ps1
```

`build.ps1` 从 `$env:WINDIR` 定位 `Microsoft.NET/Framework64/v4.0.30319/csc.exe`，引用 Framework/WPF 程序集，编译 `LaunchCore.cs`、`VideoOptions.cs` 和 `VideoLauncher.cs`，并嵌入 `app.manifest` 与 `app/assets/launch.ico`。

输出为 `app/HAAVK-Launch.exe`，会覆盖已有的同名 EXE。视频和图标通过相对程序目录读取，发布时须保留 `app` 内的目录结构。清单使用 `asInvoker`，启动器不要求管理员权限。

## 自动化测试

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\run-tests.ps1
```

测试脚本编译并运行现有测试程序：

| 测试源码 | 检查内容 |
| --- | --- |
| `VideoOptionsTests.cs` | DeepSeek 的三种主题组合、预览模式、路径处理，以及不支持的目标和非法参数拒绝，共 14 组 |
| `VideoControlsTest.cs` | 检查原声音轨开启、视频比例、右上角最小化/最大化还原/关闭控件、Enter 不跳过、Esc 结束预览、系统关闭，以及缺失和损坏视频的 Esc 提示 |
| `PlaybackTest.cs` | 三个主题均能载入、持续推进至完整片尾并收到 `MediaEnded`，不提前截断 |

完整播放测试需要等待三段视频，至少约 94 秒，另有编译和载入时间。窗口控制与播放测试均使用预览模式，不执行目标应用的启动。WPF 播放依赖当前系统的媒体组件和桌面会话；测试失败时先查看输出的具体错误。

默认测试产物写入 `app/source/test-results`，该目录已加入 `.gitignore`。可用 `-OutputDirectory` 指定其他位置；只运行参数和交互检查时，可加 `-SkipPlayback`：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\run-tests.ps1 -SkipPlayback -OutputDirectory .\.test-output
```

## 手动验证

```powershell
.\app\HAAVK-Launch.exe --preview 01
.\app\HAAVK-Launch.exe --preview 02
.\app\HAAVK-Launch.exe --preview 03
```

确认三段动画的右上角均显示最小化、最大化/还原和关闭按钮，视频区域没有底部控制栏或进度条。普通窗口保持 16:9；最大化后视频仍保持原始比例，不拉伸或裁切，再次点击能还原。原视频音轨始终开启，预览播放至片尾自动关闭。分别检查 Enter 不触发操作、Esc 结束预览、×/Alt+F4 关闭，以及拖动窗口。实际听到的音量取决于 Windows 音量和输出设备。

最小化后，确认声音和播放继续推进；在片尾前从任务栏还原，应能继续看到当前画面。预览模式即使保持最小化，也应在播完后自行关闭。

准备验证实际目标时，运行以下命令：

```powershell
.\app\HAAVK-Launch.exe deepseek 02
```

动画结束或按 Esc 后，通过默认浏览器打开 `https://chat.deepseek.com/`。确认 Enter 不跳过，×/Alt+F4 或系统关闭窗口会取消本次启动；保持最小化直到片尾仍应打开网页。视频失败时，中央错误提示应说明可按 Esc 继续打开网页，× 仍可取消。目标路由定义在 `LaunchCore.cs`。

快捷方式安装脚本默认写入当前用户桌面；也可传入一个已存在的测试目录，先检查生成的三个 DeepSeek 入口：

```powershell
New-Item -ItemType Directory -Force .\.test-output\shortcuts | Out-Null
$shortcutFolder = (Resolve-Path .\.test-output\shortcuts).Path
cscript //nologo .\app\创建DeepSeek桌面快捷方式.vbs $shortcutFolder
```

在命令末尾添加 `preview` 会在指定目录创建三个预览入口。安装脚本只新增快捷方式，不删除已有入口；同名入口会添加序号。

```powershell
cscript //nologo .\app\创建DeepSeek桌面快捷方式.vbs $shortcutFolder preview
```

## 图标与演示

图标生成脚本保留 HAAVK 预览入口的几何图标，并从 `app/assets/brand` 中的产品图像打包 DeepSeek 的 ICO。DeepSeek 入口不添加 H 字样或版本角标。产品标识权利说明见根目录 `THIRD_PARTY_NOTICES.md`。需要重新生成时，指定一个本地预览输出位置，再编译 EXE 以更新嵌入的图标：

```powershell
New-Item -ItemType Directory -Force .\.test-output | Out-Null
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\generate-icons.ps1 -PreviewPath .\.test-output\icons.png
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\build.ps1
```

`record-demos.ps1` 保留为开发验证工具。它录制实际 WPF 预览窗口，默认按窗口尺寸输出；普通窗口小于 4K 时，录屏也小于 4K，不能将它当作 4K 文件或分辨率证明。运行此工具另需 `ffmpeg` 位于 PATH 中，建议将本地录制放入测试输出目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\record-demos.ps1 -OutputDirectory .\.test-output\recordings
```

不指定 `-OutputDirectory` 时，脚本默认输出到 `docs/demos`；另支持 `-FFmpeg` 参数。录制会运行预览窗口，需保持交互式桌面可用，并等待三个主题依次完成。它编译的 `RecordDemo.exe` 是本地辅助程序，已通过 `.gitignore` 排除。

录制器观察播放器时钟，将原视频音轨对齐后编码为 AAC 合入窗口录屏；它不录制桌面音频。录制验证检查片尾事件、播放位置和播放器原声音轨是否开启，并报告窗口尺寸。这类开发录屏会重新编码；发布的三个 `_4K.mp4` 则直接使用作者提供的文件，未经过该录制流程。

README 的 `docs/demos/HAAVK-03-4K.jpg` 是 4K 视频第 8 秒的源文件取帧，不是窗口录屏。公开预览与下载入口指向 Release 中的三个 4K MP4。

## 发布内容

完整发布 ZIP 应包含 EXE、`assets`、三段完整 4K 视频、媒体清单、`source`、`创建DeepSeek桌面快捷方式.vbs` 和三个预览脚本，约 2 GB。三段 4K 文件同时作为独立 Release 资产发布，方便查看或单独下载。ZIP 内使用 `HAAVK_01.mp4` 等播放器文件名，内容必须与对应 `_4K.mp4` 字节一致。

源码仓库不跟踪 MP4；保留 `app/videos/manifest.json` 与下载脚本。旧的 720p 播放副本和窗口尺寸演示 MP4 不再作为当前版本的仓库媒体分发。源码压缩包不是完整运行包，必须先下载并校验视频。

根目录保留 README、MIT 许可、视频资产许可及第三方标识说明。代码与自绘几何图标采用 MIT，三部动画及其取帧内容采用 CC BY 4.0；产品标识保留各自权利。不要将测试产物、日志、桌面 `.lnk`、开发录屏或个人路径配置加入完整发布包。
