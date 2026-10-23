# 在本机 Windows 运行 TikTok MCP Desktop

本仓库是 **WPF 桌面程序**。Cloud Agent 跑在 Linux 上，**不能直接替你点开 Windows 窗口**。请在本机执行下面任一条路径。

## 路径 A（推荐）：本机一键运行

1. 用 Cursor / 资源管理器打开本仓库目录（或 `git pull origin main` 拉最新）。
2. 安装：
   - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - [Node.js 18+](https://nodejs.org/)
   - [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（多数 Win10/11 已带）
3. PowerShell：

```powershell
cd <仓库根目录>
.\scripts\run-desktop.ps1
```

4. 窗口打开后：设置里可留空 `client_id` → **连接** → **授权登录**。

## 路径 B：让 Cloud Agent 在你的 Windows 上代跑

需要本机开 Cursor 自托管 Worker，Agent 才能连上你的电脑：

```powershell
# 安装 Cursor CLI 并登录后：
agent login
agent worker start
```

文档：https://cursor.com/docs/cloud-agent/self-hosted/my-machines  

Worker 在线后，再在对话里说「在我的 Windows 机器上运行桌面端」。

## 打包 exe

```powershell
.\scripts\publish-win-x64.ps1
# 产物：publish\win-x64\TikTokMcpDesktop.exe
```
