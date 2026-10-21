# AutoCommand v2026.10.20.4

Follow-up to v2026.10.20.2's Transmissions tab. That tab could show what apps
sent only when TLS session keys existed - and the "encrypted (no keys)" dead
end asked the user to fix it by hand. This release automates the whole
remediation loop: detection now comes to you, and the fix is one click.

## Every big encrypted upload now announces itself

- The moment any app pushes more than 10 KB over TLS without having written
  session keys, a Windows toast fires: "<app> sent 4.2 MB - encrypted, no
  keys", with the destination, SNI host and RDAP provider. Clicking it jumps
  straight to the Transmissions tab. Deliberately unthrottled - every
  occurrence, no pacing - because the ignore list (below) is the pressure
  valve, not a timer.
- AutoCommand itself never toasts about its own traffic.

## One-click "Restart under watch"

- On any encrypted row (and from the new Apps-without-keys popup), Restart
  under watch recovers the exact command line and working directory for that
  PID from the Sysmon archive - PID-reuse-aware, so a recycled PID cannot
  attach the wrong command line - then confirms, terminates ALL running
  instances of the image (browsers and Electron apps otherwise forward the
  relaunch to a surviving instance and inherit nothing), and relaunches it
  with key capture applied. Its very next TLS flow arrives decrypted.
- The offer is refused honestly where it cannot work, with the reason shown:
  AutoCommand itself, Windows system components (Schannel never writes keys -
  metadata is the ceiling there), and Store/UWP apps (Windows starts those
  through app activation).

## The ignore list, and the surfaces that respect it

- Ignoring an app silences its toasts and suggestions only - its rows stay in
  the grid, because the feed stays truthful. The list persists as
  ignored-apps.json beside the archive, editable from the new popup.
- "Apps without keys" popup (toolbar button, live count): per-image
  encrypted-vs-decrypted flow counts, each row with Restart-under-watch and
  Ignore actions, the ignore-list manager, and the system key capture
  shortcut.
- The status bar grows a "Turn on key capture" button that appears exactly
  while watching with system-wide capture off - the cost of it being off is
  now visible where you are looking, not after the fact.

## Engineering notes

- TrayNotifier gained a per-toast activation callback (existing callers
  unchanged); transmissions toasts select the tab on activation instead of
  just raising the window.
- The pipeline tracks per-image stats and raises one event per big encrypted
  upload with no pacing by design; the ignore-list filter lives on the
  consumer, so the pipeline reports and the UI decides.
