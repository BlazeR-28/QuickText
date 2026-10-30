# Changelog

All notable changes to QuickText are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [2.0.1] - 2026-10-30

### Added
- `QUICKTEXT_SPELLCHECK_LANGUAGE` environment variable to spell-check in another language than the Windows display language.

### Changed
- README rewritten with screenshots; repository documentation updated.

## [2.0.0] - 2026-10-29

A complete rewrite in plain WPF. The embedded browser is gone.

### Added
- Tray icon, default global shortcut `Ctrl+Shift+Space`, `Esc` hides the window.
- Optional "Start with Windows" (quietly in the tray).
- Single instance: starting QuickText again brings the running window forward.
- Window size and position are remembered; the window grows with the text.
- Dark right-click menu with spelling suggestions.
- `QUICKTEXT_DATA_DIR` environment variable for portable use.
- Self-contained single-file release that runs without installing .NET.
- GitHub Actions for build and release.

### Changed
- New graphite and amber look and a new icon.
- Memory use drops from about 310 MB to about 73 MB, and to roughly 20-30 MB while hidden.
- Settings are reduced to autosave, start with Windows, opacity and shortcuts. Window size is set by dragging the edges.
- Targets .NET 10.

### Fixed
- Clear can be undone with `Ctrl+Z`.
- Shortcut registration errors are shown instead of failing silently.
- Shortcuts that would block typing (a bare letter, `Ctrl+Alt+letter`) are refused.
- Notes and settings are written atomically; crashes are logged to `crash.log`.
- Links open only as http or https.

## [1.1.0] - 2026-06-08

### Added
- Configurable window size limits in the settings panel.

## [1.0.0] - 2026-06-06

First release: WPF shell with a WebView2 interface, autosave, link pills, configurable shortcuts and window opacity.

[Unreleased]: https://github.com/BlazeR-28/QuickText/compare/v2.0.1...HEAD
[2.0.1]: https://github.com/BlazeR-28/QuickText/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/BlazeR-28/QuickText/releases/tag/v2.0.0
[1.1.0]: https://github.com/BlazeR-28/QuickText/releases/tag/v1.1.0
[1.0.0]: https://github.com/BlazeR-28/QuickText/releases/tag/v1.0.0
