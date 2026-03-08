# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [2.0.0] - 2026-03-08

### Added
- Provider selection dialog with a detailed comparison view (features, trade-offs, and usage notes).
- Toolbar button to choose the active Markdown provider without using a combo box.
- Robust HTML sanitization based on `HtmlAgilityPack` for rendered content.
- Runtime application of saved settings (theme, provider, syntax highlighting, auto-reload, cache limits, recent files limit).
- Persisted and restored main window bounds (size, position, maximized state).
- GitHub Actions release flow that publishes portable ZIP, installer EXE, and SHA256 checksum assets.
- Full application localization coverage for Italian and English (main window, settings, provider dialog, status/error flows, provider metadata).
- Installer localization via Inno Setup `CustomMessages` for Italian and English custom texts.

### Changed
- Update parsing moved from regex-based extraction to typed JSON deserialization in `UpdateManager`.
- Inno Setup script now allows CI to override `MyAppVersion` at build time.
- Rendered HTML now uses stricter CSP handling and safe document title encoding.
- `MainWindow` lifecycle cleanup improved for file watcher and event subscriptions.
- UI culture selection now defaults to Italian on Italian systems and falls back to English on non-Italian systems.
- Installer file-association integration improved with Windows Default Apps capabilities registration (`Capabilities`, `RegisteredApplications`, and shell association refresh).

### Fixed
- Installer script no longer breaks when optional runtime files are missing.
- Markdown file filter strings in localized resources have been corrected.
- Startup crash caused by an incompatible `System.Resources.Extensions` runtime dependency has been fixed for the .NET Framework build.

### Removed
- Legacy local release scripts and committed old release artifacts.
- Legacy `MDViewer.setup` (`.vdproj`) installer project.

## [1.1.0] - 2025-03-07

### Added
- **Syntax Highlighting**: Automatic code highlighting for fenced code blocks using highlight.js.
- **PDF Export**: Direct export of the current document to PDF.
- **Settings Page**: Full settings window to configure application behavior.
- **Recent Files**: Menu with the last opened files.
- **Update Checks**: Manual and automatic checks for new versions from GitHub Releases.
- **Rendering Cache**: Local cache for the GitHub API provider to reduce repeated API calls.
- **Auto-reload**: Automatic reload when the opened file changes on disk.
- **Inno Setup Installer**: New installer replacing the previous `.vdproj` setup project.

### Changed
- Improved theme handling (light/dark/system).
- Updated status bar with version info and quick actions.
- Optimized HTML rendering template.

### Removed
- Legacy installer project `MDViewer.setup` (`.vdproj`).

## [1.0.0] - 2025-02-01

### Added
- Markdown rendering with two providers: **Markdig** (local, fast, offline) and **GitHub API** (online, full GFM rendering).
- Light/dark theme support with automatic Windows theme detection.
- Drag and drop support for opening files.
- Document printing.
- Italian/English localization.
- Automatic WebView2 Runtime installation if missing.
- Modern Material-style UI.

---

## Legend

- **Added**: New features.
- **Changed**: Changes in existing functionality.
- **Deprecated**: Features that will be removed in upcoming versions.
- **Removed**: Features removed in this version.
- **Fixed**: Bug fixes.
- **Security**: Security-related fixes.
