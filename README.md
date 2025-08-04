# Viewer - Markdown File Viewer

A small utility born out of personal need: many Markdown editors have live previews, but few standalone viewers make opening and printing `.md` files straightforward. Viewer is a lightweight WPF application that renders GitHub-Flavored Markdown in real time using the official GitHub API—built to solve that annoyance, and happy if it helps someone else.

## Features

- **GitHub-Flavored Markdown Rendering:** Renders Markdown files using GitHub's official API for authentic formatting.
- **Modern Material Design UI:** Clean, intuitive interface with Material Design elements.
- **Drag & Drop Support:** Simply drag Markdown files onto the application to view them.
- **Command Line Integration:** Open files directly from command line or file associations.
- **Print Functionality:** Print rendered Markdown documents directly from the viewer.
- **Real-time Loading Indicators:** Visual feedback during file processing.
- **Error Handling:** Graceful fallback to plain text when GitHub API is unavailable.
- **Multi-language Support:** Localized interface (Italian and English included).

## Technical Stack

- **.NET Framework 4.8:** Robust, mature framework for Windows applications.
- **WPF (Windows Presentation Foundation):** Rich UI framework with XAML markup.
- **WebView2:** Modern web engine for rendering HTML content.
- **GitHub Markdown API:** Official GitHub API for authentic Markdown rendering.

## System Requirements

- Windows 10 or later  
- .NET Framework 4.8  
- Internet connection (for GitHub API rendering)  
- WebView2 Runtime (usually pre-installed on Windows 11)

## Usage

1. **Open Files:** Click the folder icon or use the File menu to browse for Markdown files.  
2. **Drag & Drop:** Drag `.md` files directly onto the application window.  
3. **Command Line:** Associate `.md` files with the application or pass file path as argument.  
4. **Print:** Use the print button to send rendered content to printer.  
5. **Reload:** Refresh content if the source file has been modified.

## GitHub API Usage Notice

This application uses GitHub's public Markdown API for rendering. Please be aware of the following:

### Rate Limiting

- GitHub API has rate limits (typically 60 requests per hour for unauthenticated requests).  
- Heavy usage may temporarily restrict access.  
- The application will fallback to plain text display when limits are reached.

### Privacy Considerations

- Markdown content is sent to GitHub's servers for processing.  
- Do not use this application with sensitive, confidential, or proprietary content.  
- GitHub's privacy policy and terms of service apply to API usage.

### Network Requirements

- Internet connection required for full Markdown rendering.  
- Application works offline but shows plain text without formatting.  
- Firewall/proxy configurations may affect GitHub API access.

## Alternatives for Sensitive Content

For sensitive documents, consider:

- Using offline Markdown processors.  
- Setting up local Markdown rendering services.  
- Using the plain text fallback mode.
