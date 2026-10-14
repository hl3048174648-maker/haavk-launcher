using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;

namespace LaunchBloom
{
    public sealed class LaunchSpec
    {
        public readonly string FileName;
        public readonly string Arguments;

        public LaunchSpec(string fileName, string arguments)
        {
            FileName = fileName;
            Arguments = arguments;
        }
    }

    public static class LaunchCore
    {
        private const int DefaultDuration = 2200;

        public static LaunchSpec Resolve(string target)
        {
            if (String.Equals(target, "codex", StringComparison.OrdinalIgnoreCase))
            {
                return new LaunchSpec(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    "shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App");
            }
            if (String.Equals(target, "deepseek", StringComparison.OrdinalIgnoreCase))
                return new LaunchSpec("https://chat.deepseek.com/", String.Empty);

            throw new ArgumentException("Target must be codex or deepseek.", "target");
        }

        public static int ReadDuration(string settingsPath)
        {
            try
            {
                foreach (string sourceLine in File.ReadLines(settingsPath))
                {
                    string line = sourceLine.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                    int separator = line.IndexOf('=');
                    if (separator < 0 || !String.Equals(line.Substring(0, separator).Trim(),
                        "duration_ms", StringComparison.OrdinalIgnoreCase)) continue;

                    string value = line.Substring(separator + 1);
                    int comment = value.IndexOfAny(new char[] { ';', '#' });
                    if (comment >= 0) value = value.Substring(0, comment);

                    int duration;
                    return Int32.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out duration)
                        && duration >= 500 && duration <= 8000 ? duration : DefaultDuration;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (SecurityException) { }

            return DefaultDuration;
        }
    }

    public sealed class LaunchGate
    {
        // Both cancellation and a launch attempt consume the gate permanently.
        private int consumed;

        public bool TryRun(Action action)
        {
            if (Interlocked.CompareExchange(ref consumed, 1, 0) != 0) return false;
            action();
            return true;
        }

        public void Cancel()
        {
            Interlocked.Exchange(ref consumed, 1);
        }
    }
}
