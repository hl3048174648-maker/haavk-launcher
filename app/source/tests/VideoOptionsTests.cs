using System;
using System.IO;
using System.Reflection;
using LaunchBloom;

internal static class VideoOptionsTests
{
    private static int passed;
    private static int failed;
    private const string BaseDirectory = @"C:\HAAVK 启动 工具";

    private static int Main()
    {
        Run("All six app/version combinations select their bundled videos and launch routes", delegate {
            foreach (string target in new string[] { "codex", "deepseek" })
            foreach (string variant in new string[] { "01", "02", "03" })
            {
                Options options = Parse(new string[] { target, variant }, BaseDirectory);
                Equal(target, options.Target);
                Equal(variant, options.Variant);
                Equal(false, options.Preview);
                Equal(@"C:\HAAVK 启动 工具\videos\HAAVK_" + variant + ".mp4", options.VideoPath);
                if (options.Launch == null) throw new Exception("App mode needs a launch route.");
                Equal(target == "codex" ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") : "https://chat.deepseek.com/", options.Launch.FileName);
                Equal(target == "codex" ? "shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App" : "", options.Launch.Arguments);
            }
        });
        Run("All preview versions have no app target or launch route", delegate {
            foreach (string variant in new string[] { "01", "02", "03" })
            {
                Options options = Parse(new string[] { "--preview", variant }, BaseDirectory);
                Equal(true, options.Preview);
                Equal(true, String.IsNullOrEmpty(options.Target));
                Equal(null, options.Launch);
                Equal(variant, options.Variant);
                Equal(@"C:\HAAVK 启动 工具\videos\HAAVK_" + variant + ".mp4", options.VideoPath);
            }
        });
        Run("App names are case-insensitive and normalized", delegate {
            Equal("codex", Parse(new string[] { "CoDeX", "01" }, BaseDirectory).Target);
            Equal("deepseek", Parse(new string[] { "DEEPSEEK", "02" }, BaseDirectory).Target);
        });
        Run("Missing media does not prevent selecting an app", delegate {
            string absent = Path.Combine(Path.GetTempPath(), "HAAVK absent " + Guid.NewGuid().ToString("N"));
            Options options = Parse(new string[] { "codex", "03" }, absent);
            Equal(Path.Combine(absent, "videos", "HAAVK_03.mp4"), options.VideoPath);
            Equal(false, File.Exists(options.VideoPath));
            Equal(false, options.Preview);
        });
        Run("Chinese and space-containing directory is canonicalized", delegate {
            Options options = Parse(new string[] { "deepseek", "02" }, @"C:\用户 资料\启动器\临时\..");
            Equal(@"C:\用户 资料\启动器\videos\HAAVK_02.mp4", options.VideoPath);
        });
        Run("Relative base directory becomes a full video path", delegate {
            Options options = Parse(new string[] { "--preview", "01" }, @"资料 目录\..");
            Equal(Path.Combine(Environment.CurrentDirectory, "videos", "HAAVK_01.mp4"), options.VideoPath);
        });
        Run("Missing and excess CLI arguments explain correct usage", delegate {
            string[][] invalid = { null, new string[0], new string[] { "codex" }, new string[] { "codex", "01", "extra" } };
            foreach (string[] args in invalid) Reject(args, BaseDirectory, "参数");
        });
        Run("Unknown targets cannot become commands or web addresses", delegate {
            foreach (string target in new string[] { null, "", " ", "cmd.exe", "https://example.com/", "../codex", " codex", "codex ", "--other" })
                Reject(new string[] { target, "01" }, BaseDirectory, "目标");
        });
        Run("Variants strictly allow only two-digit bundled identifiers", delegate {
            foreach (string variant in new string[] { null, "", "1", "001", "00", "04", "01 ", " 01", "01.mp4", "../01", @"..\02", "https://example.com/03" })
                Reject(new string[] { "codex", variant }, BaseDirectory, "版本");
        });
        Run("Preview also rejects invalid and extra arguments", delegate {
            Reject(new string[] { "--preview", "../01" }, BaseDirectory, "版本");
            Reject(new string[] { "--preview" }, BaseDirectory, "参数");
            Reject(new string[] { "--preview", "02", "codex" }, BaseDirectory, "参数");
            Reject(new string[] { "--PREVIEW", "02" }, BaseDirectory, "目标");
            Reject(new string[] { "02", "--preview" }, BaseDirectory, null);
        });
        Run("Null and blank base directories are rejected", delegate {
            foreach (string path in new string[] { null, "", " \t\r\n" })
                Reject(new string[] { "codex", "01" }, path, "目录");
        });
        Run("Invalid base paths return a Chinese argument error", delegate {
            foreach (string path in new string[] { "C:\\bad\0path", "https://example.com/files" })
                Reject(new string[] { "--preview", "03" }, path, "目录");
        });

        Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed, " + (passed + failed) + " total");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.GetType().Name + " - " + error.Message); }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!Object.Equals(expected, actual)) throw new Exception("Expected <" + expected + ">, got <" + actual + ">.");
    }

    private static void Reject(string[] args, string directory, string explanation)
    {
        try { Parse(args, directory); }
        catch (ArgumentException error)
        {
            bool hasChinese = false;
            foreach (char value in error.Message)
                if (value >= '\u4e00' && value <= '\u9fff') hasChinese = true;
            if (!hasChinese || (explanation != null && !error.Message.Contains(explanation)))
                throw new Exception("Error must explain the invalid input in Chinese: " + error.Message);
            return;
        }
        throw new Exception("Expected ArgumentException for invalid options.");
    }

    // Runtime discovery permits an executable RED run before VideoOptions exists.
    private static Options Parse(string[] args, string directory)
    {
        Type type = Assembly.GetExecutingAssembly().GetType("LaunchBloom.VideoOptions", false);
        if (type == null) throw new Exception("Missing production behavior: LaunchBloom.VideoOptions");
        MethodInfo method = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        if (method == null) throw new Exception("Missing public VideoOptions.Parse method.");
        object value;
        try { value = method.Invoke(null, new object[] { args, directory }); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        if (value == null) throw new Exception("Parse did not return options.");
        return new Options {
            Target = (string)type.GetField("Target").GetValue(value),
            Variant = (string)type.GetField("Variant").GetValue(value),
            VideoPath = (string)type.GetField("VideoPath").GetValue(value),
            Preview = (bool)type.GetField("Preview").GetValue(value),
            Launch = (LaunchSpec)type.GetField("Launch").GetValue(value)
        };
    }

    private sealed class Options
    {
        public string Target;
        public string Variant;
        public string VideoPath;
        public bool Preview;
        public LaunchSpec Launch;
    }
}
