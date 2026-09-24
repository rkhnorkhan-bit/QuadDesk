# Changelog

## 0.1.8

- добавлена схема `schemaVersion: 2` для будущих индивидуальных настроек каждого дисплея;
- добавлена модель `DisplayProfile` с настройками layout, Smart Snap, Strict, Win+Arrow, Windows Snap suppression и guard-параметрами;
- добавлены поля `activeDisplayProfileKey` и `displayProfiles` в конфиг;
- добавлена безопасная миграция config 0.1.7 → 0.1.8 с резервной копией `config.json.backup-before-0.1.8.*.bak`;
- сохранено старое поведение карантина для повреждённых JSON;
- обновлён `config.default.json` под новую схему;
- добавлена документация `docs/DISPLAY_PROFILES.md`;
- это foundation-патч: текущая рабочая модель одного активного дисплея сохранена, multi-display routing будет включаться следующим этапом.

## 0.1.7

- добавлен встроенный пункт **Обновления…** в главное окно и tray;
- обновления берутся только из stable GitHub Releases;
- pull-request artifacts, draft/pre-release сборки и preview-сборки не используются для обычного обновления;
- скачиваются setup EXE и `SHA256SUMS.txt`;
- setup EXE обязательно проверяется по SHA-256 перед запуском;
- добавлен отдельный `QuadDesk.Updater.exe`;
- QuadDesk копирует updater во временную папку, закрывается, после чего updater запускает установщик и открывает новую версию;
- схема рассчитана на будущие обновления без ручного удаления или полной переустановки;
- updater входит в portable ZIP и Windows installer;
- добавлена документация `docs/UPDATER.md`.

## 0.1.6

- Smart Snap внутри каждой родительской зоны: левая/правая половина, четверти и локальный maximize;
- перенос типовых Windows Snap-геометрий с уровня монитора внутрь активной зоны;
- свободный resize окна разрешён внутри зоны, но окно не может выйти за её максимальные границы;
- несколько окон могут находиться в одной зоне независимо друг от друга;
- overlay показывает локальную цель Smart Snap;
- состояние Smart Snap сохраняется в workspace;
- добавлен отдельный рубильник Smart Snap в настройках;
- добавлен экспериментальный Strict Submonitors без изменения поведения по умолчанию;
- Strict Guard удерживает управляемые окна внутри выбранного подмонитора через отдельный watchdog;
- добавлен локальный перехват Win+Arrow на выбранном мониторе, включается отдельно;
- улучшена поддержка Windows Terminal / cmd / powershell / pwsh / conhost;
- Strict Submonitors и Win+Arrow Capture отключены по умолчанию, чтобы не повредить текущий рабочий сценарий;
- добавлены unit-тесты геометрии, определения edge/corner и отрицательных координат.

## 0.1.5

- единый источник версии в `Directory.Build.props`;
- стабильная multi-size иконка `QuadDesk.ico`, встроенная в EXE;
- иконка приложения используется главным окном и tray;
- `publish.ps1` собирает portable ZIP и Windows installer;
- Inno Setup installer ставит QuadDesk per-user в `%LOCALAPPDATA%\Programs\QuadDesk`;
- SHA-256 для release-артефактов;
- GitHub Actions build/test/package на push и PR;
- автоматический GitHub Release по тегу `v*`;
- build outputs исключены через `.gitignore`.

## 0.1.4

- настраиваемые глобальные горячие клавиши;
- диагностика конфликтов RegisterHotKey;
- исправления управления зонами и portable update.

### Build compatibility hotfix
- release PowerShell scripts are ASCII-safe for Windows PowerShell 5.1 and PowerShell 7;
- source archive no longer contains local `config.json`;
- asset verification falls back to `config.default.json` in a clean clone;
- release output directory is cleaned before packaging to prevent stale artifacts.