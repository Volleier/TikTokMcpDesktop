# TikTok for Business Open MCP — 可读数据说明

依据官方文档
[Available tools in TikTok for Business MCP Server](https://business-api.tiktok.com/portal/docs/available-tools-in-tiktok-for-business-mcp-server/v1.3)
（约 400 个工具）。本桌面端只读白名单优先拉取下列工具，并映射到表格 / 看板。

## 本应用重点展示的只读工具

| MCP 工具 | API | 看板用途 |
| --- | --- | --- |
| `auth_advertiser_get` | `/v1.3/oauth2/advertiser/get/` | 授权广告主列表 |
| `advertiser_info_get` | `/v1.3/advertiser/info/` | 广告主资料、余额、行业 |
| `campaign_get` / `smart_plus_campaign_get` | `/v1.3/campaign/get/` 等 | 系列树、目标、预算、状态 |
| `adgroup_get` / `smart_plus_adgroup_get` | `/v1.3/adgroup/get/` 等 | 组级结构 |
| `ad_get` / `smart_plus_ad_get` | `/v1.3/ad/get/` 等 | 广告 / 版式分布 |
| `report_integrated_get` | `/v1.3/report/integrated/get/` | 消耗、展示、点击、转化、CTR/CPC/CPA |

其它可读类（`*_get` / `*_list` / `report_*`）在探测到后也可入库；写操作（create/update/status）硬拒。

## 看板预设

1. **广告概览** — KPI + 目标/系列饼图 + 每日消耗折线 + 综合报表表  
2. **投放结构** — 广告主→系列→组→广告 **树状图** + 状态柱状图 + 版式饼图  
3. **效果分析** — CTR/CPC/CPA、系列对比柱状、广告主消耗饼图、效果排行表  
4. **账户与数据表** — 本地入库表统计  

离线 DevHost 会写入与上述工具同构的演示样本，便于无 TT4B 授权时预览 UI。
