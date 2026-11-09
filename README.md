# PowerWallet · 电源键钱包

双击小米手机电源键，直接打开 **Google 钱包刷卡页面**。Windows 图形工具内置 ADB 和手机插件，可安装、激活、检查状态，或恢复原快捷键，无需 root。

**目前仅适配：小米 13 Ultra / ishtar，国行 HyperOS `OS3.0.308.0.WMACNXM`，Android 16。** 未适配的设备不会被激活。系统更新后需要重新验证兼容性。

[下载 Windows EXE](https://github.com/DustinYe/PowerWallet/releases/latest) · [完整使用说明](docs/usage.md) · [验证记录](docs/verification.md)

![PowerWallet 深色界面（布局测试数据）](docs/PowerWallet-dark.png)

## Windows 版

- 单个 EXE，内置 Android platform-tools 37.0.1 的 ADB、USB 组件和手机插件 1.0.2。
- 自动检测手机、USB 调试授权、机型、系统版本和 Google 钱包。
- 一键安装并激活、停用并恢复、打开手机插件。
- 深色、浅色、跟随系统三种主题，保存用户选择，标题栏和滚动条同步适配。
- Per-Monitor V2 DPI 感知，按显示器工作区限制初始窗口大小，响应 DPI / 显示器变化。
- 小屏幕自动换行并提供滚动访问，不修改电脑分辨率或缩放设置。
- 状态来自运行中的助手，不能仅凭旧日志判定已激活。

支持 Windows 10 / 11 x64（.NET Framework 4.8，当前 Windows 10 / 11 通常已内置）。使用标准用户权限即可，不需要管理员运行。

## 使用

1. 从 Releases 下载并运行 `PowerWallet-Setup-1.0.0.exe`。它是便携式工具，不会安装 Windows 服务。
2. 在手机开发者选项中开启 **USB 调试**，使用数据线连接电脑并允许调试授权。
3. 在工具中选择手机，点击 **安装并激活**。如果手机要求确认 USB 安装，请在手机上完成。
4. 看到“钱包快捷键已就绪”后，双击实体电源键测试。

**激活完成后可以拔线；手机重启后必须重新连接电脑激活。** 助手若被系统终止，也需要重新激活。仅安装 APK 不会启动按键助手。

支付仍由 Google 钱包处理，并遵循它的验证和设备安全要求。本工具不读取银行卡、NFC 交易数据或 Google 账户，也不执行付款。真实读卡器交易未在本次开发中测试。

## 手机插件

手机桌面上的“电源键钱包”提供激活状态、打开 Google 钱包、停用并恢复入口。

- 1.0.1：修复人脸验证成功但尚未上滑退出锁屏时无法显示钱包的问题。
- 1.0.2：为电脑端增加实时状态和重启后的恢复命令，按键识别与钱包启动逻辑保持 1.0.1 的行为。

## 原理与权限

在已验证的这版国行系统中，框架 `google_pay` 快捷操作实现为空，而 `mi_pay` 固定打开小米钱包。ADB shell 助手保存原设置，将双击操作设置为该空操作，再监听实体电源键事件，通过 Google 钱包的 `QUICKDRAW` 入口打开卡片页面。

人脸验证已完成、锁屏页仍显示时，助手请求系统收起锁屏后再启动钱包；系统仍负责验证。工具不修改系统文件，不替换小米钱包，不需要 root 或无障碍服务。

Android APK 不请求网络权限。电脑端仅在本机调用内置 ADB，不包含遥测、自动更新或外网请求。ADB 的本机服务和手机调试传输仍由 Android platform-tools 处理。

## 从源码构建

Windows 端无需 NuGet 或额外 UI 框架，使用 .NET Framework 4.8 自带 C# 编译器和 WPF：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\desktop\build.ps1
.\dist\PowerWallet-Setup-1.0.0.exe --self-test .\artifacts
```

自检离线进行，覆盖兼容性拒绝、未授权、缺少钱包、安装失败、恢复、安全日志和 38 种主题 / 分辨率 / 缩放布局组合；不会激活或更改连接的手机。

Android 端需要 JDK 21、Android SDK Platform 36 与 Build Tools 36.0.0：

```powershell
.\android\Build.ps1 -Sdk 'Android SDK 路径' -Jdk 'JDK 路径' -BuildDirectory '构建目录' -ApkPath '.\desktop\assets\PowerWallet-1.0.2.apk'
```

仓库不包含 APK 签名私钥。自行构建的不同签名 APK 不能直接覆盖正式版；如果切换签名，先恢复原快捷键，再卸载旧插件。正式版内置 APK 的哈希见 `desktop/assets/SHA256SUMS.txt`。

## 许可

本项目自有源码采用 [MIT](LICENSE) 许可。内置 ADB 和 USB 组件遵循其原许可，完整声明保存在 [Android platform-tools NOTICE](desktop/assets/NOTICE.txt)。本项目独立开发，与 Xiaomi、Google 无隶属或背书关系。
