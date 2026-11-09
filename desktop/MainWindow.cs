using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PowerWallet {
    public sealed class MainWindow : Window {
        public readonly Grid View=new Grid();
        public readonly ScrollViewer Page=new ScrollViewer();
        private readonly DockPanel dock=new DockPanel();
        private readonly List<UIElement> hero=new List<UIElement>();
        private readonly WrapPanel header=new WrapPanel();
        private readonly TextBlock footerNote=new TextBlock();
        private readonly ComboBox devices=new ComboBox();
        private readonly TextBlock heading=new TextBlock(),description=new TextBlock(),deviceInfo=new TextBlock(),support=new TextBlock();
        private readonly TextBlock statusPill=new TextBlock(),screenInfo=new TextBlock();
        private readonly TextBox logBox=new TextBox();
        private readonly ProgressBar progress=new ProgressBar();
        public readonly Button ActivateButton=new Button(),RestoreButton=new Button(),OpenButton=new Button();
        private readonly Button refreshButton=new Button();
        private readonly Dictionary<string,Button> themeButtons=new Dictionary<string,Button>();
        private WalletController controller;
        private PhoneInfo phone=new PhoneInfo();
        private readonly DispatcherTimer timer=new DispatcherTimer();
        private bool busy,changingSelection,testMode,failed;
        private string preference="system",lastDevice="";
        public const string Repository="https://github.com/DustinYe/PowerWallet";
        private static readonly string Preferences=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PowerWallet","theme.txt");

        public MainWindow(bool testing) {
            testMode=testing;Title="PowerWallet · 电源键钱包";Width=920;Height=760;
            MinWidth=480;MinHeight=420;WindowStartupLocation=WindowStartupLocation.CenterScreen;
            FontFamily=new FontFamily("Microsoft YaHei UI, Segoe UI");FontSize=14;
            UseLayoutRounding=true;SnapsToDevicePixels=true;
            SetResourceReference(BackgroundProperty,"Bg");SetResourceReference(ForegroundProperty,"Ink");
            InstallStyles();
            if(!testing)try{if(File.Exists(Preferences))preference=File.ReadAllText(Preferences).Trim();}catch(IOException){}
            BuildView();SetTheme(preference,false);
            SizeChanged+=(s,e)=>ApplyViewport(Math.Max(240,ActualWidth-16),Math.Max(220,ActualHeight-40));
            SourceInitialized+=(s,e)=> {
                Native.Fit(this,true);Native.DarkTitle(this,IsDark());
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WindowMessage);
            };
            if(!testing) {
                Loaded+=async(s,e)=>await Initialize();
                SystemEvents.UserPreferenceChanged+=OnPreferenceChanged;
                timer.Interval=TimeSpan.FromSeconds(5);timer.Tick+=async(s,e)=> {if(!busy)await Refresh(false);};
                Closed+=(s,e)=>{timer.Stop();SystemEvents.UserPreferenceChanged-=OnPreferenceChanged;};
                Closing+=(s,e)=> {if(busy){e.Cancel=true;AppendLog("操作正在进行，请等待完成后关闭窗口。");}};
            }
            ShowPhone(phone);ApplyViewport(900,720);
        }
        private IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
            if(message==0x02E0 || message==0x007E) Dispatcher.BeginInvoke(new Action(()=> {if(WindowState==WindowState.Normal)Native.Fit(this,false);UpdateScreenInfo();}));
            return IntPtr.Zero;
        }
        private void OnPreferenceChanged(object s,UserPreferenceChangedEventArgs e) {if(preference=="system")Dispatcher.BeginInvoke(new Action(()=>SetTheme("system",false)));}
        private bool IsDark() {
            if(preference=="dark")return true;if(preference=="light")return false;
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                object value=key==null?null:key.GetValue("AppsUseLightTheme");return value is int && (int)value==0;
            }
        }
        public void SetTheme(string mode,bool persist) {
            preference=mode=="light" || mode=="dark"?mode:"system";bool dark=IsDark();
            string[] keys={"Bg","Card","Control","Hover","Ink","Muted","Line","Accent","AccentInk","Soft","Warn"};
            string[] colors=dark?new[]{"#101816","#18231F","#24342D","#334A3E","#F0F6F1","#A2B7AA","#33453B","#B1E5C5","#123324","#243F30","#F0C281"}:new[]{"#F3F6F1","#FFFFFF","#EAF0E8","#DCE8DC","#19382B","#617669","#D6E0D5","#225B3D","#FFFFFF","#E0EDDF","#96601E"};
            for(int i=0;i<keys.Length;i++)Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
            foreach(var pair in themeButtons) {
                pair.Value.SetResourceReference(BackgroundProperty,pair.Key==preference?"Soft":"Control");
                pair.Value.FontWeight=pair.Key==preference?FontWeights.SemiBold:FontWeights.Normal;
            }
            Native.DarkTitle(this,dark);
            if(persist && !testMode)try{Directory.CreateDirectory(Path.GetDirectoryName(Preferences));File.WriteAllText(Preferences,preference);}catch(IOException){AppendLog("主题偏好未能保存；本次切换仍然生效。");}
        }
        private void InstallStyles() {
            string xaml=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Button'><Setter Property='Background' Value='{DynamicResource Control}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='14,10'/><Setter Property='MinHeight' Value='40'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Border' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Border' Property='Opacity' Value='0.82'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Border' Property='BorderBrush' Value='{DynamicResource Accent}'/><Setter TargetName='Border' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.42'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBoxItem'><Setter Property='Background' Value='{DynamicResource Control}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='12,10'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='B' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource Soft}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBox'><Setter Property='Background' Value='{DynamicResource Control}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='MinHeight' Value='46'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton x:Name='Toggle' Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{DynamicResource Control}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='9'><TextBlock Text='⌄' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,14,0' Foreground='{DynamicResource Muted}'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin='14,10,36,10' VerticalAlignment='Center' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}'/><Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'><Border MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' Background='{DynamicResource Control}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='8' Padding='4'><ScrollViewer MaxHeight='240'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ScrollBar'><Setter Property='Width' Value='10'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Border Background='{DynamicResource Bg}'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource Muted}' Opacity='0.4' CornerRadius='4' Margin='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Border></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
            var resources=(ResourceDictionary)XamlReader.Parse(xaml);Resources.MergedDictionaries.Add(resources);
        }
        private TextBlock Text(string value,double size,string color) {var t=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap};t.SetResourceReference(TextBlock.ForegroundProperty,color);return t;}
        private Border Card(UIElement child,Thickness padding) {var border=new Border{Child=child,Padding=padding,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1)};border.SetResourceReference(Border.BackgroundProperty,"Card");border.SetResourceReference(Border.BorderBrushProperty,"Line");return border;}
        private void BuildView() {
            View.SetResourceReference(Panel.BackgroundProperty,"Bg");
            View.Children.Add(Page);Page.Content=dock;Page.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;Page.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            dock.LastChildFill=true;
            var top=header;top.Margin=new Thickness(24,18,24,14);DockPanel.SetDock(top,Dock.Top);dock.Children.Add(top);
            var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,24,8)};
            var logo=Text("⏻",27,"Accent");logo.Margin=new Thickness(0,0,10,0);brand.Children.Add(logo);
            var name=Text("PowerWallet",23,"Ink");name.FontWeight=FontWeights.SemiBold;brand.Children.Add(name);top.Children.Add(brand);
            var themes=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8),VerticalAlignment=VerticalAlignment.Center};top.Children.Add(themes);
            foreach(var option in new[]{new[]{"system","跟随系统"},new[]{"dark","深色"},new[]{"light","浅色"}}) {
                string mode=option[0];var b=new Button{Content=option[1],FontSize=12,Padding=new Thickness(10,6,10,6),MinHeight=34,Margin=new Thickness(0,0,5,0)};
                b.Click+=(s,e)=>SetTheme(mode,true);themeButtons.Add(mode,b);themes.Children.Add(b);
            }
            var footer=new StackPanel{Margin=new Thickness(24,14,24,16)};DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
            var actions=new WrapPanel();footer.Children.Add(actions);
            ActivateButton.Content="安装并激活";ActivateButton.Name="Activate";ActivateButton.MinWidth=174;ActivateButton.FontWeight=FontWeights.SemiBold;
            ActivateButton.SetResourceReference(BackgroundProperty,"Accent");ActivateButton.SetResourceReference(ForegroundProperty,"AccentInk");
            RestoreButton.Content="停用并恢复";RestoreButton.Name="Restore";RestoreButton.MinWidth=146;
            OpenButton.Content="打开手机插件";OpenButton.MinWidth=146;
            foreach(var b in new[]{ActivateButton,RestoreButton,OpenButton}){b.MinHeight=46;b.Margin=new Thickness(0,0,10,8);actions.Children.Add(b);}
            ActivateButton.Click+=async(s,e)=>await Operate("activate");RestoreButton.Click+=async(s,e)=>await Operate("restore");OpenButton.Click+=async(s,e)=>await Operate("open");
            var note=footerNote;note.FontSize=12;note.TextWrapping=TextWrapping.Wrap;note.SetResourceReference(TextBlock.ForegroundProperty,"Muted");note.Margin=new Thickness(0,1,0,5);footer.Children.Add(note);
            screenInfo.FontSize=11;screenInfo.TextWrapping=TextWrapping.Wrap;screenInfo.SetResourceReference(TextBlock.ForegroundProperty,"Muted");footer.Children.Add(screenInfo);
            var contentScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};dock.Children.Add(contentScroll);
            var body=new StackPanel{Margin=new Thickness(24,4,24,4)};contentScroll.Content=body;
            var eyebrow=Text("POWER × 2  →  GOOGLE WALLET",11,"Muted");eyebrow.FontWeight=FontWeights.SemiBold;eyebrow.Margin=new Thickness(0,0,0,8);body.Children.Add(eyebrow);
            var title=Text("电源键，换个用法。",30,"Ink");title.FontWeight=FontWeights.SemiBold;body.Children.Add(title);
            var subtitle=Text("双击电源键，直达 Google 钱包刷卡页面。",15,"Muted");subtitle.Margin=new Thickness(0,8,0,22);body.Children.Add(subtitle);
            hero.Add(eyebrow);hero.Add(title);hero.Add(subtitle);
            var statusContent=new StackPanel();
            statusPill.FontSize=11;statusPill.FontWeight=FontWeights.SemiBold;statusPill.SetResourceReference(TextBlock.ForegroundProperty,"Accent");statusContent.Children.Add(statusPill);
            heading.FontSize=23;heading.FontWeight=FontWeights.SemiBold;heading.TextWrapping=TextWrapping.Wrap;heading.Margin=new Thickness(0,7,0,8);heading.SetResourceReference(TextBlock.ForegroundProperty,"Ink");statusContent.Children.Add(heading);
            description.TextWrapping=TextWrapping.Wrap;description.SetResourceReference(TextBlock.ForegroundProperty,"Muted");description.LineHeight=23;statusContent.Children.Add(description);
            progress.Height=3;progress.IsIndeterminate=true;progress.Visibility=Visibility.Collapsed;progress.Margin=new Thickness(0,16,0,0);progress.SetResourceReference(Control.ForegroundProperty,"Accent");statusContent.Children.Add(progress);
            body.Children.Add(Card(statusContent,new Thickness(22,19,22,20)));
            var devicePanel=new StackPanel();
            var deviceHeader=new DockPanel{Margin=new Thickness(0,0,0,12)};
            refreshButton.Content="刷新";refreshButton.FontSize=12;refreshButton.Padding=new Thickness(12,5,12,5);refreshButton.MinHeight=32;refreshButton.Click+=async(s,e)=>await Refresh(true);
            DockPanel.SetDock(refreshButton,Dock.Right);deviceHeader.Children.Add(refreshButton);
            var deviceLabel=Text("手机连接",15,"Ink");deviceLabel.VerticalAlignment=VerticalAlignment.Center;deviceLabel.FontWeight=FontWeights.SemiBold;deviceHeader.Children.Add(deviceLabel);devicePanel.Children.Add(deviceHeader);
            devices.Items.Add("尚未发现手机");devices.SelectedIndex=0;devices.IsEnabled=false;devices.SelectionChanged+=async(s,e)=>{if(!changingSelection && !busy)await Refresh(true);};devicePanel.Children.Add(devices);
            deviceInfo.Margin=new Thickness(0,12,0,0);deviceInfo.FontSize=12;deviceInfo.TextWrapping=TextWrapping.Wrap;deviceInfo.SetResourceReference(TextBlock.ForegroundProperty,"Muted");devicePanel.Children.Add(deviceInfo);
            support.Margin=new Thickness(0,9,0,0);support.FontSize=12;support.TextWrapping=TextWrapping.Wrap;support.SetResourceReference(TextBlock.ForegroundProperty,"Muted");devicePanel.Children.Add(support);
            var deviceCard=Card(devicePanel,new Thickness(20));deviceCard.Margin=new Thickness(0,14,0,0);body.Children.Add(deviceCard);
            var steps=Text("首次连接\n① 在手机开发者选项中开启 USB 调试\n② 使用数据线连接，并允许这台电脑调试\n③ 点击“安装并激活”，再双击实体电源键测试",12,"Muted");steps.LineHeight=24;steps.Margin=new Thickness(2,16,0,12);body.Children.Add(steps);
            var expander=new Expander{Header="操作日志",FontSize=12,Margin=new Thickness(0,0,0,4)};expander.SetResourceReference(Control.ForegroundProperty,"Muted");
            logBox.IsReadOnly=true;logBox.TextWrapping=TextWrapping.Wrap;logBox.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;logBox.Height=142;logBox.BorderThickness=new Thickness(0);logBox.Padding=new Thickness(10);logBox.SetResourceReference(Control.BackgroundProperty,"Control");logBox.SetResourceReference(Control.ForegroundProperty,"Ink");
            expander.Content=logBox;body.Children.Add(expander);Content=View;
        }
        public void ApplyViewport(double width,double height) {
            bool compact=height<500;
            foreach(var element in hero)element.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            header.Margin=compact?new Thickness(24,10,24,3):new Thickness(24,18,24,14);
            ActivateButton.MinWidth=compact?140:174;RestoreButton.MinWidth=compact?120:146;OpenButton.MinWidth=compact?130:146;
            footerNote.Text=compact?"激活后可拔线；手机重启后需重新激活。":"激活后可拔掉数据线；手机重启后需要重新激活。";
            screenInfo.Visibility=height<400?Visibility.Collapsed:Visibility.Visible;
            dock.Height=width<440 && height<400?400:Math.Max(220,height);UpdateScreenInfo();
        }
        private void UpdateScreenInfo() {
            if(testMode){screenInfo.Text="Windows x64 · 手机插件 1.0.2 · 分辨率与缩放自适应";return;}
            var source=PresentationSource.FromVisual(this);
            double scale=source==null || source.CompositionTarget==null?1:source.CompositionTarget.TransformToDevice.M11;
            screenInfo.Text="桌面版 1.0.0 · 手机插件 1.0.2 · 当前缩放 "+Math.Round(scale*100)+"%";
        }
        public void ShowPhone(PhoneInfo value) {
            phone=value;
            if(!busy && !failed) {
                statusPill.Text=phone.Active?"已激活 · 可拔线使用":phone.CanActivate?"可以开始":phone.Device!=null && phone.Device.State=="unauthorized"?"等待手机授权":"准备连接";
                heading.Text=phone.Active?"钱包快捷键已就绪":phone.CanActivate?"手机已准备好":phone.Device==null?"连接你的小米手机":phone.Device.State!="device"?"请在手机完成授权":!phone.Supported?"此系统尚未适配":"还需要 Google 钱包";
                description.Text=phone.Reason.Length>0?phone.Reason:"用数据线连接手机，并打开 USB 调试。";
            }
            deviceInfo.Text=phone.Device!=null && phone.Device.State=="device"?phone.DisplayModel+" · Android "+phone.Android+"\n"+phone.Rom:"电脑会自动检测已连接的 Android 手机。";
            support.Text=phone.Device!=null && phone.Device.State=="device"?(phone.Supported?"✓ 此机型和系统版本已适配":"支持范围：小米 13 Ultra · "+WalletController.SupportedRom):"无需 root · 内置 ADB · 无需另外安装运行库";
            support.SetResourceReference(TextBlock.ForegroundProperty,phone.Device!=null && phone.Device.State=="device" && !phone.Supported?"Warn":"Muted");
            ActivateButton.IsEnabled=!busy && controller!=null && phone.CanActivate;
            RestoreButton.IsEnabled=!busy && controller!=null && phone.Device!=null && phone.Device.State=="device" && phone.HasBackup;
            OpenButton.IsEnabled=!busy && controller!=null && phone.CurrentHelper;
            refreshButton.IsEnabled=!busy && controller!=null;devices.IsEnabled=!busy && devices.Items.Count>0 && devices.Items[0] is Device;
            ActivateButton.Content=phone.Active?"重新激活":"安装并激活";
        }
        private void SetBusy(bool value,string title) {
            busy=value;progress.Visibility=value?Visibility.Visible:Visibility.Collapsed;
            if(value){failed=false;statusPill.Text="正在处理";heading.Text=title;description.Text="请保持数据线连接，并留意手机上的授权或安装提示。";}
            ShowPhone(phone);
        }
        private void AppendLog(string value) {
            if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>AppendLog(value)));return;}
            logBox.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+value+Environment.NewLine);logBox.ScrollToEnd();
        }
        private async Task Initialize() {
            SetBusy(true,"正在准备连接工具");
            try {
                await Task.Run(()=>Assets.Extract());
                controller=new WalletController(new AdbRunner(Path.Combine(Assets.DirectoryPath,"adb.exe")),Path.Combine(Assets.DirectoryPath,Assets.ApkName),Assets.Hash(Assets.Read(Assets.ApkName)),AppendLog);
                AppendLog("内置文件完整性检查通过。");
            }catch(Exception ex){ShowError(ex);}
            finally{SetBusy(false,"");}
            if(controller!=null){await Refresh(true);timer.Start();}
        }
        private void ShowError(Exception ex) {
            failed=true;statusPill.Text="操作未完成";heading.Text="请检查后重试";description.Text=WalletController.Redact(ex.Message,phone.Device);AppendLog(description.Text);
        }
        private async Task Refresh(bool explicitRefresh) {
            if(busy || controller==null)return;
            busy=true;if(explicitRefresh)failed=false;ShowPhone(phone);
            string selected=devices.SelectedItem is Device?((Device)devices.SelectedItem).Serial:lastDevice;
            try {
                var found=await Task.Run(()=>controller.Scan());
                Device chosen=found.FirstOrDefault(x=>x.Serial==selected) ?? found.FirstOrDefault(x=>x.State=="device") ?? found.FirstOrDefault();
                changingSelection=true;devices.Items.Clear();foreach(var d in found)devices.Items.Add(d);
                if(chosen!=null){devices.SelectedItem=chosen;lastDevice=chosen.Serial;}else{devices.Items.Add("尚未发现手机");devices.SelectedIndex=0;}
                changingSelection=false;
                phone=await Task.Run(()=>controller.Inspect(chosen));
                if(explicitRefresh)AppendLog(chosen==null?"没有发现手机，等待连接。":phone.DisplayModel+"："+phone.Reason);
            }catch(Exception ex){ShowError(ex);}finally{changingSelection=false;busy=false;ShowPhone(phone);}
        }
        private async Task Operate(string operation) {
            if(busy || controller==null || phone.Device==null)return;Device selected=phone.Device;
            SetBusy(true,operation=="activate"?"正在安装并激活":operation=="restore"?"正在恢复原快捷键":"正在打开手机插件");
            try {
                if(operation=="activate")phone=await Task.Run(()=>controller.Activate(selected));
                else if(operation=="restore")phone=await Task.Run(()=>controller.Restore(selected));
                else await Task.Run(()=>controller.OpenPlugin(selected));
                failed=false;
            }catch(Exception ex){ShowError(ex);}finally{SetBusy(false,"");}
        }
        public void SetDemo(bool active) {
            var device=new Device{Serial="test-device",State="device",Model="Xiaomi 13 Ultra"};
            devices.Items.Clear();devices.Items.Add(device);devices.SelectedIndex=0;
            controller=new WalletController(null,"", "",null);
            ShowPhone(new PhoneInfo{Device=device,Product="ishtar",Rom=WalletController.SupportedRom,Android="16",Supported=true,WalletInstalled=true,CurrentHelper=true,HasBackup=true,Active=active,Reason=active?"快捷键助手正在运行，可以拔掉数据线。":"此系统已适配，点击下方按钮安装并激活。"});
        }
    }
}
