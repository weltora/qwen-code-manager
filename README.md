# Qwen Code Manager

Dark Windows desktop launcher/manager for Qwen Code projects.
It orchestrates the real Qwen Code CLI — it is not an IDE, terminal, Git client or AI agent.

## What it does

- Project cards: name, type, path, cover, status, Git summary, last Qwen session, last activity
- **Continue** (`qwen -c`), **New Session** (`qwen`), resume a specific session (`qwen -r <id>`)
- Sessions screen built read-only from `%QWEN_HOME%\projects\<slug>\chats\` (verified against Qwen Code 0.28.x)
- Read-only Git status (`git status --porcelain=v1 --branch`, `remote get-url origin`)
- Open folder / terminal (Windows Terminal preferred) / GitHub
- Project checkpoint: plain `.qwen-manager/checkpoint.md`, created only on explicit request
- Offline-first: everything except GitHub cover lookup works without internet
- JSON persistence at `%LOCALAPPDATA%\QwenCodeManager\` (settings, projects, covers, logs)
- Never modifies user projects, Qwen config, `.env`, keys or skills

## Layout

```
src/QwenCodeManager.Core      UI-independent services + models (net8.0-windows)
src/QwenCodeManager.App       WinUI 3 / Windows App SDK 1.7, MVVM (CommunityToolkit.Mvvm)
tests/QwenCodeManager.Core.Tests   xUnit, 51 tests
```

## Build & run

Requires .NET SDK 8 (pinned via `global.json`). Windows SDK comes from NuGet
(`Microsoft.Windows.SDK.BuildTools`), no Visual Studio workload needed.

```
dotnet build -c Release
dotnet test
```

Run:
`src\QwenCodeManager.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\QwenCodeManager.exe`

Unpackaged, self-contained Windows App SDK — no MSIX install required.

## Qwen integration facts (verified locally, 0.28.4)

- Executable resolution honors PATHEXT and skips the extensionless POSIX `qwen` sh shim;
  batch files run through `cmd.exe /d /k ""<path>\qwen.cmd" <args>"`.
- `QWEN_HOME` is inherited unchanged (user value: `D:\QwenCode\home`).
- Project slug: absolute path lowercased, `:`/`\`/`/` → `-` (e.g. `c--users-x-desktop-app`).
- Session files: `<id>.jsonl` (transcript), `<id>.runtime.json` (snake_case: `started_at`, `work_dir`, `qwen_version`), `<id>.title.json` (`displayName`).
