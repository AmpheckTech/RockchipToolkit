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

本机 loopback 模式直接使用 Server 本地路径，不经过文件上传下载。远端模式
自动使用 blob 传输，也可通过 `ARKT_FILE_MODE=auto|server|upload` 覆盖。

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

## MCP

`arkt-mcp` 提供 Streamable HTTP MCP 接口，并通过本机 WebSocket RPC 连接
Toolkit Server。

```bash
arkt-mcp --server 127.0.0.1:5070 --listen http://127.0.0.1:5071
```

MCP 地址为 `http://127.0.0.1:5071/mcp`。也可以通过 `ARKT_SERVER`、
`ARKT_SERVER_TOKEN` 和 `ARKT_MCP_LISTEN` 配置连接与监听地址。
文件传输模式可通过 `--file-mode auto|server|upload` 或 `ARKT_FILE_MODE` 覆盖。

提供的工具：

- 设备：`list_devices`、`get_device_info`、`get_chip_info`、`get_chip_id`、
  `get_capabilities`、`load_loader`、`reset_device`
- 固件和存储：`flash_firmware`、`read_storage`、`write_storage`、
  `erase_storage`、`select_storage`
- 串口：`list_serial_ports`、`open_serial_port`、`close_serial_port`、
  `clear_serial_buffer`、`read_serial_history`
- ADB：`list_adb_devices`、`adb_shell`、`adb_push`、`adb_pull`、
  `reboot_adb_device`
- 任务和服务：`list_tasks`、`get_task`、`cancel_task`、`get_server_info`

## 构建

需要 .NET 10 SDK：

```bash
dotnet restore Ampheck.RockchipToolkit.slnx
dotnet build Ampheck.RockchipToolkit.slnx -c Release
```

发布 MCP Bridge：

```bash
dotnet publish \
  src/Ampheck.RockchipToolkit.Mcp/Ampheck.RockchipToolkit.Mcp.csproj \
  -c Release -r linux-x64 -o release/arkt-mcp
```
