# Phase 3A Release Payload Results

- Measured UTC: 2026-08-17T17:48:05.4477297+00:00
- Classification: fresh-materialized-path
- OS: Microsoft Windows NT 10.0.26200.0
- Git commit: 31a4d57ebc6261439522e22dc24eca1670b9a3e9
- Source EXE SHA-256: 888e80e77a0febf0913220cb67cb68b6ce3be3f5bdc7dbae6e6503e2d1f939ea
- Payload: 127.58 MB in 2 files
- Parameters: runs=5, ready timeout=10 s, idle sample=15 s, exit after=20 s
- Maximum startup: 1268.4 ms - PASS
- Average startup: 1259.5 ms
- Maximum working set: 172.5 MiB / 200 MiB - PASS
- Maximum private bytes: 105.1 MB

| Run | Startup ms | CPU at ready ms | Max working set MB | Max private MB | Idle samples |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1264.3 | 578.1 | 172.1 | 103.8 | 30 |
| 2 | 1257.1 | 671.9 | 172.4 | 104.5 | 30 |
| 3 | 1260.2 | 625 | 172.4 | 105.1 | 30 |
| 4 | 1268.4 | 609.4 | 172.4 | 104.5 | 30 |
| 5 | 1247.4 | 625 | 172.5 | 105.1 | 30 |

The machine-readable results are stored in artifacts/phase3a-payload-measurement/phase3a-payload-results.json.
