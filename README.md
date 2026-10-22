# Ampheck Rockchip Toolkit

用于 Rockchip 设备的命令行工具。`arkt` 可以本机直接使用，也可以通过 Server 从另一台机器操作 USB 设备和串口。

## 功能

- 查看 RockUSB 设备、芯片信息、存储信息和能力
- 烧录 `update.img`
- 烧录固件目录或 ZIP，并按 `parameter.txt` 识别分区
- 从本地路径、Windows UNC 路径或 HTTP(S) 地址加载固件
- 按扇区读取、擦除和写入任意位置
- 写入前可备份目标区域
- 串口打开、关闭、交互终端和历史读取
- 多客户端共享同一串口
- ADB 设备列表、Shell、Push、Pull 和重启到 Loader
- Server 提供 ADB 转发
- 局域网自动发现 Server
- 支持 Windows、Linux（x64/ARM64）

## 获取

从 GitHub Releases 下载对应平台的文件，或者从源码构建。

Linux 下载后需要添加执行权限：

```bash
chmod +x arkt
```

## 本机直接使用

RockUSB 和串口命令会先探测本机 `127.0.0.1:5070`。ADB 默认使用本机
`5037` 端口。

如果本机没有 Server，RockUSB 和 ADB 操作会直接在本机执行。串口需要持久打开，因此要先启动 Server：

```bash
./arkt server nodaemon --hostname arkt-server
```

Server 启动后，在另一个终端执行：

```bash
./arkt serial open /dev/ttyUSB0
./arkt serial terminal /dev/ttyUSB0
```

按 `Ctrl+]` 退出当前串口终端。物理串口会继续保持打开；需要关闭时执行：

```bash
./arkt serial close /dev/ttyUSB0
```

## 远程使用

在连接开发板的机器上启动 Server：

```bash
./arkt server nodaemon \
  --listen 0.0.0.0:5070 \
  --hostname arkt-server
```

在另一台机器上设置 Server 地址：

```bash
export ARKT_SERVER=10.0.0.2:5070
```

```bash
arkt devices
arkt serial list
arkt adb devices
```

Server 设置了 token 时：

```bash
export ARKT_SERVER_TOKEN=your-token
```

客户端名称会显示在 Server 日志中，默认使用机器名。可以覆盖：

```bash
export ARKT_CLIENT_NAME=build-pc-01
```

### Server 参数

```text
--listen host:port       监听地址，默认 127.0.0.1:5070
--hostname NAME          局域网发现和日志中显示的 Server 名称
--token TOKEN            可选的客户端访问 token
--idle-timeout SECONDS   无客户端连接后自动退出，0 表示不退出
--max-attachment-mib N   单个上传附件上限
--max-request-mib N      单次请求上传总量上限
--serial-history-kib N   每个串口的历史缓冲大小
```

Server 同时提供 ADB 服务，默认监听 `5037`。端口被占用时其他功能仍可使用，
但日志中会提示 ADB 服务不可用。

当前通信未启用加密。ADB Server 也没有认证机制，因此不要把 `0.0.0.0:5070`
或 `0.0.0.0:5037` 直接暴露到公网。

## 发现 Server

```bash
arkt discover
```

输出的 endpoint 可以直接用于客户端：

```bash
export ARKT_SERVER=10.0.0.2:5070
```

## 命令补全

Bash 中临时启用：

```bash
source <(arkt completion bash)
```

写入 `~/.bashrc` 可长期生效。`completion` 还支持 `zsh`、`fish` 和
`powershell`。

## RockUSB

在 Windows 使用 RockUSB 前需要安装对应的 Rockchip USB 驱动。Linux 需要允许
当前用户访问设备的 USB 节点。

### 查看设备

```bash
arkt devices
arkt info
arkt capability
arkt chip info
arkt chip id
```

多台设备同时连接时，可以使用 `--vid` 和 `--pid` 指定设备。

### 烧录 update.img

```bash
arkt flash update.img
```

烧录完成后默认重启设备。保留在 Loader 模式：

```bash
arkt flash update.img --no-reset
```

MaskROM 模式可以自动使用 `update.img` 内置的 Loader，也可以手动指定：

```bash
arkt flash update.img --loader loader.bin
```

### 烧录固件目录

固件目录中的镜像由 `parameter.txt` 决定。常见内容如下：

```text
parameter.txt
boot.img
rootfs.img
...
```

工具只烧录目录中存在的分区镜像：

```bash
arkt flash ./firmware
```

只烧录 boot 时，准备一个只包含 `parameter.txt` 和 `boot.img` 的目录：

```bash
mkdir boot-only
cp parameter.txt boot.img boot-only/
arkt flash boot-only
```

### 通过 HTTP(S) 烧录

连接 Server 时，URL 默认由 Server 下载：

```bash
arkt flash http://127.0.0.1/update.img
```

如果 URL 只能由客户端访问：

```bash
arkt flash http://127.0.0.1/update.img --fetch client
```

由客户端下载后上传给 Server。

### 原始扇区写入

`--sector` 是目标起始扇区，每个扇区 512 字节：

```text
目标偏移 = 扇区号 × 512
```

例如从 `0x800000` 开始写：

```bash
arkt write boot.img --sector 0x4000
```

写入文件的一部分：

```bash
arkt write image.bin \
  --sector 0x8000 \
  --source-offset 0x100000 \
  --length 0x400000
```

写入前备份目标范围：

```bash
arkt write boot.img \
  --sector 0x8000 \
  --backup boot-backup.img
```

`write` 不读取 `parameter.txt`。它只按给定扇区直接写，地址或长度错误可能覆盖其他分区。

### 读取和擦除

读取一个扇区：

```bash
arkt read dump.bin --sector 0 --count 1
```

读取 1 MiB：

```bash
arkt read dump.bin --sector 0 --count 2048
```

擦除一段扇区：

```bash
arkt erase --sector 0x8000 --count 2048
```

### 存储和重启

```bash
arkt storage emmc
arkt storage sd-card
arkt storage spi-nor

arkt reset normal
arkt reset power-off
arkt reset reset-mask-rom
arkt reset disconnect
```

## 串口

查看串口：

```bash
arkt serial list
```

打开并保留串口：

```bash
arkt serial open /dev/ttyUSB0
```

默认波特率是 `1500000`。需要其他速率时使用 `--baudrate`。

其他串口参数：

```text
--data-bits 5..8
--parity none|odd|even|mark|space
--stop-bits one|one-point-five|two
--handshake none|xon-xoff|rts-cts|rts-cts-xon-xoff
--no-dtr
--no-rts
```

进入交互终端：

```bash
arkt serial terminal /dev/ttyUSB0
```

按 `Ctrl+]` 退出终端。串口保持打开，USB 转串口掉线后会自动重连。

读取历史：

```bash
arkt serial history /dev/ttyUSB0
arkt serial history /dev/ttyUSB0 --bytes 4096
```

关闭串口：

```bash
arkt serial close /dev/ttyUSB0
```

## ADB

查看设备：

```bash
arkt adb devices
```

执行命令：

```bash
arkt adb shell uname -a
```

管道、循环等多段命令使用 `-c`：

```bash
arkt adb shell -c 'for f in /proc/[0-9]*; do echo "$f"; done'
```

进入交互 Shell：

```bash
arkt adb shell
```

文件传输：

```bash
arkt adb push ./local.bin /tmp/local.bin
arkt adb pull /tmp/local.bin ./local.bin
```

重启设备：

```bash
arkt adb reboot loader
arkt adb reboot normal
arkt adb reboot recovery
```

设置 `ARKT_SERVER` 后，ADB 请求由 Server 转发。ADB 端口默认是 `5037`，
Server 和客户端需要使用相同的端口值：

```bash
export ARKT_ADB_PORT=5038
```

发布物还包括独立的 `adbcli`，不依赖 `arkt` 或系统安装的 `adb`：

```bash
adbcli devices
adbcli shell
adbcli push ./local.bin /tmp/local.bin
adbcli server nodaemon
```

## 固件输入形式

`flash` 支持：

- 本地或 HTTP(S) 的 `update.img`
- 本地或 HTTP(S) 的固件目录
- 本地或 HTTP(S) 的 `firmware.zip`

远程模式下，本地固件会上传到 Server。

## 构建

需要 .NET 10 SDK。

```bash
dotnet restore Ampheck.RockchipToolkit.slnx
dotnet build Ampheck.RockchipToolkit.slnx -c Release
```

生成 Linux x64 可执行文件：

```bash
dotnet publish \
  src/Ampheck.RockchipToolkit.Cli/Ampheck.RockchipToolkit.Cli.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -o release/arkt
```

Windows 将 RID 换成 `win-x64`。
