using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AutoCommand.Models;

namespace AutoCommand.Services
{
    /// <summary>
    /// Persistence for trace projects: one JSON file per project under
    /// %LOCALAPPDATA%\AutoCommand\traces\projects. Files are the interface —
    /// hand-edits and dropped-in files are picked up by the watcher, and a
    /// project definition is exactly the text of its file (that's the
    /// "share" story: copy the file, paste it on another machine).
    /// </summary>
    public static class TraceProjectStore
    {
        public static string ProjectsDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCommand", "traces", "projects");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static List<TraceProject> Load()
        {
            var result = new List<TraceProject>();
            try
            {
                if (!Directory.Exists(ProjectsDirectory)) return result;
                foreach (var file in Directory.GetFiles(ProjectsDirectory, "*.json")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var p = JsonSerializer.Deserialize<TraceProject>(
                            File.ReadAllText(file), JsonOpts);
                        if (p != null && !string.IsNullOrWhiteSpace(p.Name)) result.Add(p);
                    }
                    catch { /* one unreadable file must not take the tab down */ }
                }
            }
            catch { /* unreadable dir — empty list is honest */ }
            return result;
        }

        private static string PathFor(string name) =>
            Path.Combine(ProjectsDirectory, SafeFileName(name) + ".json");

        /// <summary>File-system-safe stem for a project name (letters,
        /// digits, space, dash, underscore; everything else becomes '-').</summary>
        public static string SafeFileName(string name)
        {
            var chars = (name ?? "project").Trim().Select(c =>
                char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' ? c : '-');
            var stem = new string(chars.ToArray()).Trim();
            return stem.Length == 0 ? "project" : stem;
        }

        public static void Save(TraceProject project)
        {
            Directory.CreateDirectory(ProjectsDirectory);
            File.WriteAllText(PathFor(project.Name),
                JsonSerializer.Serialize(project, JsonOpts));
        }

        public static bool Delete(string name)
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>Creates the projects directory with a seed project the
        /// first time the Traces tab opens, so the tab never greets with an
        /// empty list.</summary>
        public static void EnsureSeeded()
        {
            try
            {
                if (Directory.Exists(ProjectsDirectory) &&
                    Directory.GetFiles(ProjectsDirectory, "*.json").Length > 0) return;
                Save(new TraceProject
                {
                    Name = "svchost — all services",
                    MatchProcess = "svchost.exe",
                    ServiceKey = "",
                    Notes = "Starter project: every svchost instance together. For the deep " +
                            "1-second capture use the Svchost Trace tab; for one service, make a " +
                            "copy of this project and set its service key (e.g. diagtrack).",
                });
            }
            catch { /* seeding is cosmetic — never fatal */ }
        }
    }
}
