# Ampheck Rockchip Toolkit

Rockchip 设备命令行工具，支持 RockUSB、串口、固件镜像和外部资产缓存。

支持 Windows（x64/ARM64）、Linux（x64/ARM64）和 macOS（ARM64）。

## 安装

从 GitHub Releases 下载对应平台的二进制并重命名为 `arkt`，Windows 为 `arkt.exe`。Linux 和 macOS 执行：

```bash
chmod +x arkt
```

Linux 下需要配置 USB/串口权限，或者使用 `sudo` 运行。

## RockUSB

```bash
arkt devices
arkt info
arkt capability
arkt chip info
arkt chip id
arkt loader MiniLoaderAll.bin
```

多台设备可用 `--vid`、`--pid` 选择。

```bash
arkt flash update.img
arkt flash ./firmware
arkt flash http://example.com/update.img
arkt flash update.img --loader loader.bin
arkt flash update.img --watch
```

`--watch` 监听新插入的 MaskROM，每次触发都会重新读取当前镜像；已有设备或
存在多台匹配设备时不会自动烧录，烧录失败会停止监听，`Ctrl+C` 停止。

```bash
arkt read dump.bin --sector 0 --count 2048
arkt write boot.img --sector 0x8000
arkt write boot.img --sector 0x8000 --backup boot-backup.img
arkt erase --sector 0x8000 --count 2048
arkt storage emmc
arkt reset normal
```

`write` 直接按扇区写入，不会检查分区边界。

## 固件镜像

```bash
arkt image unpack update.img ./firmware
arkt image pack ./firmware update-new.img
arkt image pack ./firmware update-new.img --chip RK3399
arkt verify update.img
```

默认不覆盖输出，使用 `--force` 覆盖。
烧录 update image 前会自动校验结构和内嵌校验和。

## 串口

```bash
arkt serial list
arkt serial monitor /dev/ttyUSB0
arkt serial monitor /dev/ttyUSB0 --baud 1500000
arkt serial send /dev/ttyUSB0 "reboot"
```

串口由当前进程独占。`Ctrl+]` 退出监控。

## 资产

第三方二进制按 manifest 下载到本地缓存，不随 ARKT 发布。缓存目录可用 `ARKT_ASSETS_DIR` 修改。

```bash
arkt assets root
arkt assets ensure --manifest manifest.toml --output ./assets
arkt assets verify --manifest manifest.toml
arkt assets list
arkt assets prune --dry-run
arkt assets prune --yes
arkt assets forget --manifest manifest.toml
```

rkbin 从 Rockchip 上游仓库获取，授权不变。

## 其他

```bash
arkt completion bash
```

补全支持 `bash`、`zsh`、`fish` 和 `powershell`。

## SDK

当前尚未发布到 NuGet。外部项目可以直接引用源码项目：

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Ampheck.RockchipToolkit/Ampheck.RockchipToolkit.csproj" />
  <ProjectReference Include="path/to/Ampheck.RockchipToolkit.Hardware/Ampheck.RockchipToolkit.Hardware.csproj" />
</ItemGroup>
```

```csharp
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Firmware;
using Ampheck.RockchipToolkit.Hardware;

var firmware = new FirmwareFlasher(
    new UsbDeviceProviderFactory());

await firmware.FlashAsync(
    new DeviceTarget(),
    "update.img",
    options: null,
    progress: null,
    CancellationToken.None);
```

SDK 只接收本地文件路径。HTTP 下载、Assets 缓存和 manifest 由调用方或 ARKT CLI 处理。
耗时操作通过 `IProgress<OperationProgress>` 返回阶段、条目和字节进度；速度与 ETA 由调用方计算。

## 构建

需要 .NET 10 SDK。发布 CLI：

```bash
dotnet publish src/Ampheck.RockchipToolkit.Cli/Ampheck.RockchipToolkit.Cli.csproj \
  -c Release -r linux-x64 --self-contained true -o release
```
