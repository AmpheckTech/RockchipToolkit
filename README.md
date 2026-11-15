# Ampheck Rockchip Toolkit

Rockchip 设备命令行工具，支持 RockUSB、串口、ADB 和固件镜像处理。

## 功能

- RockUSB 设备信息、烧录、读写、擦除和备份
- `update.img`、固件目录和 ZIP 烧录
- 串口终端和历史缓冲
- ADB Shell、Push、Pull 和重启
- RKFW/RKAF 打包与拆包

支持 Windows 和 Linux x64/ARM64。

## 安装

从 GitHub Releases 下载，Linux 下载后执行：

```bash
chmod +x arkt
```

ADB 需要 Android SDK Platform Tools，也可以指定路径：

```bash
export ARKT_ADB=/opt/android-sdk/platform-tools/adb
```

## 本机使用

普通命令会自动启动本机 Server：

```bash
arkt devices
arkt serial list
arkt adb devices
```

Server 管理：

```bash
arkt server start
arkt server status
arkt server stop
```

Linux 下需要配置 USB/串口权限，或者使用 `sudo` 启动 Server。

## 远程使用

连接设备的机器：

```bash
arkt server nodaemon --listen 0.0.0.0:5070 --token your-token
```

客户端：

```bash
export ARKT_SERVER=10.0.0.2:5070
export ARKT_SERVER_TOKEN=your-token
arkt devices
```

完整参数见 `arkt server nodaemon --help`。通信未加密，不要直接暴露到公网。

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

`--fetch client` 可让客户端下载 HTTP(S) 固件。

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
arkt serial open /dev/ttyUSB0
arkt serial terminal /dev/ttyUSB0
arkt serial history /dev/ttyUSB0
arkt serial clear /dev/ttyUSB0
arkt serial close /dev/ttyUSB0
```

终端按 `Ctrl+]` 退出。其他参数见 `arkt serial open --help`。

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

## 其他

```bash
arkt discover
arkt completion bash
```

补全支持 `bash`、`zsh`、`fish` 和 `powershell`。

## 构建

需要 .NET 10 SDK：

```bash
dotnet restore Ampheck.RockchipToolkit.slnx
dotnet build Ampheck.RockchipToolkit.slnx -c Release
```
