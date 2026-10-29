# QuickText

[![Framework](https://img.shields.io/badge/.NET-9.0--windows-E2AD3B.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-555555.svg?style=flat-square)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-555555.svg?style=flat-square)](LICENSE)

[![Download QuickText](https://img.shields.io/badge/Download-Latest%20Release-E2AD3B.svg?style=for-the-badge&logo=github&logoColor=1B1A18&labelColor=1B1A18)](https://github.com/BlazeR-28/QuickText/releases/latest)

A small scratchpad for Windows. Press a shortcut, jot something down, copy it, press the shortcut again. QuickText stays in the background, uses little memory and is built for short, temporary notes: collecting text from several websites, rewriting a paragraph, drafting a prompt.

It is plain text only. No tabs, no history, no search, no account, no network access.

## Using it

1. Start `QuickText.exe`. A tray icon appears in the notification area (on Windows 11 it may first sit under the arrow; drag it out to pin it).
2. Type. Your note is saved as you type and restored on the next start.
3. Press **Esc** or the minus button to hide the window. Press **Ctrl+Shift+Space** or click the tray icon to bring it back.
4. Quit with **Ctrl+Q**, the tray menu, or the settings panel.

Starting the app a second time just brings the running window to the front.

## Features

* Always on top, borderless, resizable. Size and position are remembered, and the window grows with the text up to 85% of the screen.
* Links in the note become pills at the bottom. Hovering a pill highlights the link in the text, clicking opens it in your browser (http and https only).
* Copy copies the whole note. Clear empties it, and Ctrl+Z brings it back.
* Spell checking in your Windows display language.
* Adjustable window opacity, autosave on or off.
* Settings and notes are written atomically, so a crash cannot leave a half-written file.

## Shortcuts

All shortcuts can be changed in the settings (gear icon): click a field, press the keys, Backspace clears it.

| Action | Default | Scope |
| :--- | :--- | :--- |
| Show / hide | `Ctrl+Shift+Space` | System-wide |
| Hide window | `Esc` | Window focused |
| Copy note | `Ctrl+Shift+C` | Window focused |
| Exit | `Ctrl+Q` | Window focused |

If another program already owns the system-wide shortcut, the settings panel says so. The tray icon works either way.

## Requirements

* Windows 10 or 11, 64-bit
* [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0/runtime)

## Data

Everything stays on your machine in `%LOCALAPPDATA%\QuickText`:

* `settings.json` for shortcuts, opacity, autosave, window size and position
* `notes.txt` for the note, only while autosave is on

## Build

Needs the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The executable ends up in `bin/Release/net9.0-windows/win-x64/publish/QuickText.exe`. Debug builds use `%LOCALAPPDATA%\QuickText-dev`, so development never touches your real notes.

QuickText is a plain WPF application. There is no embedded browser; the global shortcut, tray icon, window opacity and rounded corners use the Win32 and DWM APIs directly.

## License

MIT, see [LICENSE](LICENSE).
