# AutoCommand v2026.10.20.2

New WATCH tab: Transmissions. Every TLS conversation this machine has with the
cloud becomes a readable row - what was sent, to whom, by which process - using
the SSL session keys that key-honoring apps can be asked to write. This is the
tab that answers "what exactly left my machine?" without guessing from byte
counts.

## The Transmissions tab (always visible, no opt-in)

- **Each finalized TLS flow becomes a row**: time, pid and process (Sysmon EID 3
  attribution), the SNI hostname, the destination, the RDAP provider behind the
  IP (Alibaba, Microsoft, Akamai, ...), wire and decrypted byte counts, and a
  status. Flows that decrypted say "decrypted"; the rest say exactly why not -
  "encrypted (no keys)" most commonly. Uploads above 10 KB wear an amber badge,
  and the grid defaults to that lens because bulk uploads are the exfiltration
  shape that matters.
- **Message-by-message content viewing**: decrypted directions are decoded into
  HTTP messages - chunked transfer, gzip, brotli and deflate bodies included -
  viewable as pretty (JSON pretty-printed, form fields URL-decoded), text, hex
  or headers, with a per-flow SHA-256 and a save-body button. HTTP/2 streams
  are detected and shown as raw bytes with an honest label rather than
  mis-rendered.
- **Nothing truncated, everything local**: rows archive to
  %LOCALAPPDATA%\AutoCommand\transmissions as a daily greppable JSONL index
  plus full-body blobs, 30-day retention with a 256 MB bodies budget. The
  SHA-256 per row keeps "what was sent" provable after the fact. Nothing leaves
  the machine: the AI bridge exposes metadata only (new read-only
  get_transmissions MCP tool), never bodies.

## Getting keys, two ways

- **System-wide key capture** (opt-in checkbox, confirm dialog): sets
  SSLKEYLOGFILE in the user environment pointing at AutoCommand's existing
  user-only-ACL, rotated keylog, and broadcasts the change so newly launched
  apps inherit it. Chromium/Electron browsers and apps, curl, git, wget,
  OpenSSL- and Python-based tools honor it from their next launch on.
  Schannel-based Windows components ignore it - their flows keep the truthful
  "encrypted (no keys)" status with full metadata. Turning it off wipes the
  key file.
- **Launch under watch**: pick one program; AutoCommand starts it with
  SSLKEYLOGFILE set in its environment only - the precise instrument for a
  single suspect app.

## Engineering notes

- The pipeline reuses the existing raw-socket TCP reassembler and TLS
  decryptor, and adds an all-process Sysmon connect watcher for pid
  attribution, a bounded background queue (drop-counted if capture ever
  outpaces decryption), and a 96 MB in-memory body cache with archive
  fallback for instant viewing.
- Recorder rule: watching starts on first open of the tab and deliberately
  survives tab switches; it stops on Stop Watch or app exit, where the
  pipeline drains what the capture's final flush finalized.
