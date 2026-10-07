# Introduction

Fjordlight Studio 4.2 focuses on **faster exports**, a redesigned *project browser* and a long list of
fixes reported by our users. This document covers what's new, what changed and what you need to
know before upgrading.

![Fjordlight logo](/logo.png)

> **Upgrade note:** projects saved with 4.2 can't be opened in 4.0 or earlier. Back up your
> projects before upgrading.

# What's new

## Batch export, rewritten

The batch exporter has been rewritten from scratch. Exports now run in parallel and resume
automatically after a crash.

- Up to 4× faster on machines with 8 or more cores
- Resumable exports: a crashed export continues where it stopped
- New `--parallel` switch for the command-line exporter
- Progress is reported per file, not per batch

```shell
fjordlight export --project harbour.fjl --format pdf --parallel 8
```

## Project browser

The project browser has a new layout with thumbnails, tags and saved searches.

1. Open the browser with **Ctrl+O**.
2. Type a tag such as `#customer` to filter.
3. Save the search from the *Search* menu.

## Cloud sync

Cloud sync now supports selective sync. You can pick which folders sync to each machine.

| Plan         | Storage | Selective sync | Version history |
|--------------|--------:|:--------------:|-----------------|
| Standard     |  50 GB  |       No       | 30 days         |
| Professional | 200 GB  |      Yes       | 90 days         |
| Enterprise   | 500 GB  |      Yes       | 1 year          |

# Changes

## Behavior changes

- The default export format is now PDF/A-2b instead of plain PDF.
- Autosave runs every 2 minutes instead of every 5.
- The *Legacy renderer* option has been removed. It was deprecated in 4.0.

## Deprecations

The following APIs are deprecated and will be removed in 5.0:

- `Project.ExportAll()` — use `Project.ExportAsync(ExportOptions)` instead.
- `Settings.UseLegacyFonts` — fonts are now always embedded.

# Fixed issues

## Editor

- Fixed a crash when undoing a paste of more than 10 000 objects.
- Fixed incorrect kerning for some OpenType fonts.
- Fixed the zoom level resetting after switching tabs.
- Fixed keyboard focus getting lost after closing a dialog.
- Fixed a memory leak when many large images were open.

## Export

- Fixed PNG export ignoring the configured resolution.
- Fixed SVG export producing invalid output for gradients with a single stop.
- Fixed page numbers restarting in exported multi-section documents.
- Fixed exports failing when the output path contained non-ASCII characters, such as `å`, `ä` or `ö`.

## Sync

- Fixed conflicts being reported for files that were only touched, not changed.
- Fixed sync stalling on networks with a proxy that requires authentication.

# Known issues

- On Windows on ARM, the first export after installation can take up to 30 seconds while
  fonts are cached.
- Selective sync settings aren't migrated from the 4.1 beta. Reconfigure them after upgrading.

# Security

This release fixes one security issue. Projects opened from untrusted sources could reference
files outside the project folder. Project files are now confined to their folder, and external
references are shown as warnings.

---

Thank you to everyone who reported issues during the beta. See the [Fjordlight forum](https://example.com/forum)
for questions and feedback.
