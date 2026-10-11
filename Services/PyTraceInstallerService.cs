using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace AutoCommand.Services
{
    /// <summary>
    /// Installs/uninstalls the embedded python audit-hook payload into python
    /// environments, opt-in per environment. The payload is written as a
    /// uniquely-named module + .pth importer into the environment's real
    /// site-packages — never as sitecustomize.py (which may belong to the
    /// user) and never by touching the interpreter binary. Everything written
    /// is tracked in a local manifest so Uninstall removes only our files.
    /// </summary>
    public class PyTraceInstallerService
    {
        public const string ModuleName = "PyTraceHook_ac";
        public static string ManifestPath => Path.Combine(PyTraceReaderService.TraceDir, "instrumented.json");

        private const string PayloadResourceName = "AutoCommand.Assets.PyTraceHook.py";

        public sealed class PythonEnv
        {
            /// <summary>Path whose Lib\site-packages receives the payload.</summary>
            public string SitePackages { get; set; }

            /// <summary>Base of the environment (venv root or interpreter install dir).</summary>
            public string Root { get; set; }

            /// <summary>Where discovery found it: "pep514-hklm", "pep514-hkcu" or "venv".</summary>
            public string Source { get; set; }

            public string Display
            {
                get
                {
                    string kind = Source == "venv" ? "venv" : "installed";
                    return $"[{kind}] {Root}";
                }
            }
        }

        public sealed class ManifestEntry
        {
            [JsonPropertyName("sitePackages")]
            public string SitePackages { get; set; }

            [JsonPropertyName("root")]
            public string Root { get; set; }

            [JsonPropertyName("source")]
            public string Source { get; set; }

            [JsonPropertyName("files")]
            public List<string> Files { get; set; } = new();

            [JsonPropertyName("installedAt")]
            public DateTime InstalledAt { get; set; }
        }

        // -----------------------------------------------------------------------
        // Discovery
        // -----------------------------------------------------------------------

        /// <summary>PEP 514 registered environments plus a probe for python on PATH.</summary>
        public List<PythonEnv> DiscoverEnvironments()
        {
            var envs = new List<PythonEnv>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string root, string source)
            {
                if (string.IsNullOrEmpty(root)) return;
                string sitePackages = Path.Combine(root, "Lib", "site-packages");
                if (!Directory.Exists(sitePackages)) return;
                if (seen.Add(Path.GetFullPath(sitePackages)))
                    envs.Add(new PythonEnv { Root = root, SitePackages = sitePackages, Source = source });
            }

            foreach (var (hive, source) in new[]
            {
                (RegistryHive.LocalMachine, "pep514-hklm"),
                (RegistryHive.CurrentUser, "pep514-hkcu")
            })
            {
                foreach (string vendor in new[] { "PythonCore", "ContinuumAnalytics", "Python" })
                {
                    using var key = RegistryKey.OpenBaseKey(hive, Environment.Is64BitProcess && hive == RegistryHive.LocalMachine
                            ? RegistryView.Registry64 : RegistryView.Default)
                        .OpenSubKey($"SOFTWARE\\Python\\{vendor}");
                    if (key == null) continue;

                    foreach (string tag in key.GetSubKeyNames())
                    {
                        using var installPath = key.OpenSubKey($"{tag}\\InstallPath");
                        // PEP 514: the (default) value of InstallPath is where python lives
                        Add(installPath?.GetValue(null) as string, source);
                    }
                }
            }

            // The PATH python (commonly a venv launcher in active use)
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (string dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string launcher = Path.Combine(dir.Trim(), "python.exe");
                        if (!File.Exists(launcher)) continue;

                        // venv launchers sit in <root>\Scripts; base installs sit
                        // directly in their install dir. pyvenv.cfg marks a venv root.
                        string parent = Path.GetDirectoryName(Path.GetFullPath(dir.Trim()));
                        if (File.Exists(Path.Combine(parent, "pyvenv.cfg")))
                        {
                            Add(parent, "venv");
                            break;
                        }
                        Add(dir.Trim(), "pep514-hklm");
                        break;
                    }
                    catch { }
                }
            }
            catch { }

            return envs;
        }

        /// <summary>Builds an env record for a user-picked venv root folder.</summary>
        public PythonEnv EnvFromVenvRoot(string root)
        {
            if (string.IsNullOrEmpty(root)) return null;
            string sitePackages = Path.Combine(root, "Lib", "site-packages");
            return Directory.Exists(sitePackages)
                ? new PythonEnv { Root = Path.GetFullPath(root), SitePackages = Path.GetFullPath(sitePackages), Source = "venv" }
                : null;
        }

        // -----------------------------------------------------------------------
        // Install / uninstall
        // -----------------------------------------------------------------------

        public bool IsInstalled(string sitePackages)
        {
            return File.Exists(Path.Combine(sitePackages, ModuleName + ".pth")) &&
                   File.Exists(Path.Combine(sitePackages, ModuleName + ".py"));
        }

        /// <summary>Writes payload + .pth into the environment; returns an error string or null.</summary>
        public string Install(PythonEnv env)
        {
            try
            {
                string payload = ReadPayloadText();
                Directory.CreateDirectory(env.SitePackages);
                Directory.CreateDirectory(PyTraceReaderService.TraceDir);
                TlsKeylogService.EnsureHygiene();

                string modulePath = Path.Combine(env.SitePackages, ModuleName + ".py");
                string pthPath = Path.Combine(env.SitePackages, ModuleName + ".pth");
                File.WriteAllText(modulePath, payload);
                // A .pth line starting with "import " executes at site init —
                // this runs the hook before any user code without touching
                // sitecustomize.py.
                File.WriteAllText(pthPath, $"import {ModuleName}" + Environment.NewLine);

                var manifest = LoadManifest();
                var entry = manifest.FirstOrDefault(m =>
                    m.SitePackages.Equals(env.SitePackages, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    entry = new ManifestEntry { SitePackages = env.SitePackages };
                    manifest.Add(entry);
                }
                entry.Root = env.Root;
                entry.Source = env.Source;
                entry.Files = new List<string> { modulePath, pthPath };
                entry.InstalledAt = DateTime.UtcNow;
                SaveManifest(manifest);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Removes only the manifest-listed files; returns an error string or null.</summary>
        public string Uninstall(string sitePackages)
        {
            try
            {
                var manifest = LoadManifest();
                var entry = manifest.FirstOrDefault(m =>
                    m.SitePackages.Equals(sitePackages, StringComparison.OrdinalIgnoreCase));
                if (entry == null) return "Not in the instrumentation manifest.";

                foreach (string file in entry.Files)
                {
                    // Defensive: only ever delete inside the recorded site-packages dir
                    if (!Path.GetFullPath(file).StartsWith(
                            Path.GetFullPath(entry.SitePackages) + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(file)) File.Delete(file);
                }
                manifest.Remove(entry);
                SaveManifest(manifest);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public List<ManifestEntry> LoadManifest()
        {
            try
            {
                if (!File.Exists(ManifestPath)) return new List<ManifestEntry>();
                return JsonSerializer.Deserialize<List<ManifestEntry>>(File.ReadAllText(ManifestPath))
                       ?? new List<ManifestEntry>();
            }
            catch
            {
                return new List<ManifestEntry>();
            }
        }

        private static void SaveManifest(List<ManifestEntry> manifest)
        {
            Directory.CreateDirectory(PyTraceReaderService.TraceDir);
            File.WriteAllText(ManifestPath,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>The embedded payload text (read from assembly resources, never from disk).</summary>
        public static string ReadPayloadText()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(PayloadResourceName);
            if (stream == null)
                throw new InvalidOperationException($"Embedded resource {PayloadResourceName} not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
