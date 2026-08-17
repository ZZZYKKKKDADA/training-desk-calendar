# Release Performance Results

- Measured UTC: 2026-08-17T19:25:14.8496438+00:00
- Application version: 0.1.0
- Classification: fresh-materialized-path
- OS: Microsoft Windows NT 10.0.26200.0
- Git commit: 1c3e9fa6bd661f077f3500feef5e856bc825ac01
- Payload: 127.61 MiB in 2 files - PASS
- Installer: 41.76 MiB - PASS
- Installed directory: 131.87 MiB - PASS
- Maximum startup: 1337.3 ms / 2000 ms - PASS
- Maximum working set: 186.4 MiB / 200 MiB - PASS
- Maximum idle CPU: 0.053% / 0.5% - PASS
- Maximum automatic save latency: 285.2 ms / 300 ms - PASS

| Run | Startup ms | Idle CPU % | Max working set MiB | Max save ms | Idle samples |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1316.5 | 0.048 | 184.8 | 282 | 117 |
| 2 | 1280.5 | 0.04 | 185.6 | 283.2 | 117 |
| 3 | 1327.9 | 0.049 | 186.1 | 285.2 | 117 |
| 4 | 1267.9 | 0.053 | 185.4 | 283.6 | 117 |
| 5 | 1337.3 | 0.048 | 186.4 | 283.3 | 117 |

Raw JSON: artifacts/release-measurement/release-performance-results.json.
