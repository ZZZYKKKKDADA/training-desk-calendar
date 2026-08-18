# Training Desk Calendar Icon Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the supplied calendar artwork as the application icon and release the accumulated fixes as `v0.1.1`.

**Architecture:** Convert the supplied 512px PNG into a multi-resolution ICO embedded as the WPF application icon. Load the same embedded resource for the native tray icon, and derive installer version/output naming from the repository version so EXE, shortcuts, tray, installer, and release metadata stay aligned.

**Tech Stack:** .NET 10 WPF, Win32 shell APIs, Inno Setup, PowerShell, xUnit, GitHub Releases.

---

### Task 1: Add icon asset and resource wiring

**Files:**
- Create: `src/TrainingDeskCalendar.App/Assets/calendar.ico`
- Modify: `src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj`
- Modify: `src/TrainingDeskCalendar.App/Windows/TrayService.cs`
- Test: `tests/TrainingDeskCalendar.App.Tests/Windows/TrayServiceTests.cs`

- [ ] Generate 16, 24, 32, 48, 64, 128, and 256px ICO frames from `C:\Users\82148\Desktop\calendar.png`.
- [ ] Configure `ApplicationIcon` to embed the ICO in the executable.
- [ ] Load resource ID 32512 from the current executable module for the tray icon, with the existing system icon only as fallback.
- [ ] Add contract coverage for the custom application icon and module-backed tray loading.

### Task 2: Prepare the `0.1.1` release metadata

**Files:**
- Modify: `eng/Versions.props`
- Modify: `installer/TrainingDeskCalendar.iss`
- Modify: release-facing tests and documentation that intentionally pin `0.1.0` installer names.

- [ ] Change the application version to `0.1.1`.
- [ ] Update the installer default version and output base filename to `0.1.1`.
- [ ] Keep historical `v0.1.0` validation records unchanged; update current README/release instructions to point at `v0.1.1`.

### Task 3: Verify and publish

**Files:**
- No additional source files.

- [ ] Run the full test suite and Release build.
- [ ] Build the self-contained payload and Inno Setup installer.
- [ ] Run installer/package contract checks and inspect the final diff.
- [ ] Commit all accumulated fixes and icon/version changes.
- [ ] Create and push tag `v0.1.1`, then push `main` to `origin`.
