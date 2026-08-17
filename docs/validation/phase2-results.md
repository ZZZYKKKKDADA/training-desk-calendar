# Phase 2 Desktop Experience Validation Results

- Measured UTC: 2026-08-17T15:25:19.320Z
- Branch: `feature/windows-prototype`
- Task 5 implementation commit: `bb1d383`
- Approved working-set threshold: 180 MB

## Automated Gate

- `dotnet build TrainingDeskCalendar.sln --configuration Debug`: PASS, 0 warnings, 0 errors.
- `dotnet test TrainingDeskCalendar.sln --configuration Debug --no-build`: PASS, 92/92 tests.
- `dotnet test TrainingDeskCalendar.sln --configuration Release`: PASS, 92/92 tests.
- `git diff --check`: PASS after the validation documents were added.

The Phase 2 workflow gate uses temporary application-data roots and the real SQLite, settings, autosave, import/export, and composition services. It covers:

- Default Monday-to-next-Sunday 14-day range, previous/next navigation, and return to today.
- In-place edits, latest-draft persistence, completion changes, and exit-time flush.
- Six fixed task colors and card-fill mapping.
- Single-day and full-week copy behavior, including conflict confirmation.
- Theme, opacity, lock state, window settings, and current-user startup consistency.
- Export/import replacement, in-memory settings refresh, and calendar refresh.
- Single-instance, tray menu model, explicit shutdown, and retryable async disposal boundaries.

## Controlled Windows Launch

The Debug executable was launched with a unique `--ready-file` and `--exit-after-seconds 4`:

- Ready file created: PASS.
- Process remained alive after ready: PASS.
- Timed explicit exit code: 0.
- New `.NET Runtime` crash event: none.
- One working-set sample after ready: 178.3 MB, below the 180 MB threshold.

The working-set result is a single development-machine sample with limited margin. Phase 3 must repeat the formal multi-run performance gate on the required Windows and DPI matrix before release.

## Architecture Gate

- WPF views bind to view models and service callbacks; they do not access SQLite or the registry directly.
- SQLite, settings JSON, startup registration, tray integration, update-check placeholder, and desktop hosting remain behind dedicated service boundaries.
- Startup registration is current-user only and does not request administrator rights.
- Update checking remains offline in Phase 2 and reports that GitHub Releases support belongs to Phase 3.

## Pending Phase 3 Validation

No manual matrix item is marked complete by this document. The following remain pending:

- Windows 10 22H2, Windows 11 24H2, and the latest stable Windows 11 release.
- 100% and 150% DPI, multiple monitors, monitor removal, sleep/resume, Win+D, and Explorer restart.
- Current-user installer, desktop and Start menu shortcuts, uninstall data-retention choice, and runtime bootstrapper.
- Real GitHub repository URL, Actions, Releases, checksums, release notes, and online update checks.
- Repeated cold-start, idle CPU, and working-set measurements on the release package.
