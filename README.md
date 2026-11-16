# Ampheck Rockchip Toolkit

Rockchip 设备命令行工具，支持 RockUSB、串口、ADB 和固件镜像处理。

## 功能

- RockUSB 设备信息、烧录、读写、擦除和备份
- `update.img`、固件目录和 ZIP 烧录
- 串口监控和发送
- ADB Shell、Push、Pull 和重启
- RKFW/RKAF 打包与拆包

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

外部资产通过版本化 TOML manifest 按需下载、校验和缓存，不随 ARKT 发布。

```bash
arkt assets root
arkt assets ensure --manifest manifests/rkbin-rk3588.toml
arkt assets ensure --manifest manifests/rkbin-rk3588.toml --output ./assets
arkt assets ensure --manifest manifests/rkbin-rk3588.toml --output ./assets --link
arkt assets verify --manifest manifests/rkbin-rk3588.toml
arkt assets list
arkt assets forget --manifest manifests/rkbin-rk3588.toml
arkt assets prune --dry-run
arkt assets prune --yes
```

Manifest 使用 `schema = 1`，每个资产固定 `commit`、`path`、`output`、
`sha256`、`purpose` 和 `optional`。
缓存默认位于平台应用数据目录，可通过 `ARKT_ASSETS_DIR` 覆盖。
`--output` 默认复制文件；只有显式传入 `--link` 才使用 hardlink。
`verify` 只读取本地缓存，始终不会访问网络。
同一 manifest 路径再次成功 `ensure` 时会替换旧引用集合。
`forget` 只删除 manifest 引用记录，不删除缓存文件。
`rkbin` 文件从固定上游 commit 获取；ARKT 不内置这些二进制，也不授予额外权利。

## 其他

```bash
arkt completion bash
```

补全支持 `bash`、`zsh`、`fish` 和 `powershell`。

## 构建

最终发布单个 `arkt` 可执行文件。
需要 .NET 10 SDK：

```bash
dotnet restore Ampheck.RockchipToolkit.slnx
dotnet build Ampheck.RockchipToolkit.slnx -c Release
```

发布：

```bash
dotnet publish Ampheck.RockchipToolkit.csproj \
  -c Release -r linux-x64 --self-contained true -o release
```
