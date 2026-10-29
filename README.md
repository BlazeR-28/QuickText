# QuickText

[![Build](https://github.com/BlazeR-28/QuickText/actions/workflows/build.yml/badge.svg)](https://github.com/BlazeR-28/QuickText/actions/workflows/build.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-555555.svg?style=flat-square)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-555555.svg?style=flat-square)](LICENSE)

[![Download QuickText](https://img.shields.io/badge/Download-Latest%20Release-E2AD3B.svg?style=for-the-badge&logo=github&logoColor=1B1A18&labelColor=1B1A18)](https://github.com/BlazeR-28/QuickText/releases/latest)

A small scratchpad for Windows. Press a shortcut, jot something down, copy it, press the shortcut again. QuickText stays in the background, uses little memory and is built for short, temporary notes: collecting text from several websites, rewriting a paragraph, drafting a prompt.

It is plain text only. No tabs, no history, no search, no account, no network access.

## Install

1. Download **`QuickText.exe`** from the [latest release](https://github.com/BlazeR-28/QuickText/releases/latest).
2. Double-click it. That is all: nothing to install, no extra runtime needed.

Windows may show a blue "Windows protected your PC" box, because the file is not code-signed. Click **More info**, then **Run anyway**. You can compare the file with `SHA256SUMS.txt` from the release if you want to be sure it is the original.

Prefer a tiny download? `QuickText-needs-dotnet.exe` is about 0.2 MB but needs the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0/runtime) installed.

To remove QuickText, delete the exe and the folder `%LOCALAPPDATA%\QuickText`.

## Using it

1. Start QuickText. A tray icon appears in the notification area (on Windows 11 it may first sit under the arrow; drag it out to pin it).
2. Type. Your note is saved as you type and restored on the next start.
3. Press **Esc** or the minus button to hide the window. Press **Ctrl+Shift+Space** or click the tray icon to bring it back.
4. Quit with **Ctrl+Q**, the tray menu, or the settings panel.

Starting the app a second time just brings the running window to the front. In the settings you can let QuickText start quietly with Windows.

## Features

* Always on top, borderless, resizable. Size and position are remembered, and the window grows with the text up to 85% of the screen.
* Links in the note become pills at the bottom. Hovering a pill highlights the link in the text, clicking opens it in your browser (http and https only).
* Copy copies the whole note. Clear empties it, and Ctrl+Z brings it back.
* Spell checking in your Windows display language, with suggestions in the right-click menu.
* Adjustable window opacity, autosave on or off, optional start with Windows.
* Settings and notes are written atomically, so a crash cannot leave a half-written file.
* While hidden, QuickText gives its memory back to Windows and typically sits well under 50 MB.

## Shortcuts

All shortcuts can be changed in the settings (gear icon): click a field, press the keys, Backspace clears it.

| Action | Default | Scope |
| :--- | :--- | :--- |
| Show / hide | `Ctrl+Shift+Space` | System-wide |
| Hide window | `Esc` | Window focused |
| Copy note | `Ctrl+Shift+C` | Window focused |
| Exit | `Ctrl+Q` | Window focused |

Combinations that would get in the way of typing are refused (a bare letter, or Ctrl+Alt+letter, which is AltGr on many keyboard layouts). If another program already owns the system-wide shortcut, the settings panel says so. The tray icon works either way.

## Troubleshooting

* **The window is gone.** Click the tray icon, press the global shortcut, or start `QuickText.exe` again.
* **The tray icon is missing.** On Windows 11 new icons start under the arrow in the taskbar; drag it out to keep it visible.
* **The old taskbar icon still shows after an update.** Windows caches icons. Restart Explorer or sign out and in.
* **Something crashed.** Details are written to `%LOCALAPPDATA%\QuickText\crash.log`. Please attach it to an issue.

## Data

Everything stays on your machine in `%LOCALAPPDATA%\QuickText`:

* `settings.json` for shortcuts, opacity, autosave, window size and position
* `notes.txt` for the note, only while autosave is on

Set the environment variable `QUICKTEXT_DATA_DIR` to a folder to keep all of it somewhere else, for example next to the exe on a USB stick.

## Build

Needs the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0):

```powershell
dotnet publish -c Release -o out            # self-contained single exe
dotnet publish -c Release -p:SelfContained=false -o out-small   # needs the .NET runtime
```

Debug builds use `%LOCALAPPDATA%\QuickText-dev`, so development never touches your real notes. Pushing a tag like `v2.0.0` makes GitHub Actions build both variants and attach them to a release.

QuickText is a plain WPF application. There is no embedded browser; the global shortcut, tray icon, window opacity and rounded corners use the Win32 and DWM APIs directly.

## License

MIT, see [LICENSE](LICENSE).
