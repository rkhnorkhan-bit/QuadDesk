# Contributing

QuadDesk is a small Windows desktop project. Contributions are welcome when they keep the product simple, predictable and safe for everyday desktop use.

## Project direction

QuadDesk should remain:

- focused on one physical display at a time;
- free from virtual display drivers;
- free from screen capture pipelines;
- usable without administrator privileges for normal windows;
- conservative around system, shell, elevated, fullscreen and protected windows;
- easy to install, uninstall and disable.

Changes that add hidden background services, telemetry, driver dependencies, paid network features or broad system modifications are not aligned with the current direction.

## Before opening a pull request

Use a separate branch for each change:

```text
feature/short-name
fix/short-name
build/short-name
docs/short-name
```

Before submitting:

1. Build the solution.
2. Run the tests.
3. Do not commit local config, logs, artifacts, credentials or machine-specific files.
4. Keep the pull request focused on one change.
5. Explain the user-visible behavior, not only the implementation details.
6. Mention known limitations and manual test coverage.

## Development commands

Regular build:

```powershell
pwsh -ExecutionPolicy Bypass -File .\build.ps1
```

Full release package:

```powershell
pwsh -ExecutionPolicy Bypass -File .\publish.ps1
```

The full package requires Inno Setup 6.

## Pull request expectations

A pull request should include:

- a clear title;
- a short reason for the change;
- screenshots or a short video for visible UI changes when useful;
- updated tests for layout, hotkey or geometry logic;
- documentation updates when behavior changes.

Avoid large mixed pull requests. Window-management bugs are easier to review when geometry, UI and build-system changes are not bundled together.

## Window-management changes

Be careful with code that moves, resizes, hooks or filters windows.

Do not manage:

- secure desktop and UAC prompts;
- shell surfaces such as taskbar and desktop workers;
- hidden, cloaked, owned or tool windows;
- fullscreen games and protected media surfaces;
- windows that the user explicitly excluded.

Prefer explicit diagnostics over guesses. If a window is ignored, the code should make it possible to understand why.

## Licensing of contributions

By submitting a pull request, patch, file or other contribution to this repository, you confirm that you have the right to submit it.

You keep your copyright in your contribution.

You also grant the QuadDesk project owner a perpetual, worldwide, non-exclusive, royalty-free license to use, modify, distribute, sublicense and relicense your contribution as part of QuadDesk. This allows the project to keep a consistent source-available license and to offer separate commercial licenses when needed.

Your contribution remains available in the public repository under the license used by the QuadDesk release that includes it.

## Security issues

Do not publish exploitable security issues as public GitHub issues.

Open a minimal private report to the maintainer first, with enough detail to reproduce the problem. Public disclosure can happen after there is a reasonable fix or mitigation path.
