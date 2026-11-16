# Ampheck Rockchip Toolkit

Rockchip 设备命令行工具，支持 RockUSB、串口、ADB、固件镜像和外部资产缓存。

支持 Windows、Linux 和 macOS ARM64。

## 安装

从 GitHub Releases 下载 `arkt`，Linux 和 macOS 下载后执行：

```bash
chmod +x arkt
```

ADB 需要 Android SDK Platform Tools，也可以指定路径：

```bash
export ARKT_ADB=/opt/android-sdk/platform-tools/adb
```

Linux 下需要配置 USB/串口权限，或者使用 `sudo` 运行。

## RockUSB

```bash
arkt devices
arkt info
arkt capability
arkt chip info
arkt chip id
```

多台设备可用 `--vid`、`--pid` 选择。

```bash
arkt flash update.img
arkt flash ./firmware
arkt flash http://example.com/update.img
arkt flash update.img --loader loader.bin
```

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
```

默认不覆盖输出，使用 `--force` 覆盖。

## 串口

```bash
arkt serial list
arkt serial monitor /dev/ttyUSB0
arkt serial monitor /dev/ttyUSB0 --baud 1500000
arkt serial send /dev/ttyUSB0 "reboot"
```

串口由当前进程独占。`Ctrl+]` 退出监控。

## ADB

```bash
arkt adb devices
arkt adb shell uname -a
arkt adb shell
arkt adb push ./local.bin /tmp/local.bin
arkt adb pull /tmp/local.bin ./local.bin
arkt adb reboot loader
```

多台设备使用 `--serial` 选择。

## 资产

第三方二进制按 manifest 下载到本地缓存，不随 ARKT 发布。缓存目录可用 `ARKT_ASSETS_DIR` 修改。

```bash
arkt assets root
arkt assets ensure --manifest manifests/rkbin-rk3588.toml --output ./assets
arkt assets verify --manifest manifests/rkbin-rk3588.toml
arkt assets list
arkt assets prune --dry-run
```

rkbin 从 Rockchip 上游仓库获取，授权不变。

## 其他

```bash
arkt completion bash
```

补全支持 `bash`、`zsh`、`fish` 和 `powershell`。

## 构建

需要 .NET 10 SDK，执行：

```bash
dotnet publish Ampheck.RockchipToolkit.csproj \
  -c Release -r linux-x64 --self-contained true -o release
```
