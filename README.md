# Le Dormeur

Small Windows utility (C# / .NET WinForms) that:

1. Gradually reduces **brightness** by a percentage you choose
2. Over a configurable **duration** (e.g. 1 h)
3. Then **forces the PC to sleep** when the timer ends

> **Why “Le Dormeur”?** In French, *le dormeur* means **“the sleeper”** — the one who’s dozing off. Perfect for an app that gently dims the lights and tucks your PC into bed.

## Download

Grab the latest **ready-to-run** build from the [GitHub Releases](https://github.com/Kaotic/LeDormeur/releases) page — no need to install the .NET SDK. Download `LeDormeur.exe` from the newest release and run it.

## Usage

```bash
dotnet run --project src/LeDormeur
```

Or open `LeDormeur.sln` in Visual Studio / Rider and run the project.

### Settings

| Field | Description |
|--------|-------------|
| **Hours / minutes** | Duration before sleep (e.g. 1 h 0 min) |
| **Brightness to remove (%)** | Percentage points removed gradually over the duration |
| **Language** | Français, English, Español, Deutsch, Italiano, Português, Nederlands, Русский, 中文 — updates immediately. On first launch (no saved language), the Windows UI language is detected automatically. |

Settings (duration, %, language) are **saved automatically** to:

`%AppData%\LeDormeur\settings.json`

The example under the slider is **dynamic**: it uses the duration, the chosen %, and the current brightness to show the estimated result (start → target).

- **Start**: starts the timer
- **Cancel**: stops the timer and restores the original brightness

### Automatic mode

Configure via **Auto mode...** (main window or tray menu):

1. Enable automatic mode
2. Choose a **daily check time** (e.g. 23:00)
3. Choose a **response timeout** (e.g. 5 minutes)

At that time, a top-most dialog asks **“Are you still there?”** with a countdown (plus a tray balloon).

- **Yes, I'm here** (or close the window) → no sleep for that day
- **No answer** before timeout → PC sleeps

The app must be running (tray is fine). Manual brightness timer and auto mode do not overlap: if a manual timer is running, auto mode waits.

### System tray

- **Minimize** or **close (X)** sends the app to the notification area
- Tray **tooltip** shows remaining time while a timer is running
- Tray menu: **Open**, **Cancel timer**, **Exit**
- Double-click / left-click the icon to restore the window
- The pre-sleep warning restores the window automatically

### Pre-sleep warning

About **2 minutes** before sleep (or half the duration for short timers), a top-most dialog appears:

- Live countdown: **“Sleeping in X s”**
- **Postpone (+15 min)**: extends the timer, keeps current brightness, restarts the fade toward the target
- **Cancel**: aborts and restores brightness
- **Continue** / close: dismisses the dialog; sleep still happens at the end

## Brightness mode

- **WMI** (often laptops): controls real system brightness
- **Software gamma** (fallback): dims the image when WMI is unavailable (external displays / desktop)

## Dev: dry-run mode

Not exposed in the UI. Skips the real Windows sleep call (timer, brightness, and warning still run).

**Visual Studio:** select the launch profile **`LeDormeur (dry-run)`** (dropdown next to the Start button).

**CLI:**

```bash
dotnet run --project src/LeDormeur -- --dry-run
```

Or set environment variable `LEDORMEUR_DRY_RUN=1`.

## Requirements

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or runtime to run a published build)

## Publish (single-file)

Produces one self-contained `LeDormeur.exe` (no .NET install required on the target PC).

**PowerShell (recommended):**

```powershell
.\publish.ps1
```

**Or manually:**

```bash
dotnet publish src/LeDormeur/LeDormeur.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

Output: `publish/LeDormeur.exe`

Optional: `.\publish.ps1 -Runtime win-arm64` on ARM Windows.
