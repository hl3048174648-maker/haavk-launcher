using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LaunchBloom;

// Records the running WPF preview visual, including its actual decoded video and
// controls. It never captures the desktop or launches either target application.
// The recording is silent because the launcher starts muted by design.
public static class RecordDemo
{
    [STAThread] public static int Main(string[] args)
    {
        if(args.Length!=4) { Console.Error.WriteLine("RecordDemo VARIANT APP_DIRECTORY OUTPUT.mp4 FFMPEG.exe"); return 2; }
        string variant=args[0], output=Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        Application app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        VideoWindow window=new VideoWindow(VideoOptions.Parse(new string[]{"--preview",variant},args[1]));
        window.ShowInTaskbar=false; window.ShowActivated=false;
        window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=-10000; window.Top=-10000;
        MediaElement media=FindMedia((DependencyObject)window.Content);
        bool ended=false,failed=false; int frames=0, captures=0; double maximum=0;
        Stopwatch clock=new Stopwatch();
        Process encoder=null; byte[] previous=null; int width=0,height=0;
        string encoderErrors="";
        DispatcherFrame loop=new DispatcherFrame();
        DispatcherTimer tick=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(15) };
        media.MediaOpened += delegate { window.Left=-10000; window.Top=-10000; Console.WriteLine("Opened HAAVK "+variant); };
        media.MediaEnded += delegate { ended=true; };
        media.MediaFailed += delegate(object sender,ExceptionRoutedEventArgs e) { failed=true; Console.Error.WriteLine(e.ErrorException.Message); window.Close(); };
        window.ContentRendered += delegate {
            FrameworkElement visual=(FrameworkElement)window.Content;
            width=((int)visual.ActualWidth+1)/2*2; height=((int)visual.ActualHeight+1)/2*2;
            ProcessStartInfo start=new ProcessStartInfo(args[3],"-hide_banner -loglevel error -y -f rawvideo -pixel_format bgra -video_size "+width+"x"+height+" -framerate 30 -i pipe:0 -an -c:v libx264 -threads 2 -preset fast -crf 20 -pix_fmt yuv420p -movflags +faststart \""+output+"\"");
            start.UseShellExecute=false; start.CreateNoWindow=true; start.RedirectStandardInput=true; start.RedirectStandardError=true;
            encoder=new Process { StartInfo=start };
            encoder.ErrorDataReceived += delegate(object sender,DataReceivedEventArgs e) { if(e.Data!=null) encoderErrors+=e.Data+Environment.NewLine; };
            encoder.Start(); encoder.BeginErrorReadLine(); clock.Start(); tick.Start();
        };
        tick.Tick += delegate {
            try {
                if(clock.Elapsed.TotalSeconds>55) { failed=true; window.Close(); return; }
                maximum=Math.Max(maximum,media.Position.TotalSeconds);
                int due=(int)Math.Floor(clock.Elapsed.TotalSeconds*30)+1;
                if(due<=frames) return;
                // Repeat the prior observed frame for any missed wall-clock intervals.
                while(previous!=null && frames<due-1) { encoder.StandardInput.BaseStream.Write(previous,0,previous.Length); frames++; }
                RenderTargetBitmap bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
                bmp.Render((Visual)window.Content);
                byte[] pixels=new byte[width*height*4]; bmp.CopyPixels(pixels,width*4,0);
                encoder.StandardInput.BaseStream.Write(pixels,0,pixels.Length); frames++; captures++; previous=pixels;
                if(maximum>=8 && !File.Exists(Path.ChangeExtension(output,".png"))) {
                    PngBitmapEncoder png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
                    using(FileStream file=File.Create(Path.ChangeExtension(output,".png"))) png.Save(file);
                }
            } catch(Exception ex) { failed=true; Console.Error.WriteLine(ex.Message); window.Close(); }
        };
        window.Closed += delegate { tick.Stop(); clock.Stop(); loop.Continue=false; };
        window.Show(); Dispatcher.PushFrame(loop);
        int code=-1;
        if(encoder!=null) {
            encoder.StandardInput.Close();
            if(encoder.WaitForExit(30000)) { encoder.WaitForExit(); code=encoder.ExitCode; }
            else { encoder.Kill(); failed=true; }
            encoder.Dispose();
        }
        double expected=variant=="01" ? 33 : (variant=="02" ? 30.8 : 29.8);
        bool ok=ended && !failed && code==0 && maximum>=expected-0.8 && frames/30.0>=expected-0.5 && captures>frames*0.65;
        string report=(ok?"PASS":"FAIL")+" HAAVK "+variant+" preview visual recording; ended="+ended+"; frames="+frames+"; captures="+captures+"; maxMediaSeconds="+maximum.ToString("F3")+"; wallSeconds="+clock.Elapsed.TotalSeconds.ToString("F3")+"; dimensions="+width+"x"+height+"; fps=30; audio=muted\n"+encoderErrors;
        File.WriteAllText(Path.ChangeExtension(output,".txt"),report);
        Console.WriteLine(report); app.Shutdown(); return ok?0:1;
    }
    private static MediaElement FindMedia(DependencyObject node)
    {
        MediaElement media=node as MediaElement; if(media!=null) return media;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) { MediaElement found=FindMedia(VisualTreeHelper.GetChild(node,i)); if(found!=null) return found; }
        return null;
    }
}
