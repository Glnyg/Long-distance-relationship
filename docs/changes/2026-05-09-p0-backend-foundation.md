---
date: 2026-05-09
type: feature
---

# 2026-05-09 P0 后端基础

## 变更内容

- 新增 P0 后端基础实施决策：`IdentityService`、`CoupleService`、`ConsentService`。
- 固定 DDD、CQRS、充血模型、MediatR `12.5.0` 和 PostgreSQL 单库分 schema。
- 明确账号、情侣绑定、授权中心的 P0 API、审计元数据和隐私日志边界。
- 补充三个服务的 Kubernetes 部署清单要求。

## 原因

账号、情侣绑定和授权中心是后续聊天、定位、设备状态和 AI 分析的基础。先落地这些服务，可以让后续功能都依赖稳定的身份、绑定关系和授权版本。

## 影响

- 服务端新增三个 ASP.NET Core Minimal API 服务和一个共享后端基础库。
- 测试需要覆盖领域规则、API、PostgreSQL schema、审计和可观测性。
- 部署新增三个业务服务的 `Deployment`、`Service` 和 Secret 示例，但真实 Secret 仍由环境注入。
