# AIAPP

异地恋 App 项目工作区。

## 目录结构

- `src/`：应用源码。
- `tests/`：自动化测试。
- `docs/`：Obsidian 项目文档，中文为主，是唯一事实源。
- `scripts/`：本地开发和维护脚本。
- `config/`：配置模板和默认配置。
- `data/raw/`：本地原始数据，默认不入库。
- `data/processed/`：本地处理后数据，默认不入库。

## 开始使用

构建和测试当前 .NET AI 隐私网关：

```powershell
dotnet build AIAPP.slnx -c Release -warnaserror
dotnet test AIAPP.slnx -c Release --no-build
powershell -ExecutionPolicy Bypass -File scripts/Assert-DocsGate.ps1
```

修改任何产品行为前，必须先阅读 `docs/00-AI-Project-Handbook.md`。项目文档是唯一事实源，代码必须服从文档。
