# ProxiFyre UI

**ProxiFyre UI** 是一个基于 `WPF` 和 `WPF UI 4.0` 打造的现代化、高性能系统托盘辅助工具。它为底层的网络代理核心程序 `ProxiFyre.exe` 提供了一个优雅的图形化配置界面 (GUI)。

## 核心特性

- **极致现代化 UI**：全面采用最新的 `lepoco/wpfui` 组件库，符合 Windows 11 设计语言。
- **智能环境检测与自愈**：启动时自动检测 `ProxiFyre.exe`、`NDISAPI 驱动` 及 `VC++ 运行时`，若有缺失会自动弹出对话框，支持一键异步多线程下载并静默安装。
- **自动代理感知**：在下载环境依赖时，会自动读取并应用用户配置的代理节点，确保在国内网络环境下也能高速下载 GitHub 资源。
- **极简进程管理**：实时捕获并滚动输出底层核心程序的运行日志。

## 预览
<img width="700" height="550" alt="demo" src="https://github.com/user-attachments/assets/18187eb5-d3d2-451d-99f2-696e5f8780cf" />

## 快速上手

还原依赖并运行
```bash
dotnet run
```
执行 Release 构建
```bash
dotnet build -c Release
```
> 构建成功后，可执行文件及依赖将被输出至 `bin/Release/net472/` 目录。

## 技术栈

- **.NET Framework 4.7.2** (SDK-style project)
- **[WPFUI](https://github.com/lepoco/wpfui)** - v4.0 现代化 UI 库
- **CommunityToolkit.Mvvm** - 微软官方的高性能 MVVM 架构框架
- **Newtonsoft.Json** - 高效的 JSON 配置文件解析

## 鸣谢

- **lepoco** - 提供了 `WPF UI 4.0` 组件库，使 ProxiFyre UI 能够实现现代化的 UI 设计。
- **airenelias/proxifyre-tray** - 本项目借鉴了 [`airenelias/proxifyre-tray`](https://github.com/airenelias/proxifyre-tray) 的实现，并且进行重构。
- **wiresock/proxifyre** - 本项目基于 [`wiresock/proxifyre`](https://github.com/wiresock/proxifyre) 核心，站在巨人肩膀上。
