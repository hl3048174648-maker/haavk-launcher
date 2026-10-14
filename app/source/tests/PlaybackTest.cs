using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LaunchBloom;
public static class PlaybackTest
{
    [STAThread] public static int Main(string[] args)
    {
        string variant=args[0], package=args[1], snapshot=args[2];
        double expected=variant=="01" ? 33 : (variant=="02" ? 30.8 : 29.8);
        Application app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        VideoWindow window=new VideoWindow(VideoOptions.Parse(new string[]{"--preview",variant},package));
        window.ShowInTaskbar=false; window.ShowActivated=false;
        window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=-10000; window.Top=-10000;
        MediaElement media=FindMedia((DependencyObject)window.Content);
        if(media==null) throw new Exception("No player in video window");
        bool opened=false,ended=false,failed=false,timedOut=false,captured=false;
        double maximum=0, natural=0;
        Stopwatch wall=Stopwatch.StartNew();
        media.MediaOpened += delegate {
            opened=true; natural=media.NaturalDuration.TimeSpan.TotalSeconds;
            window.Left=-10000; window.Top=-10000;
            Console.WriteLine("OPENED " + variant + ": "+media.NaturalVideoWidth+"x"+media.NaturalVideoHeight+" "+natural+"s audio="+media.HasAudio);
        };
        media.MediaEnded += delegate { ended=true; Console.WriteLine("ENDED " + variant); };
        media.MediaFailed += delegate(object s,ExceptionRoutedEventArgs e) { failed=true; Console.WriteLine("MEDIA FAILED "+e.ErrorException.Message); };
        DispatcherFrame frame=new DispatcherFrame();
        DispatcherTimer tick=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
        tick.Tick += delegate {
            maximum=Math.Max(maximum,media.Position.TotalSeconds);
            if(!captured && maximum>=8) {
                captured=true;
                FrameworkElement content=(FrameworkElement)window.Content;
                RenderTargetBitmap bmp=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);
                bmp.Render(content); PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
                using(FileStream f=File.Create(snapshot)) encoder.Save(f);
            }
            if(wall.Elapsed.TotalSeconds>expected+20 || failed) { timedOut=!failed; window.Close(); }
        };
        window.Closed += delegate { tick.Stop(); frame.Continue=false; };
        window.Show(); tick.Start(); Dispatcher.PushFrame(frame); wall.Stop();
        // This host's WPF decoder rounds NaturalDuration to whole seconds; ffprobe separately verifies exact duration.
        // Ended plus the observed final position and wall time ensure we did not truncate the actual playback.
        bool ok=opened && ended && !failed && !timedOut && Math.Abs(natural-expected)<1.05 && maximum>expected-0.8 && wall.Elapsed.TotalSeconds>=expected-0.5;
        Console.WriteLine((ok ? "PASS" : "FAIL")+" full playback "+variant+": max="+maximum.ToString("F2")+"s wall="+wall.Elapsed.TotalSeconds.ToString("F2")+"s ended="+ended+" captured="+captured);
        app.Shutdown(); return ok ? 0 : 1;
    }
    private static MediaElement FindMedia(DependencyObject node)
    {
        MediaElement m=node as MediaElement; if(m!=null) return m;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) { MediaElement found=FindMedia(VisualTreeHelper.GetChild(node,i)); if(found!=null) return found; }
        return null;
    }
}
