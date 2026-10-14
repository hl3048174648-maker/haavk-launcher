using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LaunchBloom;

// Records the running WPF preview visual, including its actual decoded video and
// image. It never captures the desktop or launches either target application.
// The final file uses the original audio track, aligned to the observed player
// clock. This is intentionally not a recording of other desktop audio.
public static class RecordDemo
{
    [STAThread] public static int Main(string[] args)
    {
        if(args.Length!=4) { Console.Error.WriteLine("RecordDemo VARIANT APP_DIRECTORY OUTPUT.mp4 FFMPEG.exe"); return 2; }
        string variant=args[0], output=Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        Application app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        VideoOptions options=VideoOptions.Parse(new string[]{"--preview",variant},args[1]);
        VideoWindow window=new VideoWindow(options);
        window.ShowInTaskbar=false; window.ShowActivated=false;
        window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=-10000; window.Top=-10000;
        MediaElement media=FindMedia((DependencyObject)window.Content);
        bool ended=false,failed=false,opened=false,snapshotSaved=false,audioEnabled=true;
        int frames=0, captures=0; double maximum=0;
        List<double> audioOffsets=new List<double>();
        string visualOutput=output+".visual.tmp.mp4";
        Stopwatch clock=new Stopwatch();
        Process encoder=null; byte[] previous=null; int width=0,height=0;
        string encoderErrors="";
        DispatcherFrame loop=new DispatcherFrame();
        DispatcherTimer tick=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(15) };
        media.MediaOpened += delegate { opened=true; window.Left=-10000; window.Top=-10000; Console.WriteLine("Opened HAAVK "+variant+"; muted="+media.IsMuted+"; volume="+media.Volume); };
        media.MediaEnded += delegate { ended=true; };
        media.MediaFailed += delegate(object sender,ExceptionRoutedEventArgs e) { failed=true; Console.Error.WriteLine(e.ErrorException.Message); window.Close(); };
        window.ContentRendered += delegate {
            FrameworkElement visual=(FrameworkElement)window.Content;
            width=((int)visual.ActualWidth+1)/2*2; height=((int)visual.ActualHeight+1)/2*2;
            ProcessStartInfo start=new ProcessStartInfo(args[3],"-hide_banner -loglevel error -y -f rawvideo -pixel_format bgra -video_size "+width+"x"+height+" -framerate 30 -i pipe:0 -an -c:v libx264 -threads 2 -preset fast -crf 20 -pix_fmt yuv420p -movflags +faststart \""+visualOutput+"\"");
            start.UseShellExecute=false; start.CreateNoWindow=true; start.RedirectStandardInput=true; start.RedirectStandardError=true;
            encoder=new Process { StartInfo=start };
            encoder.ErrorDataReceived += delegate(object sender,DataReceivedEventArgs e) { if(e.Data!=null) encoderErrors+=e.Data+Environment.NewLine; };
            encoder.Start(); encoder.BeginErrorReadLine(); clock.Start(); tick.Start();
        };
        tick.Tick += delegate {
            try {
                if(clock.Elapsed.TotalSeconds>55) { failed=true; window.Close(); return; }
                maximum=Math.Max(maximum,media.Position.TotalSeconds);
                if(opened) {
                    audioEnabled=audioEnabled && !media.IsMuted && media.Volume>=0.999 && media.HasAudio;
                    double position=media.Position.TotalSeconds;
                    if(position>=0.15 && position<=3) audioOffsets.Add(clock.Elapsed.TotalSeconds-position);
                }
                int due=(int)Math.Floor(clock.Elapsed.TotalSeconds*30)+1;
                if(due<=frames) return;
                // Repeat the prior observed frame for any missed wall-clock intervals.
                while(previous!=null && frames<due-1) { encoder.StandardInput.BaseStream.Write(previous,0,previous.Length); frames++; }
                RenderTargetBitmap bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
                bmp.Render((Visual)window.Content);
                byte[] pixels=new byte[width*height*4]; bmp.CopyPixels(pixels,width*4,0);
                encoder.StandardInput.BaseStream.Write(pixels,0,pixels.Length); frames++; captures++; previous=pixels;
                if(maximum>=8 && !snapshotSaved) {
                    snapshotSaved=true;
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
        bool ok=ended && !failed && code==0 && maximum>=expected-0.8 && frames/30.0>=expected-0.5 && captures>frames*0.65 && audioEnabled && audioOffsets.Count>0;
        double audioOffset=0;
        if(ok) {
            audioOffsets.Sort(); audioOffset=audioOffsets[audioOffsets.Count/2];
            string audioFilter=audioOffset>=0
                ? "asetpts=PTS-STARTPTS,adelay="+((int)Math.Round(audioOffset*1000)).ToString(CultureInfo.InvariantCulture)+":all=1,apad"
                : "atrim=start="+(-audioOffset).ToString("F6",CultureInfo.InvariantCulture)+",asetpts=PTS-STARTPTS,apad";
            ProcessStartInfo muxStart=new ProcessStartInfo(args[3],"-hide_banner -loglevel error -y -i \""+visualOutput+"\" -i \""+options.VideoPath+"\" -filter_complex \"[1:a:0]"+audioFilter+"[audio]\" -map 0:v:0 -map \"[audio]\" -c:v copy -c:a aac -b:a 192k -t "+(frames/30.0).ToString("F6",CultureInfo.InvariantCulture)+" -movflags +faststart \""+output+"\"");
            muxStart.UseShellExecute=false; muxStart.CreateNoWindow=true; muxStart.RedirectStandardError=true;
            using(Process mux=Process.Start(muxStart)) {
                string muxError=mux.StandardError.ReadToEnd(); mux.WaitForExit();
                ok=mux.ExitCode==0; encoderErrors+=muxError;
            }
            if(ok) File.Delete(visualOutput);
        }
        string report=(ok?"PASS":"FAIL")+" HAAVK "+variant+" preview visual recording; ended="+ended+"; frames="+frames+"; captures="+captures+"; maxMediaSeconds="+maximum.ToString("F3")+"; wallSeconds="+clock.Elapsed.TotalSeconds.ToString("F3")+"; dimensions="+width+"x"+height+"; fps=30; playerAudioEnabled="+audioEnabled+"; audio=original-track-clock-synced; audioOffsetSeconds="+audioOffset.ToString("F3",CultureInfo.InvariantCulture)+"\n"+encoderErrors;
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
