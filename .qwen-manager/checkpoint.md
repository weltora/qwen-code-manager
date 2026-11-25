# Current checkpoint

Project:
Qwen Code Manager

Last updated:
2026-11-25

Current task:
Windows installer готов: Inno Setup 6, QwenCodeManager-Setup-x64.exe в artifacts\installer.

Completed:
- Master-detail layout: список проектов слева (иконка типа, имя, путь, последняя сессия, статус-точка, amber-выделение), детальная панель справа
- Детальная панель: шапка с обложкой, 4 стат-карточки (последний запуск / длительность / статус / память Qwen), amber-баннер «Найдена сессия для продолжения», 3 кнопки действий с подписями команд, история сессий со статус-пилюлями и меню
- Sidebar: Проекты / Сессии / Память / Настройки / О программе + карточка статуса Qwen Code и версия; градиентная подсветка активного пункта
- Шапка окна: логотип, название, подзаголовок; нижняя панель: Qwen Code / Сессии / Память
- Новые страницы: Память (QWEN_HOME, QWEN.md, Skills, Memories — read-only), О программе
- Полная русификация UI
- Liveness сессии по pid из runtime.json (Активна / Записана / Только метаданные — честно, без выдуманных «завершено»)
- Фикс краша XamlCompiler: x:Bind свойства типа Uri к Image.Source роняет компилятор молча (exit 1 без ошибок); заменено на BitmapImage
- Debug + Release builds: 0 ошибок; 51/51 тестов; приложение запущено и проверено по логу

Current:
- Installer 1.0.0 собран и протестирован (install / upgrade 1.0.0→1.0.1 / uninstall, user data цела)
- Единый источник версии: Directory.Build.props; иконка Assets\app.ico (tools\make-icon.ps1)
- build-installer.ps1: publish self-contained → ISCC → SHA256 → version.json
- INSTALLER.md, RELEASE.md, .gitignore созданы; 51/51 тестов

Next:
- Подпись Code Signing для production (сертификата в окружении нет)
- Возможная полировка UI: clip обложки, адаптив узкого окна, tray (отложен по ТЗ)

Known issues:
- Статус «Записана» вместо «Завершено»: Qwen не хранит флаг завершения сессии; «Активна» определяется по живому pid (возможна переиспользованность pid)
- Длительность сессии приблизительная (started_at → mtime транскрипта)
- Обложка в шапке детали не обрезается по CornerRadius (WinUI Border без clip) — косметика
