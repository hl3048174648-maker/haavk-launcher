using System;
using System.IO;
using System.Security;

namespace LaunchBloom
{
    public sealed class VideoOptions
    {
        public readonly string Target;
        public readonly string Variant;
        public readonly string VideoPath;
        public readonly bool Preview;
        public readonly LaunchSpec Launch;

        private VideoOptions(string target, string variant, string videoPath, bool preview, LaunchSpec launch)
        {
            Target = target;
            Variant = variant;
            VideoPath = videoPath;
            Preview = preview;
            Launch = launch;
        }

        public static VideoOptions Parse(string[] args, string baseDirectory)
        {
            if (args == null || args.Length != 2)
                throw new ArgumentException("启动参数不完整或过多。格式：codex 01、deepseek 02 或 --preview 03。", "args");

            bool preview = String.Equals(args[0], "--preview", StringComparison.Ordinal);
            string target = String.Empty;
            if (!preview)
            {
                if (String.Equals(args[0], "codex", StringComparison.OrdinalIgnoreCase)) target = "codex";
                else if (String.Equals(args[0], "deepseek", StringComparison.OrdinalIgnoreCase)) target = "deepseek";
                else throw new ArgumentException("启动目标只能是 codex 或 deepseek；仅预览请使用 --preview。", "args");
            }

            string variant = args[1];
            if (variant != "01" && variant != "02" && variant != "03")
                throw new ArgumentException("视频版本只能是 01、02 或 03，请勿输入文件路径或网址。", "args");

            if (String.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("程序目录不能为空。请从解压后的完整文件夹运行启动器。", "baseDirectory");

            string fullDirectory;
            try
            {
                fullDirectory = Path.GetFullPath(baseDirectory);
            }
            catch (ArgumentException error) { throw InvalidDirectory(error); }
            catch (NotSupportedException error) { throw InvalidDirectory(error); }
            catch (IOException error) { throw InvalidDirectory(error); }
            catch (SecurityException error) { throw InvalidDirectory(error); }

            return new VideoOptions(target, variant,
                Path.Combine(fullDirectory, "videos", "HAAVK_" + variant + ".mp4"),
                preview, preview ? null : LaunchCore.Resolve(target));
        }

        private static ArgumentException InvalidDirectory(Exception error)
        {
            return new ArgumentException("程序目录无效或无法访问。请从解压后的完整文件夹运行启动器。", "baseDirectory", error);
        }
    }
}
