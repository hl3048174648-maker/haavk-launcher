using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LaunchBloom
{
    public static class VideoProgram
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                VideoOptions options = VideoOptions.Parse(args, AppDomain.CurrentDomain.BaseDirectory);
                Application application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                application.Run(new VideoWindow(options));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n请使用带 HAAVK 01、02、03 的快捷方式。", "HAAVK 启动动画", MessageBoxButton.OK, MessageBoxImage.Information);
                return 1;
            }
        }
    }

    public sealed class VideoWindow : Window
    {
        private readonly VideoOptions options;
        private readonly LaunchGate gate = new LaunchGate();
        private readonly MediaElement player;
        private readonly Grid videoArea;
        private readonly TextBlock loading;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly Stopwatch loadClock = new Stopwatch();
        private readonly Stopwatch motionClock = new Stopwatch();
        private double previousPosition;
        private bool opened;
        private bool closed;
        private bool mediaError;
        private bool launchError;

        private static SolidColorBrush Ink(string value)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        private static TextBlock Text(string value, double size, string color)
        {
            return new TextBlock { Text=value, FontSize=size, Foreground=Ink(color),
                FontFamily=new FontFamily("Microsoft YaHei UI"), VerticalAlignment=VerticalAlignment.Center };
        }

        public VideoWindow(VideoOptions options)
        {
            this.options=options;
            string appName = options.Preview ? "动画预览" : (options.Target == "codex" ? "Codex" : "DeepSeek 网页");
            Title="HAAVK " + options.Variant + " · " + appName;
            string iconName=(options.Preview ? "haavk" : options.Target) + "-" + options.Variant + ".ico";
            string iconPath=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(options.VideoPath)),"assets",iconName);
            if(File.Exists(iconPath))
            {
                try { Icon=BitmapFrame.Create(new Uri(iconPath),BitmapCreateOptions.None,BitmapCacheOption.OnLoad); }
                catch(IOException) { } catch(NotSupportedException) { } catch(FileFormatException) { }
            }
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            // Opaque WPF windows allow reliable native video composition.
            Background=Brushes.Black; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Width=Math.Min(960,Math.Min(SystemParameters.WorkArea.Width*0.85,SystemParameters.WorkArea.Height*0.85*16/9));
            Height=Width*9/16;
            ShowInTaskbar=true; UseLayoutRounding=true;

            Grid root = new Grid(); Content=root;
            videoArea = new Grid { Background=Brushes.Black }; root.Children.Add(videoArea);
            player = new MediaElement { Name="AnimationVideo", LoadedBehavior=MediaState.Manual,
                UnloadedBehavior=MediaState.Manual, Stretch=Stretch.Uniform, IsMuted=false, Volume=1.0, ScrubbingEnabled=true };
            videoArea.Children.Add(player);
            loading=Text("正在载入 HAAVK " + options.Variant + "…",14,"#B4BDCA");
            loading.HorizontalAlignment=HorizontalAlignment.Center;
            loading.TextAlignment=TextAlignment.Center; loading.TextWrapping=TextWrapping.Wrap;
            loading.Margin=new Thickness(24);
            videoArea.Children.Add(loading);
            videoArea.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if(e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch(InvalidOperationException) { } }
            };

            PreviewKeyDown += delegate(object sender,KeyEventArgs e)
            {
                if(e.Key==Key.Escape) { if(launchError) Close(); else OpenTarget(); e.Handled=true; }
                else if(e.Key==Key.Enter) { e.Handled=true; }
            };
            player.MediaOpened += delegate
            {
                if(closed || mediaError) return;
                opened=true; loading.Visibility=Visibility.Collapsed; motionClock.Restart();
            };
            player.MediaEnded += delegate { if(opened && !closed && !mediaError) OpenTarget(); };
            player.MediaFailed += delegate(object sender,ExceptionRoutedEventArgs e) { ShowMediaError(e.ErrorException.Message); };
            timer.Interval=TimeSpan.FromMilliseconds(100);
            timer.Tick += delegate { CheckPlayback(); };
            Loaded += delegate
            {
                if(!File.Exists(options.VideoPath)) { ShowMediaError("视频文件不存在：" + options.VideoPath); return; }
                try { loadClock.Start(); timer.Start(); player.Source=new Uri(options.VideoPath); player.Play(); Focus(); }
                catch(Exception ex) { ShowMediaError(ex.Message); }
            };
            Closed += delegate { closed=true; gate.Cancel(); StopPlayback(); };
        }

        private void CheckPlayback()
        {
            if(closed || mediaError) return;
            if(!opened)
            {
                if(loadClock.Elapsed.TotalSeconds>15) ShowMediaError("视频载入超时。");
                return;
            }
            double position=player.Position.TotalSeconds;
            if(position>previousPosition+0.01) { previousPosition=position; motionClock.Restart(); }
            if(motionClock.Elapsed.TotalSeconds>12) ShowMediaError("视频播放没有继续前进。");
        }

        private void ShowMediaError(string detail)
        {
            if(closed || launchError || mediaError) return;
            mediaError=true; StopPlayback();
            loading.Visibility=Visibility.Visible;
            loading.Text="无法播放 HAAVK " + options.Variant + "\n" + (options.Preview ? "按 Esc 结束预览" : "按 Esc 继续打开");
            loading.Foreground=Ink("#F2B9A8");
            loading.ToolTip=detail;
        }

        private void StopPlayback()
        {
            timer.Stop(); loadClock.Stop(); motionClock.Stop();
            try { player.Close(); } catch(InvalidOperationException) { }
        }

        private void OpenTarget()
        {
            if(closed) return;
            StopPlayback();
            try
            {
                bool ran=gate.TryRun(delegate
                {
                    if(!options.Preview)
                        Process.Start(new ProcessStartInfo { FileName=options.Launch.FileName, Arguments=options.Launch.Arguments,
                            UseShellExecute=true, WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory });
                });
                if(ran) Close();
            }
            catch(Exception ex)
            {
                launchError=true; loading.Visibility=Visibility.Visible;
                loading.Text="目标未能打开\n请确认软件或默认浏览器可用，按 Esc 关闭";
                loading.ToolTip=ex.Message; loading.Foreground=Ink("#F2B9A8");
            }
        }
    }
}
