# Release checklist — Qwen Code Manager

1. Обновить `<Version>` в `Directory.Build.props` (semver: 1.0.0 → 1.0.1 → 1.1.0 …).
2. Прогнать тесты: `dotnet test -c Release`.
3. Собрать installer: `powershell -ExecutionPolicy Bypass -File build-installer.ps1`.
4. Проверить `artifacts\installer\version.json` и `.sha256`.
5. Smoke-тест: тихая установка поверх предыдущей версии, запуск, удаление
   (user data должна пережить uninstall).
6. Для production: подписать installer сертификатом Code Signing (см. INSTALLER.md),
   пересчитать SHA256 после подписи.
7. Опубликовать `QwenCodeManager-Setup-x64.exe` + `.sha256`.
8. Обновить `.qwen-manager/checkpoint.md`.

Правила:
- `AppId` в `installer\QwenCodeManager.iss` не менять — иначе upgrade сломается.
- Не включать в поставку Qwen Code, ключи, `.env`, user data.
- Артефакты только в `artifacts\`, не в `bin`/`obj`.
