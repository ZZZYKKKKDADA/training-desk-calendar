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
