# Desktop Prototype Manual Checks

## Required Environments

- Windows 10 22H2 x64, 100% DPI
- Windows 11 24H2 x64, 150% DPI
- Latest stable Windows 11 x64 available at validation time

## Desktop Behavior

- [ ] Widget has no taskbar button.
- [ ] Widget is visible on the desktop after startup.
- [ ] An ordinary application window covers the widget.
- [ ] Widget never becomes always-on-top.
- [ ] Win+D leaves the widget in a usable desktop state.
- [ ] Restarting Windows Explorer reconnects the widget within 5 seconds.
- [ ] Forced desktop-host failure shows a normal non-topmost fallback window.

## Window and Display Behavior

- [ ] Widget can move and resize at 100% DPI.
- [ ] Widget can move and resize at 150% DPI.
- [ ] Moving the widget to a second monitor keeps it visible.
- [ ] Disconnecting the saved monitor moves the widget to the primary work area.
- [ ] Sleep and resume leave the widget visible and interactive.

## Gate Decision

- [x] All automated performance gates pass with the approved 180 MB working-set limit.
- [ ] All required manual checks pass on every required environment.
- [x] The packaging decision is recorded in `desktop-prototype-results.md`.
- [ ] WPF and the selected packaging mode are approved for phase 1.

## Current Validation Environment

The available machine is Windows 11 Pro x64, build 26200. The prototype launched, but its desktop-hosted window was not exposed as a targetable window to the Windows automation layer. No required manual checkbox is marked from that observation alone. Windows 10 22H2 and the second Windows 11 validation environment remain pending.
