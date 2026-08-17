# Desktop Prototype Validation Results

- Measured UTC: 2026-08-17T11:26:15.1471548+00:00
- Runs: 5
- Average cold startup: 649 ms — PASS
- Maximum working set: 159.3 MB — FAIL
- Average idle CPU: 0.034% — PASS
- Framework-dependent directory: 24.4 MB
- Framework-dependent ZIP: 6.4 MB
- Self-contained directory: 163.6 MB
- Self-contained ZIP: 68.4 MB

## Packaging Decision

Use a framework-dependent app with a per-user .NET Desktop Runtime bootstrapper; validate final installer size in phase 3.

## Automated Gate

Overall automated result: FAIL
