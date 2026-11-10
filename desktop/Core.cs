using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerWallet {
    public sealed class CommandResult {
        public int ExitCode;
        public string Output;
        public CommandResult(int code, string output) { ExitCode=code; Output=output ?? ""; }
    }
    public interface IAdbRunner { CommandResult Run(string[] arguments, int timeoutMs); }
    public sealed class AdbRunner : IAdbRunner {
        private readonly string executable;
        public AdbRunner(string path) { executable=path; }
        public static string Quote(string value) {
            if(value.IndexOf('\0')>=0) throw new ArgumentException("Invalid argument");
            return "\""+Regex.Replace(Regex.Replace(value,@"(\\*)""", "$1$1\\\""),@"(\\+)$","$1$1")+"\"";
        }
        public CommandResult Run(string[] arguments, int timeoutMs) {
            var info=new ProcessStartInfo(executable, String.Join(" ",arguments.Select(Quote)));
            info.UseShellExecute=false; info.CreateNoWindow=true;
            info.RedirectStandardOutput=true; info.RedirectStandardError=true;
            info.StandardOutputEncoding=Encoding.UTF8; info.StandardErrorEncoding=Encoding.UTF8;
            var output=new StringBuilder(); var gate=new object();
            using(var process=new Process()) {
                process.StartInfo=info;
                DataReceivedEventHandler receive=(s,e)=> { if(e.Data!=null) lock(gate) output.AppendLine(e.Data); };
                process.OutputDataReceived+=receive; process.ErrorDataReceived+=receive;
                if(!process.Start()) throw new IOException("无法启动内置 ADB。");
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if(!process.WaitForExit(timeoutMs)) {
                    try { process.Kill(); process.WaitForExit(2000); } catch(InvalidOperationException) {}
                    throw new TimeoutException("手机响应超时，请检查连接后重试。");
                }
                process.WaitForExit();
                lock(gate) return new CommandResult(process.ExitCode,output.ToString().Trim());
            }
        }
    }
    public static class Assets {
        public const string ApkName="PowerWallet-1.0.4.apk";
        public const string Version="1.0.2";
        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PowerWallet","assets","desktop-"+Version); } }
        public static readonly string[] Names={"adb.exe","AdbWinApi.dll","AdbWinUsbApi.dll",ApkName,"NOTICE.txt","source.properties"};
        public static string Hash(byte[] data) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant(); }
        public static byte[] Read(string name) {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PowerWallet.Assets."+name)) {
                if(stream==null) throw new IOException("内置文件缺失："+name);
                using(var memory=new MemoryStream()) {stream.CopyTo(memory);return memory.ToArray();}
            }
        }
        public static void Extract() {
            Directory.CreateDirectory(DirectoryPath);
            foreach(string name in Names) {
                byte[] original=Read(name); string target=Path.Combine(DirectoryPath,name);
                if(File.Exists(target) && Hash(File.ReadAllBytes(target))==Hash(original)) continue;
                string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllBytes(temporary,original);
                if(File.Exists(target)) File.Delete(target);
                File.Move(temporary,target);
            }
        }
    }
    public sealed class Device {
        public string Serial, State, Model;
        public override string ToString() {return Model+"  ·  "+(State=="device"?"已连接":State=="unauthorized"?"等待授权":"离线");}
    }
    public sealed class PhoneInfo {
        public Device Device;
        public string Product="", Rom="", Android="", Status="", Reason="";
        public bool Supported, WalletInstalled, Active, CurrentHelper, HasBackup;
        public bool CanActivate {get{return Device!=null && Device.State=="device" && Supported && WalletInstalled;}}
        public string DisplayModel {get{return Product=="ishtar"?"小米 13 Ultra":Device==null?"未连接手机":Device.Model;}}
    }
    public sealed class WalletController {
        public const string SupportedRom="OS3.0.308.0.WMACNXM";
        public const string HelperJar="/data/local/tmp/powerwallet-helper.jar";
        private readonly IAdbRunner adb;
        private readonly string apkPath, apkHash;
        private readonly Action<string> log;
        public WalletController(IAdbRunner runner, string apk, string hash, Action<string> logger) {adb=runner;apkPath=apk;apkHash=hash;log=logger ?? (s=>{});}
        private CommandResult DeviceCommand(Device d, int timeout, params string[] arguments) {
            var all=new List<string>{"-s",d.Serial};all.AddRange(arguments);return adb.Run(all.ToArray(),timeout);
        }
        private CommandResult Shell(Device d,string command) {return DeviceCommand(d,15000,"shell",command);}
        private string Read(Device d,string command) {var r=Shell(d,command);if(r.ExitCode!=0)throw new IOException("手机命令未完成："+Redact(r.Output,d));return r.Output.Trim();}
        public static string Redact(string message, Device d) {
            string value=message ?? "";
            if(d!=null && !String.IsNullOrEmpty(d.Serial))value=value.Replace(d.Serial,"[设备]");
            string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if(!String.IsNullOrEmpty(profile)) value=value.Replace(profile,"%USERPROFILE%");
            return value.Length>1600?value.Substring(0,1600):value;
        }
        public static List<Device> ParseDevices(string text) {
            var devices=new List<Device>();
            foreach(string line in text.Split('\n')) {
                var match=Regex.Match(line.Trim(),@"^(\S+)\s+(device|offline|unauthorized)\b(.*)$");
                if(!match.Success)continue;
                var model=Regex.Match(match.Groups[3].Value,@"\bmodel:(\S+)");
                devices.Add(new Device{Serial=match.Groups[1].Value,State=match.Groups[2].Value,Model=model.Success?model.Groups[1].Value.Replace('_',' '):"Android 手机"});
            }
            return devices;
        }
        public List<Device> Scan() {
            var result=adb.Run(new[]{"devices","-l"},12000);
            if(result.ExitCode!=0)throw new IOException("无法读取手机列表："+Redact(result.Output,null));
            return ParseDevices(result.Output);
        }
        public PhoneInfo Inspect(Device d) {
            var phone=new PhoneInfo{Device=d};
            if(d==null) {phone.Reason="用数据线连接手机，并打开 USB 调试。";return phone;}
            if(d.State!="device") {phone.Reason=d.State=="unauthorized"?"请解锁手机，允许这台电脑进行 USB 调试。":"设备处于离线状态，请重新连接数据线。";return phone;}
            phone.Product=Read(d,"getprop ro.product.device");
            phone.Rom=Read(d,"getprop ro.build.version.incremental");
            phone.Android=Read(d,"getprop ro.build.version.release");
            phone.Supported=phone.Product=="ishtar" && phone.Rom==SupportedRom;
            var wallet=Shell(d,"pm path com.google.android.apps.walletnfcrel");
            phone.WalletInstalled=wallet.ExitCode==0 && wallet.Output.StartsWith("package:",StringComparison.Ordinal);
            phone.HasBackup=Read(d,"if [ -f /data/local/tmp/powerwallet-state.properties ]; then echo yes; fi")=="yes";
            var hash=Shell(d,"sha256sum "+HelperJar+" 2>/dev/null");
            phone.CurrentHelper=hash.ExitCode==0 && hash.Output.StartsWith(apkHash+" ",StringComparison.OrdinalIgnoreCase);
            if(phone.CurrentHelper) {
                var state=Shell(d,"CLASSPATH="+HelperJar+" app_process /system/bin cn.haiyu.powerwallet.Helper --status 2>/dev/null");
                phone.Status=state.Output;phone.Active=state.ExitCode==0 && state.Output.StartsWith("已激活",StringComparison.Ordinal);
            }
            phone.Reason=!phone.Supported?"当前只适配小米 13 Ultra 的 "+SupportedRom+"，此设备不会被激活。":!phone.WalletInstalled?"请先在手机安装并设置 Google 钱包。":phone.Active?"快捷键助手正在运行，可以拔掉数据线。":"此系统已适配，点击下方按钮安装并激活。";
            return phone;
        }
        private void RequireConnected(Device d) {
            if(d==null || d.State!="device") throw new InvalidOperationException("请先连接手机并完成 USB 调试授权。");
            var live=Scan().FirstOrDefault(x=>x.Serial==d.Serial);
            if(live==null || live.State!="device") throw new InvalidOperationException("手机连接已改变，请刷新后重试。");
        }
        public PhoneInfo Activate(Device d) {
            RequireConnected(d);
            PhoneInfo preflight=Inspect(d);
            if(!preflight.CanActivate) throw new InvalidOperationException(preflight.Reason);
            log("已确认机型、系统版本和 Google 钱包。");
            log("正在停止旧助手，保存的原快捷键不会丢失…");
            Shell(d,"if [ -f "+HelperJar+" ]; then CLASSPATH="+HelperJar+" app_process /system/bin cn.haiyu.powerwallet.Helper --stop >/dev/null 2>&1; fi");
            log("正在安装手机插件 1.0.4…");
            var install=DeviceCommand(d,120000,"install","-r",apkPath);
            if(install.ExitCode!=0 || !Regex.IsMatch(install.Output,@"(?m)^Success\s*$")) {
                string hint=install.Output.Contains("INSTALL_FAILED_USER_RESTRICTED")?"请在手机允许 USB 安装；如果系统提示确认安装，请在手机完成。":install.Output.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE")?"手机上存在其他签名的同名插件，请先恢复原快捷键并卸载该插件。":"请查看手机上的安装提示后重试。";
                throw new IOException("插件未安装成功。"+hint+"\n"+Redact(install.Output,d));
            }
            log("正在部署并启动按键助手…");
            var push=DeviceCommand(d,30000,"push",apkPath,HelperJar);
            if(push.ExitCode!=0) throw new IOException("无法传输助手："+Redact(push.Output,d));
            Read(d,"chmod 600 "+HelperJar);
            Read(d,"CLASSPATH="+HelperJar+" nohup app_process /system/bin cn.haiyu.powerwallet.Helper </dev/null >/data/local/tmp/powerwallet.log 2>&1 &");
            PhoneInfo result=null;
            for(int attempt=0;attempt<5;attempt++) {
                System.Threading.Thread.Sleep(700);
                result=Inspect(d);
                if(result.Active)break;
            }
            if(result==null || !result.Active) {
                var detail=Shell(d,"tail -n 12 /data/local/tmp/powerwallet.log");
                throw new IOException("助手尚未成功激活。\n"+Redact(detail.Output,d));
            }
            log("已验证后台助手正在运行，原快捷键备份已保存。");
            var show=Shell(d,"am start -n cn.haiyu.powerwallet/.MainActivity");
            if(show.ExitCode!=0) log("激活已完成；手机插件页面未自动打开，可从桌面打开。");
            return result;
        }
        public PhoneInfo Restore(Device d) {
            RequireConnected(d); log("正在停用助手并恢复激活前的快捷键…");
            // Use the bundled control code even when the installed helper stopped at reboot.
            var push=DeviceCommand(d,30000,"push",apkPath,"/data/local/tmp/powerwallet-control.jar");
            if(push.ExitCode!=0)throw new IOException("无法传输恢复工具："+Redact(push.Output,d));
            var result=Shell(d,"CLASSPATH=/data/local/tmp/powerwallet-control.jar app_process /system/bin cn.haiyu.powerwallet.Helper --restore");
            if(result.ExitCode!=0 || !result.Output.StartsWith("已停用",StringComparison.Ordinal))throw new IOException("未能恢复原快捷键："+Redact(result.Output,d));
            log(result.Output);
            var restored=Inspect(d);restored.Status=result.Output;return restored;
        }
        public void OpenPlugin(Device d) {RequireConnected(d);Read(d,"am start -n cn.haiyu.powerwallet/.MainActivity");}
    }
}
