# AutoCommand v2026.10.27

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **Traffic Patterns: groups no longer collapse on their own.** The 2-second auto-refresh was regenerating every process group from scratch, which reset all the expanders — anything you opened folded shut within seconds. The grid now uses WPF live sorting/filtering (incremental updates via property change, never a full view refresh), every group starts expanded, and your per-group open/collapsed choice is remembered even when a group's row count changes or containers are recreated while scrolling.
- **Traffic Patterns: honor the ignore list.** The ignore list ("add to ignore" in Process Monitor) was only enforced in the Process Monitor's Sysmon path — the packet-capture pipeline behind Traffic Patterns never consulted it, so ignored processes kept showing up. Finalized flows from ignored processes are no longer aggregated and their existing rows are hidden from the snapshot (history is kept, not deleted; un-ignoring brings rows back within ~30 seconds).
- **Traffic Patterns: cheaper refreshes.** Row change notifications now fire only when a value actually changed, so the live-shaped grid no longer re-evaluates thousands of no-op updates every refresh tick.
- Tab order (Traffic Patterns next to Process Monitor) and the Settings tab layout (Application Information card last) from the previous commit are included in this build.

> **Note:** run elevated as usual — nothing about the capture engine's privileges changed.
