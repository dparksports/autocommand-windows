# AutoCommand v2026.10.29

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **Defender CPU-cap now works on Tamper-Protected builds.** On recent Windows builds, Tamper Protection blocks `Set-MpPreference -ScanAvgCPULoadFactor` even from an elevated process ("The requested operation is not supported") — which made the 🐢 Gentle preset fail with a refusal dialog in v2026.10.28. The tab now falls back automatically to the Group Policy value the "Specify the maximum percentage of CPU utilization during a scan" ADMX writes (`HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Scan\AvgCpuLoadFactor`), which Tamper Protection does not police: direct write first, policy fallback on refusal, no dialog, no dead end.
- **The tab tells you which knob owns the cap.** When the policy route wins, the status line says the cap was applied via the Defender scan policy and that Defender picks it up on its next config refresh; the slider holds your requested value instead of snapping back to a stale preference read. Whenever a policy value exists, the status line shows "scan CPU cap is policy-set: N%" and the slider prefers it.
- **Reverting is explicit:** drag the slider back in the app (it rewrites the policy value), or delete it elevated: `Remove-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Scan' -Name AvgCpuLoadFactor`.
- Both failure modes stay honest: if the direct write and the policy fallback are both refused (e.g. real group policy manages the machine), the explanation dialog appears and the UI snaps back to what Defender actually reports.

> **Note:** run elevated as usual — the policy fallback needs the same admin rights the app already requests.
