# Release Performance Results

- Measured UTC: 2026-08-17T18:57:06.2989063+00:00
- Application version: 0.1.0
- Classification: fresh-materialized-path
- OS: Microsoft Windows NT 10.0.26200.0
- Git commit: a3e728de9f63f32f3da45348134422d8a1700710
- Payload: 127.61 MiB in 2 files - PASS
- Installer: 41.77 MiB - PASS
- Installed directory: 131.87 MiB - PASS
- Maximum startup: 1370.3 ms / 2000 ms - PASS
- Maximum working set: 186.7 MiB / 200 MiB - PASS
- Maximum idle CPU: 0.051% / 0.5% - PASS
- Maximum automatic save latency: 284.8 ms / 300 ms - PASS

| Run | Startup ms | Idle CPU % | Max working set MiB | Max save ms | Idle samples |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1370.3 | 0.051 | 186.7 | 279.4 | 117 |
| 2 | 1280.6 | 0.048 | 185.7 | 284.3 | 117 |
| 3 | 1302.6 | 0.043 | 186.2 | 283.2 | 117 |
| 4 | 1298.5 | 0.043 | 186.3 | 283.2 | 117 |
| 5 | 1294.2 | 0.047 | 185.4 | 284.8 | 117 |

Raw JSON: artifacts/release-measurement/release-performance-results.json.
