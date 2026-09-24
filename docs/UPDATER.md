# QuadDesk updater

QuadDesk v0.1.7 introduces a built-in stable updater.

The updater is designed to avoid manual uninstall/reinstall cycles after future patches. A normal installed copy of QuadDesk should be able to move from one stable release to the next through **Обновления…**.

## Channels

### Stable

Stable updates are read only from GitHub Releases:

```text
https://api.github.com/repos/rkhnorkhan-bit/QuadDesk/releases/latest
```

The app ignores pull request artifacts for normal updates. PR artifacts are test builds only.

Required stable release assets:

```text
QuadDesk-vX.Y.Z-win-x64-setup.exe
QuadDesk-vX.Y.Z-win-x64-portable.zip
SHA256SUMS.txt
```

The updater uses the setup EXE and SHA256SUMS.txt.

### Preview

Preview updates are intentionally not implemented in v0.1.7. Test builds remain manual GitHub Actions downloads.

## Flow

1. User opens **Обновления…**.
2. QuadDesk requests the latest stable GitHub Release.
3. QuadDesk compares the release tag with the installed assembly version.
4. If the installed version is current, the UI reports that no update is needed.
5. If a newer stable version exists, QuadDesk finds the setup EXE and SHA256SUMS.txt assets.
6. QuadDesk downloads both files into `%LOCALAPPDATA%\QuadDesk\updates\<version>`.
7. QuadDesk verifies the setup EXE SHA-256 against SHA256SUMS.txt.
8. QuadDesk copies `QuadDesk.Updater.exe` to a temporary path.
9. QuadDesk starts the temporary updater and exits.
10. The temporary updater waits for QuadDesk to exit, runs the installer silently and starts the updated QuadDesk.

## Why the updater is a separate executable

`QuadDesk.exe` cannot reliably overwrite its own installation while it is still running.

The installed `QuadDesk.Updater.exe` also should not run directly from the installation directory during update, because the installer needs to replace it too.

For that reason QuadDesk copies the updater to `%TEMP%` and runs the temporary copy. This allows future releases to replace both `QuadDesk.exe` and `QuadDesk.Updater.exe` without a manual reinstall.

## Safety rules

- Stable releases only.
- No forced updates.
- No background service.
- No telemetry.
- No PR artifacts as automatic update source.
- SHA-256 verification is mandatory.
- If checksum verification fails, the installer is not launched.
- User configuration, layouts and workspaces stay in the install folder and are not deleted by the update.

## Known limits

- The updater depends on public GitHub Release availability.
- The updater does not yet support private mirrors.
- The updater does not yet verify Authenticode signatures because release signing is not configured.
- Preview-channel automation is intentionally deferred.

## Manual test

1. Install an older stable version.
2. Publish a newer GitHub Release with setup EXE, portable ZIP and SHA256SUMS.txt.
3. Open QuadDesk → **Обновления…**.
4. Check for updates.
5. Start update.
6. Confirm QuadDesk exits, installer runs silently and the new version starts.
7. Confirm `config.json`, `layouts/` and `workspaces/` are preserved.
