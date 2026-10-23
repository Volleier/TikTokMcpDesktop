# TikTok MCP Desktop

**Windows 桌面程序**（WPF）：配置 OpenAI 兼容 API → 授权 **TikTok for Business Open MCP** → 只读拉数落盘 → 自然语言查数 → 本机表格与多数据看板。

- 不是网站产品；表格/看板嵌在桌面窗口内，数据来自本机。
- 与 AIEcomPlex **无代码/库表依赖**；Open MCP 鉴权契约对齐其 `dev` 已验证实现。
- **不是** Shop OpenAPI，也**不是** Marketing API 的 `Access-Token` 头。

## 功能

- **设置**：`base_url` / `api_key` / `model` / `client_id`（及可选 `app_id`/`app_secret`）/ MCP 变体（默认 layer）；密钥 DPAPI 加密
- **OAuth PKCE（Open MCP）**：**默认无需填写 client_id**——授权时先 `POST {mcp}/oauth/register` 动态注册公钥客户端，再打开 `mcp-tt4b-authorize` → 本机回调 → **JSON 换票**；也可手动填固定 client_id；flat/layer 分存 token
- **MCP**：`Authorization: Bearer` + JSON-RPC + SSE；只读白名单（对齐 AIEcomPlex 工具目录），写工具硬拒
- **对话 / 同步 / 表格·看板**：均在桌面壳内完成
- **定时自动调度**：支持「每 N 分钟」或「每日定点（本地时区）」后台拉数；同步页可一键开/关并显示下次执行时间
- **表格 / 看板**：按 Open MCP 工具入库展示（广告主 / 系列 / 组 / 广告 / 综合报表）；含树状结构图、目标与系列饼图、趋势折线、效果排行表。字段说明见 [`docs/tiktok-mcp-data.md`](docs/tiktok-mcp-data.md)

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

## Open MCP 鉴权契约（对齐 AIEcomPlex）

| 步骤 | 做法 |
|------|------|
| 客户端 | 默认 **DCR**：`POST {mcp}/oauth/register`（`token_endpoint_auth_method=none`）；可选固定 client_id |
| 授权页 | `.../portal/mcp-tt4b-authorize` + `client_id`/`app_id`/`resource`/`code_challenge` |
| 换票 | `POST {mcp}/oauth/token`，**JSON body**：`grant_type,client_id,code,redirect_uri,code_verifier,resource` |
| 刷新 | JSON：`grant_type=refresh_token` + `client_id` + `refresh_token` + `resource` |
| MCP | `POST {mcpUrl}` + `Authorization: Bearer` + JSON-RPC |
| 默认拒写 | 如 `campaign_create` / `*_update` 等不可经对话或拉数调用 |

## 参考

- AIEcomPlex `aie-tiktok`：`TikTokAdsOAuthService` / `TikTokAdsMcpClient`（协议参考）  
- TikTok for Business Open MCP 官方文档  
- 表格/看板交互参考 ZhiQu 形态，自研瘦 UI，不复用 SaaS 后端
