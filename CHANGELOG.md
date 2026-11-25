# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-11-24

### Added

- Detect installed programs through Winget, with cached results so the window opens instantly.
- Create a readable JSON backup of the selected programs in the Downloads folder.
- Restore a backup and reinstall everything with per-program progress and status.
- Single administrator prompt per installation, even for a single program.
- Settings tab with three options enabled by default: install without confirmation, single
  administrator permission, and automatic acceptance of Winget terms.
- Light and dark themes, saved between sessions.
- Search, select-all, system items toggle and reliable mouse-wheel scrolling.
- A single downloadable file, `ReInstala.exe`: a per-user installer with the whole app embedded,
  which creates a desktop shortcut and a Start Menu entry and registers itself in *Apps & features*
  for a clean uninstall.
- Multi-size application icon (16 to 256 px) embedded in both executables.
- Command-line modes: `--backup`, `--selftest`, installer `--quiet` and `--uninstall`.
- GitHub Actions workflows to build on every push and publish release assets on version tags.

[1.0.0]: releases/tag/v1.0.0
