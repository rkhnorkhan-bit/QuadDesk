# QuadDesk display profiles

Version 0.1.8 introduces the configuration foundation for display-specific settings.

The goal is to move QuadDesk from a single global monitor setting to a safer model where each physical display can eventually have its own behavior:

- enabled / disabled;
- selected layout;
- Smart Snap;
- Strict Submonitors;
- Win+Arrow capture;
- Windows Snap suppression;
- guard interval;
- minimum zone size;
- full-monitor vs work-area bounds.

## Why this is needed

Windows may expose several screens at once, and televisions can appear as generic display devices. A single global setting is not enough for a product that must leave ordinary monitors untouched while applying strict QuadDesk behavior only to selected screens.

0.1.8 does not yet run independent window managers for several screens at the same time. It stores the schema and migration layer needed for that next step.

## Schema

`config.json` now uses `schemaVersion: 2` and includes:

```json
{
  "activeDisplayProfileKey": null,
  "displayProfiles": []
}
```

Each display profile stores the last known identity and screen geometry, plus the QuadDesk behavior settings for that display.

## Migration

When a 0.1.7 or older config is loaded, QuadDesk upgrades it to schema 2 and writes a backup next to the original file:

```text
config.json.backup-before-0.1.8.<timestamp>.bak
```

If the config is malformed, the old quarantine behavior remains unchanged: the invalid file is moved to `*.bad.*` and safe defaults are loaded.

## Next step

0.1.9 should use these profiles as the routing layer for multi-display behavior and WinSnapAssist:

```text
window -> monitor -> display profile -> QuadDesk mode or native Windows behavior
```
