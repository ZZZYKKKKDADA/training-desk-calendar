# Desktop Prototype Validation Results

- Measured UTC: 2026-08-17T11:32:52.0142727+00:00
- Runs: 5
- Average cold startup: 663.8 ms — PASS
- Maximum working set: 163.6 MB / 180 MB limit — PASS
- Average idle CPU: 0.045% — PASS
- Framework-dependent directory: 24.4 MB
- Framework-dependent ZIP: 6.4 MB
- Self-contained directory: 163.6 MB
- Self-contained ZIP: 68.4 MB

## Packaging Decision

Use a framework-dependent app with a per-user .NET Desktop Runtime bootstrapper; validate final installer size in phase 3.

## Automated Gate

Overall automated result: PASS

## Phase 1 Input

- The approved working-set threshold remains 180 MB; the prototype automated gate measured a 163.6 MB maximum working set.
- WPF on .NET 10 with the framework-dependent per-user packaging decision is the approved Phase 1 implementation boundary.
- Phase 1 local-data behavior is covered by the automated SQLite, settings, autosave, copy, import/export, rollback, and end-to-end tests.
- Windows 10/11 manual desktop-host and display checks remain pending for Phase 3 release validation; no manual pass is inferred from the automated tests.

## Phase 2 Input

- The complete local desktop workflow is covered by 92 Debug and Release tests using the real composition, SQLite, settings, autosave, and import/export services with temporary data roots.
- A controlled Debug launch created its ready signal, remained alive until explicit timed shutdown, exited with code 0, and produced no new `.NET Runtime` crash event.
- One post-ready working-set sample measured 178.3 MB against the approved 180 MB threshold. This is not a substitute for the Phase 3 multi-run release performance matrix.
- The required Windows 10/11, DPI, Explorer restart, sleep/resume, multi-monitor, installer, and GitHub release checks remain pending and are not marked as passed.
