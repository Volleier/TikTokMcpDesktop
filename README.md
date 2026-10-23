# TikTok MCP Desktop

独立 Windows 桌面小工具：配置 OpenAI 兼容 API → 授权 TikTok for Business Open MCP → 只读拉数落盘 → 自然语言查数 → 本机在线表格与多数据看板。

与任何 SaaS / AIEcomPlex 无依赖。

## 功能

- **设置**：`base_url` / `api_key` / `model` / `client_id` / MCP 变体（默认 layer）/ 同步间隔；密钥本地加密（Windows DPAPI）
- **OAuth PKCE**：浏览器授权 → `127.0.0.1` 回调 → **JSON body** 换票；自动 refresh
- **MCP**：`tools/list` / 白名单 `tools/call`；SSE 解析；写工具硬拦截
- **对话**：OpenAI Chat Completions + function calling → MCP
- **同步**：手动 / 定时拉数 → `data/pulls/{date}/` + SQLite
- **表格 / 看板**：进程内 LocalApi（仅 loopback）+ WebView2 嵌入 Vue UI（交互参考 ZhiQu smart-table / dashboard，自研瘦客户端）

## 环境要求

- Windows 10/11 x64（正式桌面端）
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（开发）
- [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
- Node.js 18+（构建 `web/ui`）
- TikTok for Business 开发者应用：登记 `client_id`，redirect `http://127.0.0.1:{port}/callback`（默认端口 `17865`）

## 仓库结构

```
src/TikTokMcpDesktop/           WPF 壳（Windows）
src/TikTokMcpDesktop.Core/      OAuth / MCP / OpenAI / Jobs / Store
src/TikTokMcpDesktop.LocalApi/  loopback Kestrel API
src/TikTokMcpDesktop.DevHost/   跨平台 LocalApi 预览宿主（Linux/macOS/Windows）
web/ui/                         Vue3 表格 + 看板
tests/                          单元 / 集成测试
scripts/                        打包脚本
```

## 开发

```bash
# 测试（Linux / macOS / Windows）
dotnet test tests/TikTokMcpDesktop.Core.Tests -c Release

# 构建嵌入式前端
cd web/ui && npm install && npm run build && cd ../..

# 跨平台预览表格/看板（无需 WPF）
dotnet run --project src/TikTokMcpDesktop.DevHost -c Release
# 浏览器打开终端打印的 http://127.0.0.1:<port>/#/tables

# Windows 上跑完整桌面端
dotnet run --project src/TikTokMcpDesktop -c Release
```

## Windows 打包

在 Windows 上执行：

```powershell
.\scripts\publish-win-x64.ps1
```

产物：`publish/win-x64/TikTokMcpDesktop.exe`（自包含单文件）+ 旁路 `web/ui/dist`。

## 使用步骤

1. 打开应用 → **设置**：填入 OpenAI 兼容 `base_url`、API Key、model、TikTok `client_id` → 保存  
2. **连接** → 授权登录（浏览器 TT4B）→ 显示「已连接」→ 可探测 `tools/list`  
3. **同步** → 立即同步；结果写入本地  
4. **表格 / 看板**：浏览拉数结果、筛选排序、导出 CSV、切换预置看板  
5. **对话**：用自然语言触发只读 MCP 工具  

授权约 30 天；refresh 失效时按提示重新授权。切换 flat/layer 会分存 token，需重授权。

## 安全默认

- Redirect 与 LocalApi 仅绑定 `127.0.0.1`
- 写类 MCP 工具默认禁用
- 日志脱敏（token / api key 不落明文）

## 验收对照

- [x] Core：PKCE、SSE、白名单、入库、看板、LocalApi loopback 测试通过  
- [x] Web UI：表格筛选 / CSV / 三套预置看板  
- [x] WPF 壳：设置 / 授权 / 同步 / 对话 / WebView2 宿主（需在 Windows 运行）  
- [ ] 真机 TikTok 授权与真实 MCP 工具：需有效 `client_id` 与 TT4B 账号（本环境为 Linux，无法跑 WPF GUI）

## 参考

- TikTok for Business Open MCP 官方授权流程  
- 表格/看板交互参考 [jiangzhikj/zq-platform](https://github.com/jiangzhikj/zq-platform)（`zq-smart-table` / `dashboard-design`），不复用其服务端
