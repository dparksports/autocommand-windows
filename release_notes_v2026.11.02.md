# AutoCommand v2026.11.02

Self-contained Windows build — **no .NET installation required**. Unzip and run `AutoCommand.exe` as Administrator.

## Highlights

- **No more false "Orphaned" verdicts on inbox tasks.** A handler DLL that fails to resolve through classic COM registration is only the signature of uninstalled-software leftovers for third-party tasks; built-in Windows tasks also resolve their handlers through stores the classic HKCR lookup does not cover (packaged COM and friends) and run fine "unregistered". Inbox tasks now keep their **Inbox** verdict with an unresolved handler instead of being mislabeled **Orphaned** — spotted in the wild on `\Microsoft\Windows\LanguageComponentsInstaller\ReconcileLanguageResources`, whose CLSID is registered nowhere in HKCR yet runs daily with return code 0.
- **TaskAttributionSelfTest — offline verification harness for the attribution engine.** The same service sources the app ships are compiled into a standalone console that replays the last hours of Sysmon + Task Scheduler history and prints every taskhostw launch with its stitched attribution: task, trigger reason, return code, duration, verdict, handler DLL, and the known-task explanation when the catalog has one. Non-elevated runs automatically fall back to the scheduler-only replay (the Sysmon channel is admin-only), which exercises exactly the GUID-less attribution path — event 129 attributes by PID with no Sysmon record at all, the case `taskhostw.exe Install $(arg0)` made famous.

> **Note:** run elevated as usual — Sysmon event reading and Task Scheduler history queries need administrator rights. The known-tasks catalog ships at `Assets\known-tasks.json` next to the exe; edit it to grow the corpus.
