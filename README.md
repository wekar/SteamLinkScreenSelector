# Steam Link Screen Selector

A tiny Windows tray utility that **locks which monitor Steam Remote Play / Steam Link streams**,
regardless of where your mouse goes. It runs in the system tray — right‑click the icon to pick
the monitor to stream, or go back to the default "follow cursor" behaviour.

![icon](icon.png)

## How it works

Steam decides which monitor to capture from the cursor position (via `GetCursorPos`).
This tool installs a small user‑mode trampoline hook on `GetCursorPos` inside the running
`steam.exe` process that clamps the reported cursor into the bounds of the monitor you chose,
so the capture stays pinned to that display. Removing the lock restores the original bytes.

The hook adapts to the host's `GetCursorPos` prologue at runtime, so it works across different
Windows 10 / 11 builds (which lay out that function differently). It only hooks the **64‑bit**
Steam client.

No files are injected on disk and nothing is modified permanently — the hook lives only in
memory and is removed when you pick **Default (follow cursor)** or exit the app.

## Usage

1. Start Steam.
2. Run `SteamLinkScreenSelector.exe`.
3. Right‑click the tray icon and choose the monitor to lock the stream to.
4. Choose **Default (follow cursor)** or **Exit** to release the lock.

> The tool needs permission to open the Steam process. If a tray balloon reports a problem,
> it names the exact cause, e.g. Steam not running, access denied (run it as the same user as
> Steam, or elevated if Steam is elevated), or a 32‑bit Steam client (update Steam to the
> current 64‑bit client).

## Build from source

Requires only the .NET Framework that ships with Windows (no Visual Studio needed).

```
cd src
build.cmd
```

This produces `SteamLinkScreenSelector.exe` in the release root, version `1.0.0.0`, with
`app.ico` embedded as the application icon.

## Contents

```
SteamLinkScreenSelector.exe   ready-to-run build (icon embedded)
icon.png                      app icon
src/
  SteamLinkScreenSelector.cs  full source
  app.ico                     icon embedded into the exe
  icon.png                    source icon
  build.cmd                   one-click build script
```
