# Third-party components

The Windows executable embeds these files from Google's Android SDK platform-tools 37.0.1:

- `adb.exe`
- `AdbWinApi.dll`
- `AdbWinUsbApi.dll`
- `NOTICE.txt`
- `source.properties`

The original, complete notices are included in `desktop/assets/NOTICE.txt` and extracted alongside these files when the desktop application runs. Original version metadata is included in `desktop/assets/source.properties`.

The project uses Windows Presentation Foundation and the .NET Framework 4.8 installed with Windows. Those Microsoft runtime files are not redistributed by this repository or executable.
