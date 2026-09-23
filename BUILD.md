# QuadDesk — release build

## Требования

- Windows 11 x64
- .NET 10 SDK x64, версия закреплена в `global.json`
- Inno Setup 6

Однократная установка:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

Если PowerShell блокирует локальные `.ps1`, не меняйте системную policy глобально:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

## Полная release-сборка

```powershell
.\publish.ps1
```

Pipeline:

1. restore;
2. build Release;
3. unit tests;
4. self-contained single-file publish `win-x64`;
5. portable ZIP;
6. Inno Setup installer;
7. SHA-256.

Результат:

```text
artifacts\release\
  QuadDesk-vX.Y.Z-win-x64-portable.zip
  QuadDesk-vX.Y.Z-win-x64-setup.exe
  SHA256SUMS.txt
```

## Только portable

```powershell
.\publish.ps1 -SkipInstaller
```

## Версия

Меняется только в `Directory.Build.props`.

## GitHub

Каждый push/PR выполняет Windows CI. Тег `vX.Y.Z` создаёт GitHub Release автоматически.

Перед публикацией релиза вручную пройдите `docs/SMOKE_TEST.md`.
