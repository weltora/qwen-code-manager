# Qwen Code Manager — Installer

## Технология

**Inno Setup 6** — единственный installer проекта. Выбран потому что:

- приложение — unpackaged WinUI 3 (`WindowsPackageType=None`); MSIX потребовал бы
  sideload-сертификат и Windows SDK tooling, которых нет в окружении;
- Inno даёт классический `.exe`, нормальную регистрацию в Settings → Apps,
  upgrade поверх старой версии и удаление без сертификата;
- один EXE, без внешних зависимостей у пользователя: publish self-contained
  (.NET 8 runtime и Windows App SDK включены в поставку).

## Сборка

```powershell
powershell -ExecutionPolicy Bypass -File build-installer.ps1
# или с явной версией:
powershell -ExecutionPolicy Bypass -File build-installer.ps1 -Version 1.0.1
# без повторного publish (если artifacts\publish уже готов):
powershell -ExecutionPolicy Bypass -File build-installer.ps1 -SkipPublish
```

Скрипт выполняет: publish Release x64 (self-contained) → компиляция
`installer\QwenCodeManager.iss` → SHA256 → `version.json`.

Требуется: .NET SDK 8 (пинован `global.json`) и Inno Setup 6 (`ISCC.exe` в PATH
или в стандартных местах установки; скрипт ищет автоматически).

## Результат

```
artifacts\
    publish\win-x64\                  self-contained приложение
    installer\
        QwenCodeManager-Setup-x64.exe
        QwenCodeManager-Setup-x64.exe.sha256
        version.json
```

## Версия

Единый источник — `Directory.Build.props` (`<Version>`). Installer и сборки
получают версию оттуда; параметр `-Version` переопределяет её для разовой сборки.
`AppId=QwenCodeManager` в `.iss` — стабильный идентификатор, менять нельзя
(от него зависит upgrade).

## Установка / удаление / обновление

- Установка: запустить `QwenCodeManager-Setup-x64.exe` (нужны права администратора).
  Тихая: `... /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
- Каталог: `C:\Program Files\Qwen Code Manager`.
- Ярлык: меню Пуск; ярлык рабочего стола — опциональная галочка (по умолчанию выключена).
- Удаление: Settings → Apps → Qwen Code Manager → Uninstall.
- Обновление: запустить installer новой версии поверх — файлы обновляются,
  дубликаты не создаются.

## User data (никогда не трогается установщиком)

`%LOCALAPPDATA%\QwenCodeManager\` — config, projects.json, covers, cache, logs.
При удалении приложения пользовательские данные и проекты сохраняются.
Qwen Code, его настройки, ключи и Skills в installer не входят.

## Подпись

Сертификата Code Signing в окружении нет — installer не подписан.
**Для production release рекомендуется подписать installer сертификатом Code Signing.**
Без подписи Windows SmartScreen покажет предупреждение — это ожидаемое
ограничение development build, обходить его не нужно.

Подпись (когда сертификат появится):

```powershell
signtool sign /fd SHA256 /a artifacts\installer\QwenCodeManager-Setup-x64.exe
```

Приватные ключи не хранить в репозитории.

## Проверено локально

Чистая установка → запуск → Пуск-ярлык → user data создана → upgrade 1.0.0→1.0.1
(версия в Apps обновилась, данные сохранены) → удаление (файлы приложения удалены,
регистрация снята, user data цела). Полноценная чистая VM не использовалась —
тест выполнен на рабочей машине поверх существующего user data.
