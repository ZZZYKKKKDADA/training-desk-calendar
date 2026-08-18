# Training Desk Calendar Window Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent the settings window from crashing and allow the desktop-hosted calendar window to move by dragging its header.

**Architecture:** Keep the existing WPF settings window and desktop `WorkerW` hosting. Make read-only bindings explicitly one-way, and introduce a small stateful drag calculator that converts screen-pointer deltas into `Left`/`Top` updates without calling `DragMove`, which is unsupported for the hosted child window.

**Tech Stack:** .NET 10 WPF, C#, xUnit, PowerShell build scripts.

---

### Task 1: Add regression tests

**Files:**
- Modify: `tests/TrainingDeskCalendar.App.Tests/Calendar/WindowInteractionTests.cs`
- Create: `src/TrainingDeskCalendar.App/Windowing/WindowDragService.cs`

- [ ] Add a XAML contract test requiring read-only settings bindings to specify `Mode=OneWay`.
- [ ] Add a behavior test for pointer-delta drag calculations.
- [ ] Run the focused tests and confirm they fail against the current implementation.

### Task 2: Fix settings bindings

**Files:**
- Modify: `src/TrainingDeskCalendar.App/Settings/SettingsWindow.xaml`

- [ ] Set `Mode=OneWay` on `VersionText`, `RepositoryText`, and `CanOpenRepository` bindings.
- [ ] Re-run the focused settings test.

### Task 3: Replace `DragMove` for desktop child windows

**Files:**
- Modify: `src/TrainingDeskCalendar.App/MainWindow.xaml`
- Modify: `src/TrainingDeskCalendar.App/MainWindow.xaml.cs`
- Modify: `src/TrainingDeskCalendar.App/Windowing/WindowDragService.cs`

- [ ] Add mouse-move and mouse-release handlers to the header.
- [ ] Track screen coordinates while the header captures the mouse.
- [ ] Apply calculated deltas to `Left` and `Top`, respecting the existing lock state.
- [ ] Release capture on completion/cancel and preserve placement tracking.

### Task 4: Verify

**Files:**
- No additional files.

- [ ] Run `dotnet test --no-restore`.
- [ ] Run the Windows packaging script and verify a single-file executable is produced.
- [ ] Inspect the final diff for scope and unintended changes.
