<p align="center">
  <img src="Assets/tay.png" alt="TAY Logo" width="180"/>
</p>

<h1 align="center">TAY - System Optimizer</h1>

<p align="center">
  <strong>Version:</strong> v0.2.0
</p>

<p align="center">
  <strong>A modern native Windows system optimization and monitoring tool.</strong><br/>
  Built with WinUI 3 and .NET 8 · Lightweight · Real-time Telemetry · Guided Optimization
</p>

<p align="center">
  <a href="https://github.com/Palm1ye/TAY/releases/latest"><img src="https://img.shields.io/github/v/release/Palm1ye/TAY?style=flat-square&color=4AEADC&label=Download" alt="Latest Release"/></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/Palm1ye/TAY?style=flat-square&color=3B9FFF" alt="License: MIT"/></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0D1B2A?style=flat-square&logo=windows&logoColor=4AEADC" alt="Platform"/>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8"/>
</p>

---

## Functional & Technical Architecture

TAY is designed as a native Windows utility using **Windows App SDK**, **WinUI 3**, and **.NET 8**. It provides real-time monitoring, guided cleanup, process control, startup management, disk analysis, and reversible system tuning without Electron-style overhead.

### Real-Time Dashboard & Telemetry

The dashboard is a bento-style command center for live system status.

* **CPU, RAM, GPU, and disk telemetry:** Live usage values with compact progress indicators.
* **Telemetry history:** LiveCharts2/SkiaSharp chart for recent CPU, RAM, and GPU activity.
* **Power plan control:** Reads and switches available Windows power plans through `powercfg`.
* **Quick Boost:** Trims process working sets and performs a managed memory sweep for quick memory recovery.
* **Hardware summary:** Shows CPU, GPU, RAM, motherboard, process count, disk status, and uptime.

### Boost Tuning

Boost Tuning groups higher-impact maintenance tools into guided cards with realistic expectations.

* **Privacy Shield:** Reduces Windows telemetry and assistant background activity with a reversible local backup.
* **Game Focus Mode:** Temporarily pauses selected high-overhead services and raises known game process priority. Typical FPS impact is shown as an estimate, not a guarantee.
* **Memory Sweep:** Reclaims memory from idle working sets and standby cache. Best used after long sessions or when stutter appears.
* **Network Optimizer:** Flushes DNS, resets TCP/IP, scans DNS latency, and applies recommended DNS only after explicit confirmation.
* **DNS safety warning:** TAY warns users not to apply recommended DNS if they use custom DNS for website-block bypassing, filtering, parental controls, ad blocking, or custom routing.
* **Context Menu Manager:** Scans and toggles third-party shell extensions under right-click paths.
* **Driver Status:** Scans signed drivers and flags outdated display/chipset-related components.

### Disk Space Analyzer

Disk Analyzer helps identify storage pressure and locate large files.

* **Drive cards:** Shows fixed drives with free/used capacity and a scan action.
* **Visual Storage Map:** Categorizes scanned files into System, Applications, User Media, Cache & Temp, and Other files.
* **Largest files list:** Displays large detected files with size, full path, copy-path action, and Explorer location opening.
* **Guided empty states:** Explains what to do before a scan and what the visual map represents after analysis.

### Startup Manager

Startup Manager helps review and control programs that launch with Windows.

* **Registry inspection:** Reads startup entries from standard Windows Run registry locations.
* **Status toggles:** Enables or disables supported startup items.
* **Sorting controls:** Sort by recommended order, app name, impact, enabled-first, or disabled-first.
* **Guidance text:** Explains which view is useful and warns users to disable only entries they recognize.

### Cleaner

Cleaner identifies temporary and cache-heavy locations.

* **Cleanup targets:** Windows Temp, user Temp, browser cache locations, Prefetch, and Recycle Bin.
* **Selected-size summary:** Shows selected cleanup size and target count before cleaning.
* **Confirmation before deletion:** TAY asks for confirmation before removing files.
* **Safe execution:** Locked files are skipped and cleanup status is reported after the operation.

### Hardware

Hardware view collects detailed local system information.

* **Processor:** CPU name, core count, logical threads, and clock speed.
* **Graphics:** GPU name, dedicated VRAM, shared memory, and total graphics memory.
* **Memory:** Installed RAM capacity and memory architecture.
* **System:** Motherboard, operating system, build information, and platform details.

### Process Manager

Process Manager provides a safer view of active processes.

* **Search:** Filter processes by name.
* **Sorting:** Sort by RAM high-to-low, RAM low-to-high, name A-Z, or name Z-A.
* **Protected process handling:** Critical Windows processes are labeled and protected from accidental termination.
* **Confirmation before End Task:** TAY asks before forcing a process to exit.

### Settings & Diagnostics

Settings is the maintenance hub for the application.

* **Application info:** Version, channel, privilege status, runtime, and architecture.
* **System snapshot:** OS, CPU, RAM, GPU, and diagnostics copy action.
* **Update control:** GitHub release check, installer download, and release page opening.
* **Local maintenance:** App data folder access, backup-state refresh, and local backup cleanup.
* **Safety model:** Documents which high-impact operations require confirmation.

### Borderless System Tray Widget

The tray widget provides quick access to live resource status.

* **Compact telemetry:** CPU, memory, and GPU status.
* **Quick Boost:** Runs the same memory recovery action from the tray.
* **Dashboard shortcut:** Opens the main application window.
* **Borderless Win32 shell:** Uses native window styling for a compact floating panel.

---

## Installation

### Quick Install

1. Go to the [Releases](https://github.com/Palm1ye/TAY/releases/latest) page.
2. Download `TAY_Setup.exe`.
3. Run the installer.
4. Launch TAY from the Start Menu or desktop shortcut.

### Portable Zip

1. Download the portable `.zip` archive from [Releases](https://github.com/Palm1ye/TAY/releases/latest), if available.
2. Extract it to any folder.
3. Run `TAY.exe`.

---

## Build from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 build 17763 or later
- Windows 11 recommended
- [Inno Setup 6](https://jrsoftware.org/isdl.php) for installer packaging

### Build & Run

Debug build for the default output folder:

```powershell
dotnet build -p:Platform=x64
```

Runtime-specific Debug build:

```powershell
dotnet build -r win-x64 -p:Platform=x64
```

Run from source:

```powershell
dotnet run -r win-x64 -p:Platform=x64
```

### Publish & Package

Build the installer:

```powershell
.\build_setup.ps1
```

Manual Release publish:

```powershell
dotnet publish -c Release -r win-x64 -p:Platform=x64 --self-contained true
```

Release binaries are generated under:

```text
bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\
```

Installer output:

```text
Output\TAY_Setup.exe
```

---

## Safety Notes

TAY intentionally performs administrator-level operations for several system tools. Review prompts carefully before applying DNS changes, resetting TCP/IP, disabling startup entries, ending processes, clearing cache folders, or changing tuning settings.

For DNS optimization specifically: do not apply TAY's recommended DNS profile if you already use a DNS service for website-block bypassing, filtering, parental controls, ad blocking, private routing, or organization-managed network policy.

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| UI Framework | WinUI 3 |
| Runtime | .NET 8 |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| Charts | LiveCharts2 / SkiaSharp |
| System Info | WMI / System.Management |
| Performance | Performance Counters and Win32 APIs |
| Installer | Inno Setup |

---

## License

This project is licensed under the [MIT License](LICENSE).

---

<p align="center">
  <sub>Made by <a href="https://github.com/Palm1ye">Palm1ye</a></sub>
</p>
