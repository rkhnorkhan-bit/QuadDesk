# QuadDesk

[English](README.md) · [Русский](README.ru.md)

QuadDesk is a Windows desktop utility for turning one physical display into a set of practical working zones.

It does **not** create virtual monitors, install display drivers, mirror the screen, capture video, or depend on OBS/VDD-style routing. Windows still sees one monitor. QuadDesk manages ordinary application windows on top of that monitor.

The target use case is simple: a large TV or ultrawide display should behave like several clean work areas without the visual confusion of fake monitors.

## Current status

The stable public release is available from **GitHub Releases**.

Development work happens in short-lived branches and pull requests. A branch may contain experimental behavior until it is merged and tagged as a release.

## What QuadDesk does

- splits a selected physical display into configurable zones;
- keeps the primary monitor untouched unless explicitly selected;
- moves the active window into a selected zone;
- supports custom layouts and editable zone boundaries;
- supports hotkeys for layouts, zones, movement and restore;
- provides tray controls for quick operation;
- saves and restores a workspace snapshot;
- checks stable GitHub Releases for updates;
- builds a self-contained Windows installer and portable package through GitHub Actions.

## What QuadDesk intentionally avoids

- no virtual display driver;
- no fake multi-monitor topology;
- no screen capture pipeline;
- no browser runtime;
- no telemetry;
- no background network dependency for normal window management.

## Layouts

Built-in layouts include:

| Layout | Description |
|---|---|
| Single | One full-display zone |
| Dual Vertical | Two columns |
| Dual Horizontal | Two rows |
| Triple Columns | Three columns |
| Triple Main | Large main area plus two secondary areas |
| Quad 2x2 | Four equal zones |
| Main + 3 | Large left area plus three stacked right areas |
| Six 3x2 | Three columns by two rows |

Custom layouts can be edited in the app. Zones can be split horizontally or vertically, renamed, resized and saved as separate layouts.

## Basic usage

1. Install QuadDesk or unpack the portable build.
2. Start `QuadDesk.exe`.
3. Select the display that QuadDesk should manage.
4. Choose a layout.
5. Move windows with the tray menu, hotkeys, auto-snap or the main window controls.

The selected display is the only display managed by QuadDesk. Other monitors remain under normal Windows behavior.

## Default hotkeys

| Hotkey | Action |
|---|---|
| `Ctrl+Alt+0` | Enable or disable QuadDesk |
| `Ctrl+Alt+Shift+1` | Single layout |
| `Ctrl+Alt+Shift+2` | Dual Vertical |
| `Ctrl+Alt+Shift+3` | Triple Main |
| `Ctrl+Alt+Shift+4` | Quad 2x2 |
| `Ctrl+Alt+Shift+5` | Main + 3 |
| `Ctrl+Alt+Shift+6` | Six 3x2 |
| `Ctrl+Alt+1` ... `Ctrl+Alt+9` | Move active window to zone 1 ... 9 |
| `Ctrl+Alt+Arrow` | Move active window to a neighboring zone |
| `Ctrl+Alt+M` | Maximize or restore inside the current zone |
| `Ctrl+Alt+R` | Restore previous window size inside the zone |

Hotkeys can be changed in the app. If Windows or another app already owns a combination, QuadDesk reports the conflict and lets you choose another binding.

## Build from source

Requirements:

- Windows x64;
- .NET SDK matching `global.json`;
- Inno Setup 6 for installer builds.

Install Inno Setup once:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

Build, test and package:

```powershell
pwsh -ExecutionPolicy Bypass -File .\publish.ps1
```

Release artifacts are written to:

```text
artifacts\release\
```

The release builder creates:

- portable ZIP;
- Windows setup EXE;
- SHA-256 checksums.

## Release process

Version numbers are stored in `Directory.Build.props`.

GitHub Actions builds and tests every push and pull request. A tag named `vX.Y.Z` creates a GitHub Release with the installer, portable archive and checksums.

Main branch policy:

```text
main = stable source
feature/* or fix/* = development work
vX.Y.Z tag = public release
```

## Updates

Installed builds include **Обновления…**.

The updater checks stable GitHub Releases only. It does not install pull-request artifacts, drafts or preview builds.

Update flow:

1. QuadDesk finds the latest stable release.
2. QuadDesk downloads the setup EXE and `SHA256SUMS.txt`.
3. QuadDesk verifies the setup EXE SHA-256.
4. QuadDesk copies `QuadDesk.Updater.exe` to a temporary path.
5. QuadDesk exits.
6. The temporary updater runs the installer and starts the updated QuadDesk.

The updater is a separate executable so future releases can replace both `QuadDesk.exe` and `QuadDesk.Updater.exe` without requiring a manual uninstall/reinstall.

## Data and logs

QuadDesk keeps local files next to the executable:

```text
config.json
layouts\
workspaces\
logs\quad-desk.log
```

No window titles, URLs, clipboard contents or keyboard input are written to logs. Logs are for application diagnostics only.

## Known limits

- Elevated/admin windows may not be controllable from a non-elevated QuadDesk process.
- Fullscreen games, borderless exclusive surfaces and protected media windows are not a reliable target for zone management.
- Some applications enforce their own minimum window size or custom frame behavior.
- QuadDesk manages normal desktop windows; it is not a replacement for a real display driver.

## License

QuadDesk is source-available software.

You may use it for personal use and internal business use. You may inspect, modify and fork the code. Commercial distribution, resale, paid bundles, paid hosted services and white-label redistribution require separate written permission from the copyright holder.

See `LICENSE` for the full terms.