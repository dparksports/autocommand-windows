# AutoCommand v2026.10.24

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **Quick Profiles now say what they do.** The Firewall tab's profile names describe exactly what each one allows or blocks — no more guessing what "Gaming" or "Public Strict" actually toggle:
  - **Shields Up** — reset to the Windows default rule set, then disable every rule (introduced in v2026.10.23 as "Default Lockdown").
  - **Shields Up + mDNS** — disable every grouped rule, then re-enable only mDNS and Core Networking (formerly "Shield Up").
  - **Allow Discovery + Cast** (was Gaming) · **Allow Sharing + Discovery** (was Home) · **Allow Sharing + Discovery + Remote Desktop** (was Office) · **Block Sharing + Discovery + Remote Desktop** (was Public Strict — the exact mirror of the Office profile: same three groups, opposite direction).
- **The Shields Up warning is now technically accurate.** It no longer claims to cut off "all network and Internet access": disabling every rule blocks all unsolicited incoming traffic, but outbound traffic may still flow depending on your firewall profile's default actions.
- **Tooltips where they were missing.** "Shields Up + mDNS" now documents its whitelist (mDNS + Core Networking), and the Shields Up tooltip matches the corrected wording. The profile combo widened so the longest names render unclipped.
- **Nothing behavioral changed.** Same six profiles, same toggles, same drift enforcement — names and warning text only. The Fresh Setup firewall step follows the new naming. No new dependencies.

> **Note:** run elevated as usual — the profile engine drives the same `INetFwPolicy2` COM API as before.
