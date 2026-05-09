---
status: accepted
date: 2026-05-09
---

# 0004 P0 后端基础采用 DDD/CQRS 三服务

## 背景

P0 需要先支撑账号登录、情侣绑定和授权隐私中心。它们是聊天、定位、设备状态和 AI 分析的前置依赖，并且都涉及敏感身份、绑定关系和授权审计。

## 决策

首版实现三个 ASP.NET Core Minimal API 服务：`IdentityService`、`CoupleService`、`ConsentService`。每个服务内部按 DDD、CQRS 和充血模型组织，命令和查询使用 MediatR `12.5.0`。PostgreSQL 使用单库分 schema：`identity`、`couple`、`consent`。

所有服务必须接入 `AIAPP.Observability`、`correlation_id`、健康检查和本地审计表。错误响应统一使用 `ProblemDetails`，扩展 `error_code`、中文 `message` 和 `correlation_id`。

## 后果

优点：

- 三个边界适合多个 AI 并行实现，减少文件冲突。
- 单库分 schema 便于本地部署，又保留服务边界。
- 本地审计表能满足 P0 写操作可审计，后续可以迁移到独立 `AuditService`。

代价：

- 项目数量和测试数量增加。
- 需要维护共享后端基础库，避免错误响应、鉴权和审计重复实现。

## 隐私约束

不得在日志、Trace、Metric、审计或数据库保存手机号原文、验证码、access token、refresh token、聊天原文、精确定位、Wi-Fi 名称、prompt 或模型输入快照。真实短信、微信和云厂商 Secret 不进入仓库。
