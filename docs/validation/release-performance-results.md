# Release Performance Results

- Measured UTC: 2026-08-17T19:13:45.7307166+00:00
- Application version: 0.1.0
- Classification: fresh-materialized-path
- OS: Microsoft Windows NT 10.0.26200.0
- Git commit: edf11078d5f8e27474232d5b276a97dda647bd96
- Payload: 127.61 MiB in 2 files - PASS
- Installer: 41.76 MiB - PASS
- Installed directory: 131.87 MiB - PASS
- Maximum startup: 1371.3 ms / 2000 ms - PASS
- Maximum working set: 186.5 MiB / 200 MiB - PASS
- Maximum idle CPU: 0.052% / 0.5% - PASS
- Maximum automatic save latency: 285.1 ms / 300 ms - PASS

| Run | Startup ms | Idle CPU % | Max working set MiB | Max save ms | Idle samples |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1371.3 | 0.046 | 186.5 | 283.1 | 117 |
| 2 | 1294.6 | 0.052 | 185.9 | 285.1 | 117 |
| 3 | 1294.8 | 0.045 | 186.2 | 283.8 | 117 |
| 4 | 1278.9 | 0.034 | 185.8 | 283.4 | 117 |
| 5 | 1310.5 | 0.038 | 185 | 284.6 | 117 |

Raw JSON: artifacts/release-measurement/release-performance-results.json.
