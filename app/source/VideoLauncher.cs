using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
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

        private static Button WindowButton(string id,string label,string geometry,bool isClose)
        {
            Button button=new Button { Width=36, Height=30, Padding=new Thickness(0), Background=Brushes.Transparent,
                BorderThickness=new Thickness(0), Cursor=Cursors.Arrow, ToolTip=label, FocusVisualStyle=null };
            button.Content=new System.Windows.Shapes.Path { Width=14, Height=14, Data=Geometry.Parse(geometry),
                Stroke=Brushes.White, StrokeThickness=1.2, IsHitTestVisible=false };
            System.Windows.Automation.AutomationProperties.SetAutomationId(button,id);
            System.Windows.Automation.AutomationProperties.SetName(button,label);
            FrameworkElementFactory border=new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));
            border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory content=new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            border.AppendChild(content);
            ControlTemplate template=new ControlTemplate(typeof(Button)) { VisualTree=border };
            Trigger hover=new Trigger { Property=UIElement.IsMouseOverProperty, Value=true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty,Ink(isClose ? "#D7C42B31" : "#80465062")));
            template.Triggers.Add(hover);
            button.Template=template;
            return button;
        }

        public VideoWindow(VideoOptions options)
        {
            this.options=options;
            string appName = options.Preview ? "动画预览" : "DeepSeek 网页";
            Title="HAAVK " + options.Variant + " · " + appName;
            string iconName=(options.Preview ? "haavk" : options.Target) + "-" + options.Variant + ".ico";
            string iconPath=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(options.VideoPath)),"assets",iconName);
            if(File.Exists(iconPath))
            {
                try { Icon=BitmapFrame.Create(new Uri(iconPath),BitmapCreateOptions.None,BitmapCacheOption.OnLoad); }
                catch(IOException) { } catch(NotSupportedException) { } catch(FileFormatException) { }
            }
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanMinimize;
            WindowChrome.SetWindowChrome(this,new WindowChrome { CaptionHeight=0, ResizeBorderThickness=new Thickness(0),
                GlassFrameThickness=new Thickness(0), CornerRadius=new CornerRadius(0), UseAeroCaptionButtons=false });
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

            StackPanel windowButtons=new StackPanel { Orientation=Orientation.Horizontal };
            Border controls=new Border { Child=windowButtons, Background=Ink("#B31C2028"), CornerRadius=new CornerRadius(6),
                Padding=new Thickness(2), Margin=new Thickness(8), HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top };
            WindowChrome.SetIsHitTestVisibleInChrome(controls,true);
            Button minimize=WindowButton("MinimizeWindow","最小化","M 2,8 L 12,8",false);
            minimize.ToolTip="最小化（继续播放）";
            Button maximize=WindowButton("MaximizeRestoreWindow","最大化","M 2,2 L 12,2 12,12 2,12 Z",false);
            Button close=WindowButton("CloseWindow","关闭并取消启动","M 2,2 L 12,12 M 12,2 L 2,12",true);
            minimize.Click+=delegate { WindowState=WindowState.Minimized; };
            maximize.Click+=delegate { WindowState=WindowState==WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; };
            close.Click+=delegate { Close(); };
            windowButtons.Children.Add(minimize); windowButtons.Children.Add(maximize); windowButtons.Children.Add(close);
            controls.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { e.Handled=true; };
            videoArea.Children.Add(controls);
            StateChanged+=delegate {
                bool maximized=WindowState==WindowState.Maximized;
                string label=maximized ? "还原" : "最大化";
                maximize.ToolTip=label;
                System.Windows.Automation.AutomationProperties.SetName(maximize,label);
                ((System.Windows.Shapes.Path)maximize.Content).Data=Geometry.Parse(maximized ? "M 5,2 L 12,2 12,9 M 2,5 L 9,5 9,12 2,12 Z" : "M 2,2 L 12,2 12,12 2,12 Z");
                if(opened) motionClock.Restart(); else if(loadClock.IsRunning) loadClock.Restart();
            };
            videoArea.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if(e.LeftButton == MouseButtonState.Pressed && !controls.IsMouseOver && WindowState==WindowState.Normal)
                    { try { DragMove(); } catch(InvalidOperationException) { } }
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
            // Playback remains active; a minimized compositor must not cause a false stall error.
            if(WindowState==WindowState.Minimized) return;
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
