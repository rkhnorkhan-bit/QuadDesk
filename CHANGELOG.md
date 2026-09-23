# Changelog

## 0.1.6

- Smart Snap внутри каждой родительской зоны: левая/правая половина, четверти и локальный maximize;
- перенос типовых Windows Snap-геометрий с уровня монитора внутрь активной зоны;
- свободный resize окна разрешён внутри зоны, но окно не может выйти за её максимальные границы;
- несколько окон могут находиться в одной зоне независимо друг от друга;
- overlay показывает локальную цель Smart Snap;
- состояние Smart Snap сохраняется в workspace;
- добавлен отдельный рубильник Smart Snap в настройках;
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
