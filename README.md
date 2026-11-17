# Steam Link Screen Selector

A tiny Windows tray utility that **locks which monitor Steam Remote Play / Steam Link streams**,
regardless of where your mouse goes. It runs in the system tray — right‑click the icon to pick
the monitor to stream, or go back to the default "follow cursor" behaviour.

![logo](logo.png)

## How it works

Steam decides which monitor to capture from the cursor position (via `GetCursorPos`).
This tool installs a small user‑mode trampoline hook on `GetCursorPos` inside the running
`steam.exe` process that clamps the reported cursor into the bounds of the monitor you chose,
so the capture stays pinned to that display. Removing the lock restores the original bytes.

No files are injected on disk and nothing is modified permanently — the hook lives only in
memory and is removed when you pick **Default (follow cursor)** or exit the app.

## Usage

1. Start Steam.
2. Run `SteamLinkScreenSelector.exe`.
3. Right‑click the tray icon and choose the monitor to lock the stream to.
4. Choose **Default (follow cursor)** or **Exit** to release the lock.

> Needs permission to open the Steam process. If you see "Steam not running or access denied",
> run the tool with the same (or higher) privileges as Steam.

## Build from source

Requires only the .NET Framework that ships with Windows (no Visual Studio needed).

```
cd src
build.cmd
```

This produces `SteamLinkScreenSelector.exe` in the release root with `logo.png` embedded as the
application icon.

## Contents

```
SteamLinkScreenSelector.exe   ready-to-run build (logo embedded as icon)
logo.png                      app logo
src/
  SteamLinkScreenSelector.cs  full source
  app.ico                     icon generated from the logo
  logo.png                    source logo
  build.cmd                   one-click build script
```
