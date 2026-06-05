# QuickText 📝💎

QuickText is a lightweight, high-performance, standalone Windows notepad application styled after the **Liquid Glass Design System** (Obsidian & Slate-Purple Aesthetics). It provides an always-on-top, instant-start scratchpad for temporary notes and copy-paste tasks.

## Features
- **Always on Top**: Keeps your note container pinned in front of other windows natively.
- **Liquid Glass Aesthetics**: Modern dark mode with desaturated slate-purple accents, frosted glass background, rounded window corners, and custom thin scrollbars.
- **Instant Launch**: Starts in under 150ms using a borderless native WPF shell and WebView2 (Chromium).
- **Tab Key Support**: Inserts actual tab characters (`\t`) inside the text field instead of shifting focus.
- **Copy Button**: A single-click Copy button with immediate visual feedback.
- **Reactive Link Extraction**: Automatically parses text for URLs and renders them as clickable pills at the bottom, opening them instantly in your default system browser.
- **Smooth Window Dragging**: Drag the window around by clicking and dragging anywhere on the glass background.
- **Dynamic Resize**: Automatically scales window height dynamically to fit your content.
- **Zero Configuration & Standalone**: Compiles into a single standalone `QuickText.exe` with no local file dependencies.

## Installation / Build
To compile the standalone executable:
1. Open PowerShell in this directory.
2. Run:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
   ```
3. Copy the compiled `QuickText.exe` from `bin/Release/net9.0-windows/win-x64/publish/` to your Desktop.

## License
Licensed under the [MIT License](LICENSE).
