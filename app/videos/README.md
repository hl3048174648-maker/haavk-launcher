# 提供的 4K 动画文件

v1.0.4 使用作者提供的三个 4K MP4，内容逐字节保留，没有重新编码。这里的 4K 指文件的 3840×2160 像素尺寸；视频为 30 fps、H.264 High Level 5.1，包含 48 kHz 双声道 AAC 音轨。

三个文件均超过 GitHub 单文件 100 MiB 限制，因此通过 [v1.0.4 Release](https://github.com/hl3048174648-maker/haavk-launcher/releases/tag/v1.0.4) 分发，不放入 Git 源码。完整发行包已经包含视频；下载源码的用户需要先恢复视频。

在仓库根目录使用 Windows PowerShell 5.1 或更新版本运行：

```powershell
.\app\source\download-videos.ps1
```

若本机默认禁止运行脚本，可仅为本次 PowerShell 进程使用以下命令，不改动系统策略：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\app\source\download-videos.ps1
```

脚本默认下载到本目录，不需要登录、额外模块或工具。也可以指定目录：

```powershell
.\app\source\download-videos.ps1 -DestinationDirectory 'D:\HAAVK Launcher\videos'
```

脚本根据 [manifest.json](manifest.json) 中的 HTTPS 地址、精确字节数和 SHA-256 校验文件。已有且校验正确的文件会跳过；新下载写入同目录的独立临时文件，只有完整通过校验后才替换目标文件。下载中断或校验失败时保留原文件，并清理本次临时文件；再次运行即可重试。强制终止 PowerShell 时可能留下 `.partial-*` 文件，它们不会被播放器使用。

| Release 文件名 | 播放器文件名 | 精确字节数 | 时长 |
| --- | --- | ---: | ---: |
| `HAAVK_01_4K.mp4` | `HAAVK_01.mp4` | 708946980 | 33.0 秒 |
| `HAAVK_02_4K.mp4` | `HAAVK_02.mp4` | 743761309 | 30.8 秒 |
| `HAAVK_03_4K.mp4` | `HAAVK_03.mp4` | 657647987 | 29.8 秒 |

三个文件共 2,110,356,276 字节（约 1.97 GiB）。替换已有视频时还需要存放当前下载临时文件的空间，最大单个临时文件约 710 MiB。

也可从 Release 手动下载上述 4K 文件，按表格重命名后放入本目录，再运行脚本进行校验。只有精确大小及 SHA-256 均匹配的文件才会被跳过；完整校验值见 `manifest.json`。播放器仍使用固定的 `HAAVK_01.mp4`、`HAAVK_02.mp4`、`HAAVK_03.mp4` 路径。
