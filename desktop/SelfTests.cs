using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PowerWallet {
    internal sealed class FakeAdb : IAdbRunner {
        public readonly List<string> Calls=new List<string>();
        public string Rom=WalletController.SupportedRom,Product="ishtar",State="device";
        public bool Wallet=true,InstallFailure,Deployed,Active;
        public CommandResult Run(string[] args,int timeout) {
            string text=String.Join(" ",args);Calls.Add(text);
            if(text=="devices -l")return new CommandResult(0,"List of devices attached\nTEST001 "+State+" product:ishtar model:Xiaomi_13_Ultra device:ishtar\n");
            if(args.Contains("install"))return new CommandResult(InstallFailure?1:0,InstallFailure?"Failure [INSTALL_FAILED_USER_RESTRICTED]":"Success");
            if(args.Contains("push")){Deployed=true;return new CommandResult(0,"1 file pushed");}
            string command=args.Last();
            if(command=="getprop ro.product.device")return new CommandResult(0,Product);
            if(command=="getprop ro.build.version.incremental")return new CommandResult(0,Rom);
            if(command=="getprop ro.build.version.release")return new CommandResult(0,"16");
            if(command.StartsWith("pm path"))return new CommandResult(Wallet?0:1,Wallet?"package:/data/app/wallet/base.apk":"");
            if(command.Contains("sha256sum"))return new CommandResult(Deployed?0:1,Deployed?"testhash  /data/local/tmp/powerwallet-helper.jar":"");
            if(command.Contains("--status"))return new CommandResult(Active?0:1,Active?"已激活 · 数据线可拔出":"");
            if(command.Contains("nohup"))Active=true;
            if(command.Contains("--restore")){Active=false;return new CommandResult(0,"已停用，并恢复原快捷键");}
            return new CommandResult(0,"");
        }
    }
    public static class SelfTests {
        private static readonly List<string> results=new List<string>();
        private static void Check(bool condition,string name) {if(!condition)throw new Exception(name);results.Add("PASS "+name);}
        private static bool ChangesPhone(string line) {return line.Contains("install ") || line.Contains("push ") || line.Contains("--stop") || line.Contains("nohup") || line.Contains("settings ");}
        private static void Reject(Action action,string name) {bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        private static WalletController Controller(FakeAdb adb) {return new WalletController(adb,"test.apk","testhash",null);}
        private static Device Device() {return new Device{Serial="TEST001",State="device",Model="Test phone"};}
        public static void Run(string directory) {
            Directory.CreateDirectory(directory);
            var parsed=WalletController.ParseDevices("* daemon started successfully\nList of devices attached\nA device model:Pixel_8 transport_id:1\nB unauthorized\nC offline\n");
            Check(parsed.Count==3 && parsed[0].Model=="Pixel 8" && parsed[1].State=="unauthorized" && parsed[2].State=="offline","device parser handles authorization and daemon noise");
            var fake=new FakeAdb{Rom="OS3.0.999.0"};Reject(()=>Controller(fake).Activate(Device()),"unsupported firmware is rejected");Check(!fake.Calls.Any(ChangesPhone),"unsupported firmware performs no mutation");
            fake=new FakeAdb{Product="other"};Reject(()=>Controller(fake).Activate(Device()),"unsupported model is rejected");Check(!fake.Calls.Any(ChangesPhone),"unsupported model performs no mutation");
            fake=new FakeAdb{Wallet=false};Reject(()=>Controller(fake).Activate(Device()),"missing Wallet is rejected");Check(!fake.Calls.Any(ChangesPhone),"missing Wallet performs no mutation");
            fake=new FakeAdb{State="unauthorized"};Reject(()=>Controller(fake).Activate(Device()),"connection is rechecked before activation");Check(!fake.Calls.Any(ChangesPhone),"unauthorized phone performs no mutation");
            fake=new FakeAdb{InstallFailure=true};bool failed=false;try{Controller(fake).Activate(Device());}catch(IOException e){failed=e.Message.Contains("USB 安装");}Check(failed,"USB install rejection produces useful guidance");Check(!fake.Calls.Any(x=>x.Contains("nohup") || x.Contains("push ")),"failed install never starts helper");
            fake=new FakeAdb{Rom="new-firmware"};Controller(fake).Restore(Device());Check(fake.Calls.Any(x=>x.Contains("--restore")),"restore remains available after a firmware update");
            Check(!fake.Calls.Any(x=>x.Contains("put system") || x.Contains("mi_pay")),"desktop restore delegates to saved original setting");
            fake=new FakeAdb();var active=Controller(fake).Activate(Device());Check(active.Active,"successful activation requires a live helper response");
            int stopped=fake.Calls.FindIndex(x=>x.Contains("--stop")),installed=fake.Calls.FindIndex(x=>x.Contains(" install ")),pushed=fake.Calls.FindIndex(x=>x.Contains(" push ")),started=fake.Calls.FindIndex(x=>x.Contains("nohup"));
            Check(stopped>=0 && installed>stopped && pushed>installed && started>pushed,"activation stops, installs, deploys, then starts in order");
            Check(fake.Calls.FindLastIndex(x=>x.Contains("--status"))>started,"live status is checked after starting the helper");
            Check(WalletController.Redact("TEST001 "+Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Device()).IndexOf("TEST001",StringComparison.Ordinal)<0,"diagnostic output hides device serial");
            foreach(string asset in Assets.Names)Check(Assets.Read(asset).Length>0,"embedded asset "+asset);
            double[][] screens={new double[]{800,600,1},new double[]{800,600,1.25},new double[]{1024,768,1.5},new double[]{1280,720,1},new double[]{1280,720,1.5},new double[]{1280,720,2},new double[]{1366,768,1},new double[]{1366,768,1.25},new double[]{1366,768,2},new double[]{1920,1080,1},new double[]{1920,1080,1.25},new double[]{1920,1080,1.5},new double[]{1920,1080,2},new double[]{2560,1440,1.5},new double[]{2560,1440,2},new double[]{3200,2000,2},new double[]{3200,2000,2.5},new double[]{3840,2160,2},new double[]{3840,2160,3}};
            foreach(string theme in new[]{"dark","light"}) {
                foreach(double[] screen in screens) {
                    var fit=ScreenPolicy.Fit(screen[0]/screen[2],screen[1]/screen[2]-40);
                    Check(fit.Width<=screen[0]/screen[2] && fit.Height<=screen[1]/screen[2]-40,"window fits "+String.Join("x",screen)+" "+theme);
                    var window=new MainWindow(true);window.SetTheme(theme,false);window.SetDemo(true);
                    double width=fit.Width-16,height=fit.Height-40;
                    window.ApplyViewport(width,height);window.View.Measure(new Size(width,height));window.View.Arrange(new Rect(0,0,width,height));window.View.UpdateLayout();
                    foreach(Button button in new[]{window.ActivateButton,window.RestoreButton,window.OpenButton}) {
                        Point location=button.TranslatePoint(new Point(0,0),window.View);
                        Check(button.ActualWidth>0 && location.X>=-1 && location.X+button.ActualWidth<=width+1,"action fits horizontally "+button.Content+" "+String.Join("x",screen)+" "+theme);
                        Check(location.Y+button.ActualHeight<=Math.Max(height,400)+1,"action is visible or vertically reachable "+button.Content+" "+String.Join("x",screen)+" "+theme);
                        if(width>=440)Check(location.Y+button.ActualHeight<=height+1,"action stays visible on compact screen "+button.Content+" "+String.Join("x",screen)+" "+theme);
                    }
                    Check(window.Page.ScrollableWidth<1,"no horizontal scrolling "+String.Join("x",screen)+" "+theme);
                    if((screen[0]==1920 && screen[2]==1) || (theme=="dark" && screen[0]==1280 && screen[2]==2)) {
                        string name=screen[0]==1920?"PowerWallet-"+theme+".png":"PowerWallet-compact-200pct.png";
                        Render(window.View,width,height,screen[2],Path.Combine(directory,name));
                    }
                    window.Close();
                }
            }
            File.WriteAllLines(Path.Combine(directory,"test-results.txt"),results.Concat(new[]{results.Count+" checks passed; 38 theme / display combinations; no phone required."}),Encoding.UTF8);
        }
        private static void Render(FrameworkElement visual,double width,double height,double scale,string path) {
            var image=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))encoder.Save(file);
        }
    }
}
