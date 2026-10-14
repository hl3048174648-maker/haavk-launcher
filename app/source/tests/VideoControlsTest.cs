using System;
using System.Collections.Generic;
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
        Run("01: sound, full video, Enter ignored, Esc skips",VideoOptions.Parse(new string[]{"--preview","01"},package),"enter");
        Run("02: sound, full video, Enter ignored, Esc skips",VideoOptions.Parse(new string[]{"--preview","02"},package),"enter");
        Run("03: sound, full video, Enter ignored, Esc skips",VideoOptions.Parse(new string[]{"--preview","03"},package),"enter");
        Run("Esc skips a preview directly",VideoOptions.Parse(new string[]{"--preview","01"},package),"esc");
        Run("OS window close cancels a preview",VideoOptions.Parse(new string[]{"--preview","02"},package),"close");
        Run("Missing media keeps Enter inert and permits Esc",VideoOptions.Parse(new string[]{"--preview","03"},fixture),"error");
        Directory.CreateDirectory(Path.Combine(fixture,"videos"));
        File.WriteAllText(Path.Combine(fixture,"videos","HAAVK_01.mp4"),"invalid mp4 content");
        Run("Corrupt media keeps Enter inert and permits Esc",VideoOptions.Parse(new string[]{"--preview","01"},fixture),"error");
        app.Shutdown();
        Console.WriteLine(failures==0 ? "PASS: 7 video-control checks" : "FAIL: "+failures+" video-control checks");
        return failures==0 ? 0 : 1;
    }
    private static void Run(string name,VideoOptions options,string action)
    {
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
        if(media.IsMuted || Math.Abs(media.Volume-1.0)>0.001) problems.Add("audio is not unmuted at full volume");
        if(CountButtons((DependencyObject)window.Content)!=0) problems.Add("visible buttons remain");
        if(Math.Abs(window.Width/window.Height-16.0/9)>0.005) problems.Add("window is not 16:9");
        FrameworkElement viewport=VisualTreeHelper.GetParent(media) as FrameworkElement;
        if(viewport==null || Math.Abs(viewport.ActualWidth-window.ActualWidth)>1 || Math.Abs(viewport.ActualHeight-window.ActualHeight)>1)
            problems.Add("video viewport does not fill the window; footer or progress area remains");
        // Before MediaOpened (and after Close), WPF reports a 0x0 media surface.
        if(media.NaturalVideoWidth>0 && (Math.Abs(media.ActualWidth-window.ActualWidth)>1 || Math.Abs(media.ActualHeight-window.ActualHeight)>1))
            problems.Add("opened video does not fill its 16:9 viewport");
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
