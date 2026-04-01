# ProxiFyre UI

**ProxiFyre UI** 是一个基于 `WPF` 和 `WPF UI 4.0` 打造的现代化、高性能系统托盘辅助工具。它为底层的网络代理核心程序 `ProxiFyre.exe` 提供了一个优雅的图形化配置界面 (GUI)。

ProxiFyre 是一款 Windows 平台上的开源 SOCKS5 透明代理工具。支持透明代理指定的进程，能让那些本身完全不支持代理设置的应用程序，实现真正的透明转发。

## 核心特性

- **极致现代化 UI**：全面采用最新的 `lepoco/wpfui` 组件库，符合 Windows 11 设计语言。
- **智能环境检测与自愈**：启动时自动检测 `ProxiFyre.exe`、`NDISAPI 驱动` 及 `VC++ 运行时`，若有缺失会自动弹出对话框，支持一键异步多线程下载并静默安装。
- **自动代理感知**：在下载环境依赖时，会自动读取并应用用户配置的代理节点，确保在国内网络环境下也能高速下载 GitHub 资源。
- **极简进程管理**：实时捕获并滚动输出底层核心程序的运行日志。

## 预览
<img width="700" height="550" alt="demo" src="https://github.com/user-attachments/assets/ffb0b8c2-22af-4b55-848f-dd88487a9b78" />

## 快速上手

还原依赖并运行
```bash
dotnet run
```
执行 Release 构建
```bash
dotnet build -c Release
```
> 构建成功后，可执行文件及依赖将被输出至 `bin/Release/net48/` 目录。

## 技术栈

- **.NET Framework 4.8** - 基础框架
- **WPFUI** - 现代化 UI 库
- **CommunityToolkit.Mvvm** - 微软官方的高性能 MVVM 架构框架
- **Newtonsoft.Json** - 高效的 JSON 配置文件解析

## 鸣谢

- **wpfui** - 提供了 [WPF UI 4.0](https://github.com/lepoco/wpfui) 组件库，使此项目能够实现现代化的 UI 设计。
- **proxifyre-tray** - 参考了 [proxifyre-tray](https://github.com/airenelias/proxifyre-tray) 的设计。
- **proxifyre** - 基于 [proxifyre](https://github.com/wiresock/proxifyre) 核心，站在巨人肩膀上。
