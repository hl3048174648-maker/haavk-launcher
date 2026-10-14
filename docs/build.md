# 构建与测试

所有命令从仓库根目录运行。项目使用 Windows 自带的 .NET Framework 编译器，直接生成 WPF EXE；不需要 NuGet、Node.js 或额外的 .NET SDK。

## 环境

- Windows 10/11 的 64 位桌面环境。
- .NET Framework 4.x 及其 WPF 组件。
- 系统可播放 H.264/AAC；播放器为 WPF `MediaElement`。
- PowerShell；创建快捷方式另需 Windows Script Host。
- 测试播放和窗口交互时，需要已登录的交互式桌面会话。

Codex 桌面应用和浏览器不随包提供。仅执行预览或自动化测试不要求安装 Codex，也不应打开 Codex 或 DeepSeek。

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
| `VideoOptionsTests.cs` | 六种目标/主题组合、预览模式、路径处理，以及非法参数拒绝，共 12 组 |
| `VideoControlsTest.cs` | Enter 结束预览、Esc/关闭取消启动、缺失和损坏视频处理，共 7 项 |
| `PlaybackTest.cs` | 三个主题均能载入、持续推进至完整片尾并收到 `MediaEnded`，不提前截断 |

完整播放测试需要等待三段视频，至少约 94 秒，另有编译和载入时间。测试使用预览或取消路径，不执行目标应用的启动。WPF 播放依赖当前系统的媒体组件和桌面会话；测试失败时先查看输出的具体错误。

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

确认三段动画均以原始比例播放，默认静音，声音按钮可切换，进度推进且片尾自动关闭。分别检查 Enter 结束预览、Esc 取消，以及拖动窗口。

准备验证实际目标时，运行以下命令：

```powershell
.\app\HAAVK-Launch.exe codex 01
.\app\HAAVK-Launch.exe deepseek 02
```

第一条在动画结束或按 Enter 后，通过 Windows `shell:AppsFolder` 打开已安装的 `OpenAI.Codex_2p2nqsd0c76g0!App`；第二条通过默认浏览器打开 `https://chat.deepseek.com/`。目标路由定义在 `LaunchCore.cs`。

快捷方式安装脚本默认写入当前用户桌面；也可传入一个已存在的测试目录，先检查生成的六个入口：

```powershell
New-Item -ItemType Directory -Force .\.test-output\shortcuts | Out-Null
$shortcutFolder = (Resolve-Path .\.test-output\shortcuts).Path
cscript //nologo .\app\创建六个桌面快捷方式.vbs $shortcutFolder
```

## 图标与演示

图标生成脚本保留 HAAVK 预览入口的几何图标，并从 `app/assets/brand` 中的产品图像打包 Codex / OpenAI 和 DeepSeek 的 ICO。AI 入口不添加 H 字样或版本角标。产品标识权利说明见根目录 `THIRD_PARTY_NOTICES.md`。需要重新生成时，指定一个本地预览输出位置，再编译 EXE 以更新嵌入的图标：

```powershell
New-Item -ItemType Directory -Force .\.test-output | Out-Null
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\generate-icons.ps1 -PreviewPath .\.test-output\icons.png
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\build.ps1
```

录制三种主题的窗口演示另需 `ffmpeg` 位于 PATH 中：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\source\record-demos.ps1
```

脚本默认输出到 `docs/demos`，也支持 `-OutputDirectory` 和 `-FFmpeg` 参数。录制会运行预览窗口，需保持交互式桌面可用，并等待三个主题依次完成。它编译的 `RecordDemo.exe` 是本地辅助程序，已通过 `.gitignore` 排除。

## 发布内容

`app` 应包含 EXE、`assets`、三段优化视频、`source`、创建快捷方式脚本和三个预览脚本。根目录保留 README、MIT 许可、视频资产许可及第三方标识说明。代码与自绘几何图标采用 MIT，三段原作及演示中的动画内容采用 CC BY 4.0；产品标识保留各自权利。

不要将原始 1080p 大体积媒体、测试产物、日志、桌面 `.lnk` 或个人路径配置加入发布包。演示视频使用 `docs/demos/HAAVK-01-demo.mp4`、`HAAVK-02-demo.mp4`、`HAAVK-03-demo.mp4` 命名。
