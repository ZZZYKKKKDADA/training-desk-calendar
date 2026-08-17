# Data Recovery Validation Results

- Audited UTC: 2026-08-17T19:03:33.5472690+00:00
- Unique temporary root: C:\Users\82148\AppData\Local\Temp\training-desk-recovery-6b7c6463beba409c9b5895ea047770dc
- Scenario count: 5
- Audit JSON SHA-256: f6729db08601f7e3fa0eaad84dbe0ab3ee6bc4a46102ed454f36b2aded3b242d
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
