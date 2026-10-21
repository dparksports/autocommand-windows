# AutoCommand v2026.10.20.6

Layer 3 for the Transmissions tab: an intercept for apps that can NEVER write
TLS session keys - Schannel-based Windows system components (svchost services,
taskhostw, conhost-hosted injected code), pinned or exotic clients. Until now
those rows ended at "encrypted (no keys)" with metadata only; this release
gives them a one-click, time-boxed, fully reversible decryption path.

## Why driverless, unlike the open-source interceptors

mitmproxy's local:<pid> capture and similar tools redirect traffic with
WinDivert - a kernel driver that on Windows 11 26H2 fights memory integrity
(HVCI), Smart App Control enforcement, and Microsoft's 2026 removal of
cross-signed-driver trust. AutoCommand takes the route that reaches exactly
the target set this feature exists for, because Windows services route
through WinHTTP: a local loopback proxy installed as the temporary system
proxy. No driver, no SAC exceptions, works everywhere.

## The intercept session (15 minutes, auto-reverting)

- Every accepted connection is attributed to its owning PID through the
  kernel TCP table. The TARGET pid's connections are decrypted - leaf
  certificate minted per hostname from a per-install "AutoCommand Intercept
  CA", upstream validated for real, both legs TLS, never a downgrade. Every
  other app's connections tunnel through byte-for-byte, untouched.
- The CA is trusted machine-wide only while the session runs, and removed at
  the end. The WinHTTP + WinINET proxy settings and the per-program UDP/443
  block rule (QUIC falls back to TCP/TLS) are journaled BEFORE installation;
  a crash leaves the journal, and the next app start undoes everything it
  describes. App exit also stops the session.
- Certificate-pinned clients fail loudly - recorded as "refused intercept
  (pinning)", which is itself a detection signal. Intercepted traffic lands
  in the same Transmissions grid, tagged "decrypted - intercept", with the
  full archive treatment: message decode, SHA-256, >10KB lens, body viewer.
- Strictly observe-only: bytes are teed and recorded, never modified in
  transit.

## UX

- Encrypted rows gain "Intercept this process..." with a consent dialog that
  states exactly what will happen and how it reverts. An amber banner counts
  down the session with a Stop-now button.
- The system-component restart-block message now points at the intercept
  instead of claiming metadata is the ceiling.

## Known limits (honest ones)

- Apps that ignore both WinHTTP and WinINET proxy settings (raw sockets)
  bypass the intercept - the WinDivert transparent route remains a possible
  future escape hatch for those, with the driver-signing caveats above.
- HTTP/2 bodies are detected and shown as raw bytes (no HPACK demux yet),
  same as the keylog path.
