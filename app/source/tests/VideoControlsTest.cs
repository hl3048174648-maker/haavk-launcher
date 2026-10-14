using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LaunchBloom;
public static class VideoControlsTest
{
    private static int failures;
    [STAThread] public static int Main(string[] args)
    {
        Application app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        string package=args[0], fixture=args[1];
        Directory.CreateDirectory(fixture);
        Run("Enter ends preview 01",VideoOptions.Parse(new string[]{"--preview","01"},package),"enter");
        Run("Enter ends preview 02",VideoOptions.Parse(new string[]{"--preview","02"},package),"enter");
        Run("Enter ends preview 03",VideoOptions.Parse(new string[]{"--preview","03"},package),"enter");
        Run("Esc cancels Codex",VideoOptions.Parse(new string[]{"codex","01"},package),"esc");
        Run("Close cancels DeepSeek",VideoOptions.Parse(new string[]{"deepseek","02"},package),"close");
        Run("Missing media offers exit",VideoOptions.Parse(new string[]{"--preview","03"},fixture),"error");
        Directory.CreateDirectory(Path.Combine(fixture,"videos"));
        File.WriteAllText(Path.Combine(fixture,"videos","HAAVK_01.mp4"),"invalid mp4 content");
        Run("Corrupt media offers exit",VideoOptions.Parse(new string[]{"--preview","01"},fixture),"error");
        app.Shutdown();
        Console.WriteLine(failures==0 ? "PASS: 7 video-control checks" : "FAIL: "+failures+" video-control checks");
        return failures==0 ? 0 : 1;
    }
    private static void Run(string name,VideoOptions options,string action)
    {
        VideoWindow window=new VideoWindow(options);
        window.ShowInTaskbar=false; window.ShowActivated=false;
        window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=-10000; window.Top=-10000;
        MediaElement media=Find<MediaElement>((DependencyObject)window.Content,null);
        media.MediaOpened += delegate { window.Left=-10000; window.Top=-10000; };
        bool closed=false,acted=false,timeout=false,errorObserved=false;
        Stopwatch clock=Stopwatch.StartNew();
        DispatcherFrame frame=new DispatcherFrame();
        DispatcherTimer tick=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
        tick.Tick += delegate {
            bool ready=action=="error" ? HasError((DependencyObject)window.Content) : clock.ElapsedMilliseconds>=450;
            if(!acted && ready) {
                acted=true; errorObserved=HasError((DependencyObject)window.Content);
                if(action=="close") {
                    Button button=Find<Button>((DependencyObject)window.Content,"取消启动并关闭");
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else DispatchKey(window,action=="esc" ? Key.Escape : Key.Enter);
            }
            if(clock.ElapsedMilliseconds>5000) { timeout=true; window.Close(); }
        };
        window.Closed += delegate { closed=true; tick.Stop(); frame.Continue=false; };
        window.Show(); tick.Start(); Dispatcher.PushFrame(frame);
        bool ok=closed && acted && !timeout && (action!="error" || errorObserved);
        Console.WriteLine((ok ? "PASS " : "FAIL ")+name);
        if(!ok) failures++;
    }
    private static void DispatchKey(UIElement element,Key key)
    {
        KeyEventArgs e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(element),Environment.TickCount,key);
        e.RoutedEvent=Keyboard.PreviewKeyDownEvent; element.RaiseEvent(e);
    }
    private static T Find<T>(DependencyObject node,string name) where T:DependencyObject
    {
        T found=node as T;
        if(found!=null && (name==null || System.Windows.Automation.AutomationProperties.GetName(node)==name)) return found;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) {
            T child=Find<T>(VisualTreeHelper.GetChild(node,i),name); if(child!=null) return child;
        }
        return null;
    }
    private static bool HasError(DependencyObject node)
    {
        TextBlock text=node as TextBlock;
        if(text!=null && text.Text.StartsWith("无法播放 HAAVK")) return true;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) if(HasError(VisualTreeHelper.GetChild(node,i))) return true;
        return false;
    }
}
