// ProxyInstallSelfTest — drives McpClientRegistration's install decision
// logic (the versioned-refresh + lock + fallback rules) against synthetic
// directories, no WPF and no elevation needed.
//
//   dotnet run --project ProxyInstallSelfTest
//
// Scenarios (I2 acceptance):
//   1. upgrade:     a complete OLD install exists, a NEW source appears →
//                   installs v<new>, returns the new path, old folder is
//                   cleaned when unlocked, kept when locked
//   2. lock:        the source exe is held open → install fails, the
//                   previously-good path is kept and reported, a dev path is
//                   never registered
//   3. no source:   legacy flat set only → legacy path returned unchanged
//   4. nothing:     no source, no prior install → null
using System.Diagnostics;
using AutoCommand.Services;

int failures = 0;
void Check(string name, bool pass, string detail)
{
    Console.WriteLine((pass ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " — " + detail : ""));
    if (!pass) failures++;
}

string root = Path.Combine(Path.GetTempPath(), "autocommand-proxyinstall-selftest-" + Guid.NewGuid().ToString("N")[..8]);
string tools = Path.Combine(root, "tools");
Directory.CreateDirectory(tools);
try
{
    // ── helpers ──────────────────────────────────────────────────────────
    void MakeSource(string dir, string marker)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "AutoCommand.AgentProxy.exe"), "fake exe " + marker);
        File.WriteAllText(Path.Combine(dir, "coreclr.dll"), "fake runtime " + marker);
    }
    void MakeLegacyTools()
    {
        foreach (string f in new[] { "AutoCommand.AgentProxy.exe", "AutoCommand.AgentProxy.dll",
                                     "AutoCommand.AgentProxy.runtimeconfig.json", "AutoCommand.AgentProxy.deps.json" })
            File.WriteAllText(Path.Combine(tools, f), "legacy " + f);
    }

    // ── 1. upgrade: stale complete install + new source → refresh ────────
    MakeLegacyTools();                                   // the old flat install (complete)
    string srcOld = Path.Combine(root, "src-old"); MakeSource(srcOld, "old");
    string srcNew = Path.Combine(root, "src-new"); MakeSource(srcNew, "new");
    var (p1, m1) = McpClientCatalog.ResolveAndInstallProxy("2026.10.16", tools, new[] { srcOld });
    Check("1a upgrade installs new version", p1 == Path.Combine(tools, "v2026.10.16", "AutoCommand.AgentProxy.exe"), m1);
    Check("1b new bits actually copied",
          File.ReadAllText(p1!).Contains("old"), "source content came from src-old");
    Check("1c marker written", File.Exists(Path.Combine(tools, "v2026.10.16", "proxy.installed")), "");
    Check("1d legacy flat set cleaned (unlocked)",
          !File.Exists(Path.Combine(tools, "AutoCommand.AgentProxy.dll")), "");

    // idempotent: second call with same version → same path, no reinstall
    var (p1b, m1b) = McpClientCatalog.ResolveAndInstallProxy("2026.10.16", tools, new[] { srcNew });
    Check("1e same version → same path", p1b == p1, m1b);
    Check("1f v2026.10.16 content NOT refreshed by same-version call",
          File.ReadAllText(p1b!).Contains("old"), "still the old bits — refresh only on version change");

    // ── 2. locked old install: a running proxy holds v2026.10.16 open ────
    string lockedExe = p1!;
    using (var lockStream = new FileStream(lockedExe, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        var (p2, m2) = McpClientCatalog.ResolveAndInstallProxy("2026.10.17", tools, new[] { srcNew });
        Check("2a next version installs alongside the locked one",
              p2 == Path.Combine(tools, "v2026.10.17", "AutoCommand.AgentProxy.exe"), m2);
        Check("2b locked old folder survives for its running proxy", File.Exists(lockedExe), "");
        Check("2c new install carries new bits", File.ReadAllText(p2!).Contains("new"), "");
    }
    // lock released → a later install cleans the stale folder
    _ = McpClientCatalog.ResolveAndInstallProxy("2026.10.18", tools, new[] { srcNew });
    Check("2d stale folder cleaned once unlocked",
          !Directory.Exists(Path.Combine(tools, "v2026.10.17")), "");

    // ── 3. every source locked → keep the previously-good path ───────────
    string keepExe = Path.Combine(tools, "v2026.10.18", "AutoCommand.AgentProxy.exe");
    using (var srcLock = new FileStream(Path.Combine(srcNew, "AutoCommand.AgentProxy.exe"),
                                        FileMode.Open, FileAccess.Read, FileShare.None)) // deny-all: a real lock
    {
        var (p3, m3) = McpClientCatalog.ResolveAndInstallProxy("2026.10.19", tools, new[] { srcNew });
        Check("3a locked source → falls back to newest good install", p3 == keepExe, m3);
        Check("3b failure is reported, not swallowed", m3.Contains("keeping"), m3);
    }
    Check("3c v2026.10.19 was not half-created", !Directory.Exists(Path.Combine(tools, "v2026.10.19")), "");

    // ── 4. no source anywhere, legacy only → legacy path ─────────────────
    string tools2 = Path.Combine(root, "tools2");
    Directory.CreateDirectory(tools2);
    foreach (string f in new[] { "AutoCommand.AgentProxy.exe", "AutoCommand.AgentProxy.dll",
                                 "AutoCommand.AgentProxy.runtimeconfig.json", "AutoCommand.AgentProxy.deps.json" })
        File.WriteAllText(Path.Combine(tools2, f), "legacy " + f);
    var (p4, m4) = McpClientCatalog.ResolveAndInstallProxy("2026.10.16", tools2, Array.Empty<string>());
    Check("4a no source → legacy flat install kept",
          p4 == Path.Combine(tools2, "AutoCommand.AgentProxy.exe"), m4);

    // ── 5. nothing at all → null, with a diagnostic message ──────────────
    var (p5, m5) = McpClientCatalog.ResolveAndInstallProxy("2026.10.16", Path.Combine(root, "tools3"),
                                                            Array.Empty<string>());
    Check("5a no source, no prior install → null", p5 == null, m5);
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

Console.WriteLine(failures == 0 ? "PROXY INSTALL SELF-TEST PASSED" : $"PROXY INSTALL SELF-TEST FAILED ({failures})");
return failures == 0 ? 0 : 1;
