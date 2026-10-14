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
        private readonly TextBlock status;
        private readonly TextBlock loading;
        private readonly Border progress;
        private readonly Border rail;
        private readonly Button muteButton;
        private readonly Button openButton;
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

        private static Button Button(string value, string foreground, string background)
        {
            Button button = new Button { Content=value, FontSize=12, FontFamily=new FontFamily("Microsoft YaHei UI"),
                Foreground=Ink(foreground), Background=Ink(background), BorderThickness=new Thickness(0),
                Padding=new Thickness(16,9,16,9), Cursor=Cursors.Hand };
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree=border };
            button.MouseEnter += delegate { button.Opacity=0.8; };
            button.MouseLeave += delegate { button.Opacity=1; };
            return button;
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
            string accent=options.Variant=="02" ? "#D1ACFA" : (options.Variant=="03" ? "#FF8C91" : "#B7E3D0");
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            // Opaque WPF windows allow reliable native video composition.
            Background=Brushes.Black; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Width=Math.Min(960, SystemParameters.WorkArea.Width * 0.85);
            Height=Width * 9 / 16 + 82;
            ShowInTaskbar=true; UseLayoutRounding=true;

            Grid root = new Grid(); Content=root;
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(2) });
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(80) });
            videoArea = new Grid { Background=Brushes.Black }; root.Children.Add(videoArea);
            player = new MediaElement { Name="AnimationVideo", LoadedBehavior=MediaState.Manual,
                UnloadedBehavior=MediaState.Manual, Stretch=Stretch.Uniform, IsMuted=true, ScrubbingEnabled=true };
            videoArea.Children.Add(player);
            loading=Text("正在载入 HAAVK " + options.Variant + "…",14,"#B4BDCA");
            loading.HorizontalAlignment=HorizontalAlignment.Center;
            videoArea.Children.Add(loading);
            Button close=Button("×","#FFFFFF","#282B32"); close.Width=34; close.Height=32; close.FontSize=20;
            close.Padding=new Thickness(0); close.HorizontalAlignment=HorizontalAlignment.Right;
            close.VerticalAlignment=VerticalAlignment.Top; close.Margin=new Thickness(12);
            System.Windows.Automation.AutomationProperties.SetName(close,"取消启动并关闭");
            close.Click += delegate { CancelAndClose(); }; videoArea.Children.Add(close);
            videoArea.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if(e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch(InvalidOperationException) { } }
            };

            rail = new Border { Background=Ink("#242830") }; Grid.SetRow(rail,1); root.Children.Add(rail);
            progress = new Border { Width=0, HorizontalAlignment=HorizontalAlignment.Left, Background=Ink(accent) };
            rail.Child=progress;
            Grid footer = new Grid { Background=Ink("#111318"), Margin=new Thickness(0) };
            Grid.SetRow(footer,2); root.Children.Add(footer);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            StackPanel labels = new StackPanel { VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(22,0,10,0) };
            labels.Children.Add(Text("HAAVK " + options.Variant + "   /   " + appName,13,"#F3F4F7"));
            status=Text(options.Preview ? "仅播放预览 · 不会打开软件" : "完整播放后打开 · Enter 跳过 · Esc 取消",11,"#8D98AA");
            status.Margin=new Thickness(0,7,0,0); labels.Children.Add(status); footer.Children.Add(labels);
            StackPanel actions = new StackPanel { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,18,0) };
            Grid.SetColumn(actions,1); footer.Children.Add(actions);
            muteButton=Button("静音","#AEB9CA","#222731"); muteButton.Margin=new Thickness(0,0,10,0);
            muteButton.ToolTip="开启或关闭视频声音";
            muteButton.Click += delegate { player.IsMuted=!player.IsMuted; muteButton.Content=player.IsMuted ? "静音" : "有声"; };
            actions.Children.Add(muteButton);
            openButton=Button(options.Preview ? "结束预览" : "立即打开  ↗","#12221C",accent);
            openButton.Click += delegate { if(launchError) Close(); else OpenTarget(); }; actions.Children.Add(openButton);

            PreviewKeyDown += delegate(object sender,KeyEventArgs e)
            {
                if(e.Key==Key.Escape) { CancelAndClose(); e.Handled=true; }
                if(e.Key==Key.Enter) { if(launchError) Close(); else OpenTarget(); e.Handled=true; }
            };
            player.MediaOpened += delegate
            {
                if(closed || mediaError) return;
                opened=true; loading.Visibility=Visibility.Collapsed; motionClock.Restart();
                muteButton.IsEnabled=player.HasAudio;
                if(!player.HasAudio) { muteButton.Content="无音轨"; muteButton.ToolTip="这段视频没有声音"; }
                if(player.NaturalVideoWidth>0 && player.NaturalVideoHeight>0)
                    ResizeForVideo((double)player.NaturalVideoHeight / player.NaturalVideoWidth);
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

        private void ResizeForVideo(double ratio)
        {
            Rect area=SystemParameters.WorkArea;
            double width=Math.Min(960,area.Width*0.85);
            double height=width*ratio+82;
            if(height>area.Height*0.85) { height=area.Height*0.85; width=(height-82)/ratio; }
            Width=width; Height=height;
            Left=area.Left+(area.Width-width)/2; Top=area.Top+(area.Height-height)/2;
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
            if(player.NaturalDuration.HasTimeSpan && player.NaturalDuration.TimeSpan.TotalSeconds>0)
            {
                double total=player.NaturalDuration.TimeSpan.TotalSeconds;
                progress.Width=Math.Max(0,rail.ActualWidth*Math.Min(1,position/total));
                status.Text=(options.Preview ? "仅预览" : "播放后打开") + "   " + FormatTime(position) + " / " + FormatTime(total) + "   ·   Enter 跳过 · Esc 取消";
            }
            if(motionClock.Elapsed.TotalSeconds>12) ShowMediaError("视频播放没有继续前进。");
        }

        private static string FormatTime(double seconds)
        {
            TimeSpan span=TimeSpan.FromSeconds(Math.Max(0,seconds));
            return ((int)span.TotalMinutes).ToString("00") + ":" + span.Seconds.ToString("00");
        }

        private void ShowMediaError(string detail)
        {
            if(closed || launchError || mediaError) return;
            mediaError=true; StopPlayback();
            loading.Visibility=Visibility.Visible;
            loading.Text="无法播放 HAAVK " + options.Variant;
            loading.ToolTip=detail;
            status.Text=options.Preview ? "请检查 videos 文件夹中的视频" : "仍可点击“立即打开”进入目标";
            status.Foreground=Ink("#F2B9A8"); status.ToolTip=detail;
            muteButton.IsEnabled=false;
        }

        private void StopPlayback()
        {
            timer.Stop(); loadClock.Stop(); motionClock.Stop();
            try { player.Close(); } catch(InvalidOperationException) { }
        }

        private void CancelAndClose()
        {
            gate.Cancel(); Close();
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
                launchError=true; loading.Visibility=Visibility.Visible; loading.Text="目标未能打开";
                status.Text="请确认软件或默认浏览器可用"; status.ToolTip=ex.Message;
                status.Foreground=Ink("#F2B9A8"); openButton.Content="关闭"; muteButton.IsEnabled=false;
            }
        }
    }
}
