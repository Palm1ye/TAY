<p align="center">
  <img src="Assets/tay.png" alt="TAY Logo" width="180"/>
</p>

<h1 align="center">TAY — System Optimizer</h1>

<p align="center">
  <strong>Version:</strong> v0.1.1.2
</p>

<p align="center">
  <strong>A modern, native Windows system optimization &amp; monitoring tool.</strong><br/>
  Built with WinUI 3 &amp; .NET 8 · Lightweight · Real-time Telemetry
</p>

<p align="center">
  <a href="https://github.com/Palm1ye/TAY/releases/latest"><img src="https://img.shields.io/github/v/release/Palm1ye/TAY?style=flat-square&color=4AEADC&label=Download" alt="Latest Release"/></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/Palm1ye/TAY?style=flat-square&color=3B9FFF" alt="License: MIT"/></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0D1B2A?style=flat-square&logo=windows&logoColor=4AEADC" alt="Platform"/>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8"/>
</p>

---

## 🛠️ Functional & Technical Architecture

TAY is designed as a native Windows utility leveraging the **Windows App SDK** and **WinUI 3** to provide low-level system administration capabilities without heavy electron-based dependencies. Below is the technical breakdown of each functional module:

### 📊 Real-Time Dashboard & Telemetry (`sys_overview.sh`)
Provides real-time hardware telemetry and scheduling in a beautiful bento-box dashboard.
*   **CPU Utilization Monitoring**: Tracked using C#'s `System.Diagnostics` performance counters, measuring processor time accurately across logical threads.
*   **GPU Engine Tracking**: Polled from custom performance counters or DirectX device telemetry arrays to represent real-time engine load.
*   **Memory Telemetry**: Analyzes physical RAM and calculates accurate heap allocations and committed bytes.
*   **Interactive History Graph**: Visualized using a highly optimized, hardware-accelerated **LiveCharts2 (SkiaSharp)** cartesian chart showing rolling history vectors of CPU, RAM, and GPU.
*   **Power Plan Engine**: Queries available system power plans using native Windows `powercfg` sub-commands/WMI calls and allows seamless, instant active plan switching.
*   **🚀 Quick Boost Memory Sweep**: Executes a high-performance, background-threaded RAM sweep:
    1. Forces deep .NET garbage collections (`GC.Collect()` and `GC.WaitForPendingFinalizers()`).
    2. Invokes the native Windows kernel API `EmptyWorkingSet` via `psapi.dll` across active running processes.
    3. Releases unused private working sets back to the operating system, instantly freeing up hundreds of megabytes of RAM.

### 💾 Disk Space Analyzer
Deep-scans storage drives to locate capacity bottlenecks.
*   **Multi-Threaded Directory Crawler**: Asynchronously scans the chosen drive partition utilizing C#'s `System.IO` APIs.
*   **Visual Storage Categories**: Automatically categorizes files into **System Files**, **Applications**, **User Media** (images, videos, audio), and **Cache/Temp files**.
*   **Top 50 Largest Files**: Automatically discovers and lists the 50 largest files on the system with full file paths and sizes, allowing you to instantly locate space-hogging archives, virtual machines, or log arrays.

### ⚡ Startup Manager
Optimizes boot times by cleaning up background programs.
*   **Registry Registry Hive Inspection**: Scans standard Windows run locations:
    - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
    - `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`
*   **Startup Directory Polling**: Inspects user-specific and machine-wide standard Startup folders.
*   **One-Click Toggle**: Allows you to instantly disable or re-enable items with simple registry mutations to decrease Windows boot latencies.

### 🧹 Deep System Cleaner
Identifies and purges system junk to reclaim gigabytes of disk space.
*   **Target Caches**: Scans and cleans:
    - User Temp folders (`%TEMP%`) and System Temp directory (`C:\Windows\Temp`).
    - Windows Prefetch cache (`C:\Windows\Prefetch`).
    - Web browser temporary caches and cookie files.
    - System error logs, memory crash dumps, and Windows Update download archives.
*   **Safe Execution**: Features safe folder crawling to ensure active system lock files are bypassed without causing utility crashes.

### 🖥️ Hardware Specification Dump
Generates detailed hardware reports.
*   **WMI (Windows Management Instrumentation) Queries**: Employs optimized `System.Management` queries to dump:
    - Processor model name, physical cores, and architecture.
    - Graphics card manufacturer and model.
    - Installed physical memory capacity and architecture (e.g., DDR4/DDR5 detection).
    - Motherboard baseboard manufacturer and model.

### 📋 Process Manager
Monitors running processes.
*   **Active Array Listing**: Pulls running processes with Process ID (PID), memory working set sizes, and active thread counts.
*   **Unresponsive Killer**: Supports one-click termination of frozen or heavy system tasks using `Process.Kill()`.

### 🔧 Borderless System Tray Widget
A premium floating mini-dashboard accessible from your taskbar tray.
*   **Win32 Custom Borderless Shell**: Bypasses traditional WinUI window frame styling. Strips standard borders, minimize/maximize boxes, caption buttons, and resizing frames (`WS_CAPTION`, `WS_THICKFRAME`, `WS_BORDER`) using native `SetWindowLong` overrides.
*   **Jitter-Free Dragging**: Custom cursor tracking using Win32 `GetCursorPos` in code-behind enables highly fluid dragging from any empty space of the card with zero jitter.
*   **Quick Tools**: Includes live resource load gauges, a system uptime timer, and a dedicated **Quick Boost** button for rapid memory sweep actions.

---

## 🚀 Installation

### Quick Install (Recommended)
1. Go to the [**Releases**](https://github.com/Palm1ye/TAY/releases/latest) page.
2. Download `TAY_Setup.exe`.
3. Run the installer and follow the quick prompts.
4. Launch TAY from your Desktop or Start Menu.

### Portable Zip
1. Download the `.zip` archive from [**Releases**](https://github.com/Palm1ye/TAY/releases/latest).
2. Extract to any folder.
3. Run `TAY.exe` directly.

---

## 🛠️ Build from Source

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK 1.5](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- Windows 10 (build 17763) or later
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (optional, for compiling the installer)

### Build & Run
```bash
git clone https://github.com/Palm1ye/TAY.git
cd TAY
dotnet build -r win-x64 -p:Platform=x64
dotnet run -r win-x64 -p:Platform=x64
```

### Publish & Package
TAY using Inno Setup for compiling the installer:

```powershell
# Run the automated packaging pipeline
./build_setup.ps1
```

If you prefer to publish manually without Inno Setup:
```bash
dotnet publish -c Release -r win-x64 -p:Platform=x64 --self-contained false
```
The raw binaries will be generated in `bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish/`.

---


## 🧩 Tech Stack

| Component | Technology |
|-----------|-----------|
| UI Framework | WinUI 3 (Windows App SDK 1.5) |
| Runtime | .NET 8 |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Charts | LiveCharts2 (SkiaSharp) |
| System Info | WMI (System.Management) |
| Performance | PerformanceCounters & Win32 APIs |

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<p align="center">
  <sub>Made with ❤️ by <a href="https://github.com/Palm1ye">Palm1ye</a></sub>
</p>
