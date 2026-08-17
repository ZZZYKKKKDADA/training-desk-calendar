# Data Recovery Validation Results

- Audited UTC: 2026-08-17T19:25:29.9743134+00:00
- Unique temporary root: C:\Users\82148\AppData\Local\Temp\training-desk-recovery-9d4bf3ebfa574b7d9f59c31792b3dfc8
- Scenario count: 5
- Audit JSON SHA-256: 8389cd20dafe6b1ba0a266596cc1c4d603666c07597bafbcd73cc818c697a223
- Logs contain training plan text: False - PASS
- All scenarios passed: True

Physical database and settings SHA-256 values are retained for every scenario. SQLite rollback may rewrite physical database pages; the audit also records canonical logical plan/settings hashes and only marks rollback successful when logical state is restored.

| Scenario | Outcome | Database before | Database after | Passed |
| --- | --- | --- | --- | --- |
| corrupt-json | rejected-without-mutation | 48e4a32a0c39... | 48e4a32a0c39... | True |
| unknown-version | rejected-without-mutation | 48e4a32a0c39... | 48e4a32a0c39... | True |
| invalid-color | rejected-without-mutation | 48e4a32a0c39... | 48e4a32a0c39... | True |
| settings-write-failure-rollback | rollback-restored | 041065edb82d... | 85bc0eca9250... | True |
| database-corruption-isolated-copy | rejected-with-source-unchanged | 0843cec69a96... | 0843cec69a96... | True |

Machine-readable result: artifacts/data-recovery/data-recovery-results.json.
