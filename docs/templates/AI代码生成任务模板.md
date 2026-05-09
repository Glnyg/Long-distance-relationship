# AI代码生成任务模板

## 任务目标

用一句话说明这次要生成或修改什么。

## 必读文档

- `docs/00-AI-Project-Handbook.md`
- `docs/engineering-standards/代码生成总规则.md`
- `docs/engineering-standards/AI隐私工程规范.md`
- 相关功能文档：

## 涉及数据

本次是否涉及以下数据：

- 聊天：是 / 否。
- 定位轨迹：是 / 否。
- 用机状态：是 / 否。
- 音视频：是 / 否。
- 账号身份：是 / 否。
- 推送 token：是 / 否。

## 授权规则

说明本次功能需要检查哪些授权，以及未授权时如何降级。

## 日志与可观测性

需要记录的元数据：

- `operation`：
- `correlation_id`：
- `result`：
- `error_code`：
- `duration_ms`：

禁止写入日志的数据：

- 聊天原文。
- 完整经纬度。
- Wi-Fi 名称。
- 手机号。
- 验证码。
- token。
- prompt。

## 测试要求

必须补充或确认的测试：

- 成功路径：
- 未授权路径：
- 解绑路径：
- 隐私日志路径：
- 失败降级路径：

## 验收命令

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```
