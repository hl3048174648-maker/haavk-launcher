using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LaunchBloom;
public static class VideoControlsTest
{
    private static int failures;
    private static int checks;
    [STAThread] public static int Main(string[] args)
    {
        Application app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        string package=args[0], fixture=args[1];
        Directory.CreateDirectory(fixture);
        Run("01: sound, full video, Enter ignored, Esc skips",VideoOptions.Parse(new string[]{"--preview","01"},package),"enter");
        Run("02: sound, full video, Enter ignored, Esc skips",VideoOptions.Parse(new string[]{"--preview","02"},package),"enter");
        Run("03: sound, full video, Enter ignored, Esc skips",VideoOptions.Parse(new string[]{"--preview","03"},package),"enter");
        Run("Esc skips a preview directly",VideoOptions.Parse(new string[]{"--preview","01"},package),"esc");
        Run("OS window close cancels a preview",VideoOptions.Parse(new string[]{"--preview","02"},package),"close");
        Run("Missing media keeps Enter inert and permits Esc",VideoOptions.Parse(new string[]{"--preview","03"},fixture),"error");
        Directory.CreateDirectory(Path.Combine(fixture,"videos"));
        File.WriteAllText(Path.Combine(fixture,"videos","HAAVK_01.mp4"),"invalid mp4 content");
        Run("Corrupt media keeps Enter inert and permits Esc",VideoOptions.Parse(new string[]{"--preview","01"},fixture),"error");
        foreach(string variant in new string[]{"01","02","03"}) RunWindowButtons(VideoOptions.Parse(new string[]{"--preview",variant},package));
        app.Shutdown();
        Console.WriteLine(failures==0 ? "PASS: "+checks+" video-control checks" : "FAIL: "+failures+" of "+checks+" video-control checks");
        return failures==0 ? 0 : 1;
    }
    private static void Run(string name,VideoOptions options,string action)
    {
        checks++;
        // Synthetic skip keys must never reach a real app target.
        if(!options.Preview) throw new Exception("Control tests require preview mode.");
        VideoWindow window=new VideoWindow(options);
        window.ShowInTaskbar=false; window.ShowActivated=false;
        window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=-10000; window.Top=-10000;
        MediaElement media=Find<MediaElement>((DependencyObject)window.Content,null);
        bool opened=false;
        media.MediaOpened += delegate { opened=true; window.Left=-10000; window.Top=-10000; };
        bool closed=false,acted=false,timeout=false,errorObserved=false,enterIgnored=false,finishSent=false;
        long enteredAt=0;
        List<string> problems=new List<string>();
        Stopwatch clock=Stopwatch.StartNew();
        DispatcherFrame frame=new DispatcherFrame();
        DispatcherTimer tick=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
        tick.Tick += delegate {
            bool ready=action=="error" ? HasError((DependencyObject)window.Content) :
                (action=="enter" ? opened && clock.ElapsedMilliseconds>=450 : clock.ElapsedMilliseconds>=150);
            if(!acted && ready) {
                acted=true; errorObserved=HasError((DependencyObject)window.Content);
                CheckLayoutAndSound(window,media,problems);
                if(action=="error" && !ErrorMentionsEscape((DependencyObject)window.Content)) problems.Add("media error lacks Esc guidance");
                if(action=="close") {
                    finishSent=true; window.Close();
                }
                else if(action=="esc") { finishSent=true; DispatchKey(window,Key.Escape); }
                else {
                    enteredAt=clock.ElapsedMilliseconds;
                    if(!DispatchKey(window,Key.Enter)) problems.Add("Enter was not consumed");
                }
            }
            if(acted && !finishSent && !closed && clock.ElapsedMilliseconds-enteredAt>=250) {
                enterIgnored=window.IsVisible;
                finishSent=true; DispatchKey(window,Key.Escape);
            }
            if(clock.ElapsedMilliseconds>8000) { timeout=true; window.Close(); }
        };
        window.Closed += delegate { closed=true; tick.Stop(); frame.Continue=false; };
        window.Show(); tick.Start(); Dispatcher.PushFrame(frame);
        if(!closed || !acted || !finishSent) problems.Add("did not reach the intended close action");
        if(timeout) problems.Add("timed out");
        if(action=="error" && !errorObserved) problems.Add("media error was not observed");
        if((action=="enter" || action=="error") && !enterIgnored) problems.Add("Enter skipped or closed the preview");
        bool ok=problems.Count==0;
        Console.WriteLine((ok ? "PASS " : "FAIL ")+name+(ok ? "" : ": "+String.Join("; ",problems.ToArray())));
        if(!ok) failures++;
    }
    private static bool DispatchKey(UIElement element,Key key)
    {
        KeyEventArgs e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(element),Environment.TickCount,key);
        e.RoutedEvent=Keyboard.PreviewKeyDownEvent; element.RaiseEvent(e);
        return e.Handled;
    }
    private static void CheckLayoutAndSound(VideoWindow window,MediaElement media,List<string> problems)
    {
        // Media metadata can arrive before WPF's deferred measure/arrange pass.
        window.UpdateLayout();
        if(media.IsMuted || Math.Abs(media.Volume-1.0)>0.001) problems.Add("audio is not unmuted at full volume");
        if(CountButtons((DependencyObject)window.Content)!=3) problems.Add("expected exactly three window controls");
        if(Math.Abs(window.Width/window.Height-16.0/9)>0.005) problems.Add("window is not 16:9");
        FrameworkElement viewport=VisualTreeHelper.GetParent(media) as FrameworkElement;
        if(viewport==null || Math.Abs(viewport.ActualWidth-window.ActualWidth)>1 || Math.Abs(viewport.ActualHeight-window.ActualHeight)>1)
            problems.Add("video viewport does not fill the window; footer or progress area remains");
        // Before MediaOpened (and after Close), WPF reports a 0x0 media surface.
        if(media.NaturalVideoWidth>0 && (Math.Abs(media.ActualWidth-window.ActualWidth)>1 || Math.Abs(media.ActualHeight-window.ActualHeight)>1))
            problems.Add("opened video does not fill its 16:9 viewport");
    }
    private static void RunWindowButtons(VideoOptions options)
    {
        checks++;
        List<string> problems=new List<string>();
        VideoWindow window=new VideoWindow(options) { ShowInTaskbar=false, ShowActivated=false, Opacity=0,
            WindowStartupLocation=WindowStartupLocation.Manual, Left=SystemParameters.WorkArea.Left+40, Top=SystemParameters.WorkArea.Top+40 };
        MediaElement media=Find<MediaElement>((DependencyObject)window.Content,null);
        bool opened=false,closed=false,finished=false;
        media.MediaOpened+=delegate { opened=true; };
        Stopwatch clock=Stopwatch.StartNew();
        long changedAt=0;
        int step=0;
        double minimizedPosition=0;
        Rect normalBounds=new Rect(window.Left,window.Top,window.Width,window.Height);
        Button minimize=null,maximize=null,close=null;
        DispatcherFrame frame=new DispatcherFrame();
        DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
        window.Closed+=delegate { closed=true; timer.Stop(); frame.Continue=false; };
        timer.Tick+=delegate {
            try {
                if(step==0 && opened && clock.ElapsedMilliseconds>=450) {
                    CheckLayoutAndSound(window,media,problems);
                    minimize=FindControl((DependencyObject)window.Content,"MinimizeWindow");
                    maximize=FindControl((DependencyObject)window.Content,"MaximizeRestoreWindow");
                    close=FindControl((DependencyObject)window.Content,"CloseWindow");
                    if(minimize==null || maximize==null || close==null) { problems.Add("window controls are missing"); window.Close(); return; }
                    double previousX=-1;
                    foreach(Button button in new Button[]{minimize,maximize,close}) {
                        if(String.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button)) || button.ToolTip==null) problems.Add("control lacks accessible name or tooltip");
                        Point point=button.TranslatePoint(new Point(0,0),window);
                        if(point.Y<0 || point.Y>24 || point.X<window.ActualWidth-160 || point.X<=previousX || point.X+button.ActualWidth>window.ActualWidth) problems.Add("controls are not ordered at the upper right");
                        previousX=point.X;
                    }
                    if(!DispatchKey(maximize,Key.Enter) || window.WindowState!=WindowState.Normal) problems.Add("Enter on a window button was not inert");
                    Click(maximize); step=1; changedAt=clock.ElapsedMilliseconds;
                }
                else if(step==1 && clock.ElapsedMilliseconds-changedAt>=200) {
                    if(window.WindowState!=WindowState.Maximized) problems.Add("maximize button did not maximize");
                    if(System.Windows.Automation.AutomationProperties.GetName(maximize)!="还原") problems.Add("maximize did not become restore");
                    CheckWorkArea(window,problems);
                    if(media.Stretch!=Stretch.Uniform) problems.Add("maximized video does not preserve aspect ratio");
                    Click(maximize); step=2; changedAt=clock.ElapsedMilliseconds;
                }
                else if(step==2 && clock.ElapsedMilliseconds-changedAt>=200) {
                    if(window.WindowState!=WindowState.Normal || Math.Abs(window.Width-normalBounds.Width)>1 || Math.Abs(window.Height-normalBounds.Height)>1 || Math.Abs(window.Left-normalBounds.Left)>1 || Math.Abs(window.Top-normalBounds.Top)>1) problems.Add("restore did not recover normal bounds");
                    if(System.Windows.Automation.AutomationProperties.GetName(maximize)!="最大化") problems.Add("restore did not become maximize");
                    minimizedPosition=media.Position.TotalSeconds;
                    Click(minimize); step=3; changedAt=clock.ElapsedMilliseconds;
                }
                else if(step==3 && clock.ElapsedMilliseconds-changedAt>=900) {
                    if(window.WindowState!=WindowState.Minimized) problems.Add("minimize button did not minimize");
                    if(media.Position.TotalSeconds<=minimizedPosition+0.15) problems.Add("playback stopped while minimized");
                    if(media.IsMuted || Math.Abs(media.Volume-1)>0.001) problems.Add("minimize changed audio settings");
                    window.WindowState=WindowState.Normal; step=4; changedAt=clock.ElapsedMilliseconds;
                }
                else if(step==4 && clock.ElapsedMilliseconds-changedAt>=200) {
                    CheckLayoutAndSound(window,media,problems);
                    finished=true; Click(close);
                }
                if(clock.ElapsedMilliseconds>10000) { problems.Add("control sequence timed out"); window.Close(); }
            }
            catch(Exception error) { problems.Add(error.Message); window.Close(); }
        };
        window.Show(); timer.Start(); Dispatcher.PushFrame(frame);
        if(!closed || !finished) problems.Add("close button sequence did not finish");
        Console.WriteLine((problems.Count==0 ? "PASS " : "FAIL ")+options.Variant+": minimize/playback, maximize/work area, restore, close"+(problems.Count==0 ? "" : ": "+String.Join("; ",problems.ToArray())));
        if(problems.Count>0) failures++;
    }
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    private static Button FindControl(DependencyObject node,string id)
    {
        Button button=node as Button;
        if(button!=null && System.Windows.Automation.AutomationProperties.GetAutomationId(button)==id) return button;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) {
            Button child=FindControl(VisualTreeHelper.GetChild(node,i),id); if(child!=null) return child;
        }
        return null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor,Work; public int Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window,int flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    private static void CheckWorkArea(VideoWindow window,List<string> problems)
    {
        MonitorInfo info=new MonitorInfo { Size=Marshal.SizeOf(typeof(MonitorInfo)) };
        if(!GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(window).Handle,2),ref info)) { problems.Add("monitor work area unavailable"); return; }
        FrameworkElement content=(FrameworkElement)window.Content;
        Point topLeft=content.PointToScreen(new Point(0,0));
        Point bottomRight=content.PointToScreen(new Point(content.ActualWidth,content.ActualHeight));
        if(Math.Abs(topLeft.X-info.Work.Left)>2 || Math.Abs(topLeft.Y-info.Work.Top)>2 || Math.Abs(bottomRight.X-info.Work.Right)>2 || Math.Abs(bottomRight.Y-info.Work.Bottom)>2)
            problems.Add("maximized content does not match the monitor work area: "+topLeft+" to "+bottomRight+" vs "+info.Work.Left+","+info.Work.Top+" to "+info.Work.Right+","+info.Work.Bottom);
    }
    private static int CountButtons(DependencyObject node)
    {
        Button button=node as Button;
        int count=button!=null && button.IsVisible ? 1 : 0;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) count+=CountButtons(VisualTreeHelper.GetChild(node,i));
        return count;
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
    private static bool ErrorMentionsEscape(DependencyObject node)
    {
        TextBlock text=node as TextBlock;
        if(text!=null && text.Text.StartsWith("无法播放 HAAVK") && text.Text.Contains("Esc")) return true;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) if(ErrorMentionsEscape(VisualTreeHelper.GetChild(node,i))) return true;
        return false;
    }
}
