# MDViewer

MDViewer is a small Windows desktop utility for technical workspaces full of local Markdown files: README files, notes, release docs, setup guides, and project documentation.

It opens Markdown quickly, renders it in a WebView2 preview, and makes printing or exporting to PDF straightforward without moving the file into an editor or a browser tab.

## Why

Markdown files often live next to the code they describe. MDViewer exists for the moments when you just want to read, print, or export one of those files without opening a full editor, switching preview tabs, or copying content elsewhere.

## Features

- Local Markdown rendering with Markdig by default
- Optional GitHub-Flavored Markdown rendering through the GitHub API
- Print and PDF export
- Recent files
- Auto reload when the current file changes
- Theme and settings support
- Italian/English interface with Auto, Italian, and English language preference
- WebView2 based preview

## Rendering Modes

MDViewer uses **Markdig** as the default renderer. Rendering happens locally, works offline, and keeps Markdown content on your machine.

For documents that need closer GitHub-Flavored Markdown compatibility, MDViewer can also render through the **GitHub Markdown API**. This mode is optional and requires an internet connection.

## Requirements

- Windows
- .NET Framework 4.8
- Microsoft Edge WebView2 Runtime

## Download / Build

Download the installer or portable ZIP from the [GitHub Releases](https://github.com/n-car/MDViewer/releases) page.

To build from source:

1. Open `MDViewer.sln` in Visual Studio.
2. Restore NuGet packages.
3. Build the `MDViewer` project.

## Privacy Note

Local Markdig rendering stays local.

GitHub API rendering sends the Markdown content to GitHub for processing. Do not use that mode for sensitive, private, or proprietary documents unless that is acceptable for your workflow.
