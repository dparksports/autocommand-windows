using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoCommand.Services
{
    /// <summary>
    /// Weighs the passive fingerprint and the active probe report into one
    /// plain-English verdict, in the app's voice: a headline the user can
    /// act on, the math behind it, one reversible next step — and never a
    /// conclusion the evidence doesn't support. Rule order matters: a block
    /// outranks everything; a handshake drop outranks steering; "far away"
    /// outranks "healthy"; and when nothing fired, the verdict says so
    /// honestly instead of inventing one.
    ///
    /// Every rule below maps to a failure observed in the wild, most while
    /// diagnosing a real "this site refreshes slowly" report.
    /// </summary>
    public static class SiteVerdictService
    {
        public enum VerdictKind
        {
            Blocked,             // nothing reaches the site from this PC
            LocalTlsBroken,      // every handshake fails, controls included — the PC's stack
            HandshakeDropping,   // servers accept TCP but drop some TLS hellos
            Steering,            // short-TTL DNS roulette across uneven servers
            FarAway,             // handshakes complete but the path is slow
            Healthy,             // no network problem found from here
            Insufficient         // not enough evidence to say anything
        }

        public sealed class Verdict
        {
            public VerdictKind Kind { get; set; }
            /// <summary>One sentence, plain English, the answer first.</summary>
            public string Headline { get; set; }
            /// <summary>The math, one line per measured fact.</summary>
            public List<string> Evidence { get; set; } = new();
            /// <summary>One reversible suggestion; never applied by this engine.</summary>
            public string NextStep { get; set; }
        }

        /// <summary>
        /// Decide from whatever evidence exists. Either input may be null
        /// (passive-only or probe-only runs degrade, never guess).
        /// </summary>
        public static Verdict Decide(
            SiteFingerprintService.SiteFingerprint fingerprint,
            SiteProbeService.SiteProbeReport report)
        {
            var evidence = new List<string>();

            // Fingerprint lines are evidence no matter which rule fires.
            if (fingerprint?.LooksStruggling == true)
                evidence.AddRange(fingerprint.Signals);

            if (report != null)
                evidence.AddRange(ExtractProbeEvidence(report));

            Verdict verdict = null;

            if (report != null)
            {
                verdict ??= RuleBlocked(report);
                verdict ??= RuleLocalTlsBroken(report);
                verdict ??= RuleHandshakeDropping(report);
                verdict ??= RuleSteering(report, fingerprint);
                verdict ??= RuleFarAway(report);
                verdict ??= RuleHealthy(report, fingerprint);
            }

            if (verdict == null)
            {
                // Probe-only said nothing conclusive; see what the archive says.
                if (fingerprint?.LooksStruggling == true)
                {
                    verdict = new Verdict
                    {
                        Kind = VerdictKind.Steering,
                        Headline =
                            "Recorded traffic shows this site behaving unevenly — bursts and address-hopping — " +
                            "but only the live 60-second check can say why. Run it while the site is being slow.",
                        NextStep = "Run the check while the page is misbehaving; the verdict is only as good as the moment it measures."
                    };
                }
                else
                {
                    verdict = new Verdict
                    {
                        Kind = VerdictKind.Insufficient,
                        Headline =
                            "Not enough evidence yet — no recorded conversations for this site and no probe run. " +
                            "Browse the site once, then run the check.",
                        NextStep = "Visit the site in the browser first so there's traffic to reason about."
                    };
                }
            }

            verdict.Evidence = evidence.Count > 0 ? evidence : new List<string> { "No measurements recorded." };
            return verdict;
        }

        // ── rules, in priority order ─────────────────────────────────────

        private static Verdict RuleBlocked(SiteProbeService.SiteProbeReport r)
        {
            // DNS answered but every TCP connect failed: packets leave the
            // browser and nothing comes back — the block / sinkhole shape.
            var tcpFailed = r.Edges.Where(e => e.TcpConnectMs == null).ToList();
            if (r.Edges.Count > 0 && tcpFailed.Count == r.Edges.Count)
                return new Verdict
                {
                    Kind = VerdictKind.Blocked,
                    Headline =
                        "Nothing can reach this site from your PC — the server addresses answer DNS but refuse " +
                        "every connection. A block rule may be why.",
                    NextStep =
                        "Open the Blocks tab and look for these addresses (or press 🚑 Restore Internet — one click, fully reversible)."
                };

            // DNS itself never answered: sinkhole or dead name.
            if (!r.Dns.AnySuccess && r.Edges.Count == 0)
                return new Verdict
                {
                    Kind = VerdictKind.Blocked,
                    Headline =
                        "This site's name never resolves from your PC — either the name is dead or something local " +
                        "is answering DNS for it (a sinkhole or hosts entry).",
                    NextStep =
                        "Check the Black Hole tab and the Windows hosts file; both are one-glance checks, and anything you find there is reversible."
                };

            return null;
        }

        private static Verdict RuleLocalTlsBroken(SiteProbeService.SiteProbeReport r)
        {
            // TCP reaches the site, every handshake fails — and the CONTROL
            // sites fail the same way. Same machine, same stack, same
            // failure everywhere means the local TLS stack, not the site.
            // (Observed in the wild: OS hardening that leaves schannel
            // unable to create ephemeral keys — some apps' TLS still works,
            // which makes it look exactly like "this one site is broken".)
            if (r.Controls.Count == 0 || r.Controls.Any(c => c.TlsMs.HasValue)) return null;

            var tcpOk = r.Edges.Where(e => e.TcpConnectMs.HasValue).ToList();
            if (tcpOk.Count == 0 || !tcpOk.All(e => Failed(e.TlsDefaultMs) && Failed(e.TlsPlainMs)))
                return null;

            return new Verdict
            {
                Kind = VerdictKind.LocalTlsBroken,
                Headline =
                    "Every secure handshake fails — including to your control sites, whose servers are known-good. " +
                    "The connection layer on THIS PC is the problem (TLS settings or hardening), not the site.",
                NextStep =
                    "Recent OS 'TLS hardening' changes are the usual suspect — if you ran any, undo them and retest. " +
                    "Sites may still load in some browsers (they carry their own TLS), which makes this look like a one-site fault."
            };
        }

        private static Verdict RuleHandshakeDropping(SiteProbeService.SiteProbeReport r)
        {
            // The smoking gun: TCP completes, one hello shape is silently
            // dropped, the other succeeds — on the same address, same minute.
            var splitEdges = r.Edges.Where(e =>
                e.TcpConnectMs.HasValue &&
                (Failed(e.TlsDefaultMs) != Failed(e.TlsPlainMs))).ToList();

            if (splitEdges.Count > 0)
            {
                bool defaultDropped = splitEdges.Any(e => Failed(e.TlsDefaultMs) && !Failed(e.TlsPlainMs));
                string shape = defaultDropped ? "the Windows-default handshake" : "the plain handshake";
                return new Verdict
                {
                    Kind = VerdictKind.HandshakeDropping,
                    Headline =
                        $"This site's servers accept the connection, then silently drop {shape} while accepting the other — " +
                        "a server-side quirk, not your PC. A browser that speaks the dropped flavor stalls for seconds " +
                        "before retrying somewhere else, which is exactly what a slow refresh feels like.",
                    NextStep =
                        "Report it to the site (their CDN is mishandling some modern TLS hellos). To test from your side: " +
                        "in Chrome, visit chrome://flags/#enable-post-quantum-kyber, set it to Disabled, relaunch — " +
                        "if refreshes snap to fast, the dropped hello was the post-quantum keyshare; the flag is one click to restore."
                };
            }

            // Weaker form: edges that dropped BOTH shapes after a good TCP.
            // Conclusive on its own from 2+ edges; a single edge needs a
            // healthy control to rule out the local machine.
            var tcpOk = r.Edges.Where(e => e.TcpConnectMs.HasValue).ToList();
            bool controlHealthy = r.Controls.Any(c => c.TlsMs.HasValue);
            if (tcpOk.Count >= 1 && tcpOk.All(e => Failed(e.TlsDefaultMs) && Failed(e.TlsPlainMs)) &&
                (tcpOk.Count >= 2 || controlHealthy))
                return new Verdict
                {
                    Kind = VerdictKind.HandshakeDropping,
                    Headline =
                        "This site's servers accept connections but never answer any secure handshake this app can " +
                        "send — while your control sites answer instantly. The servers are the problem.",
                    NextStep =
                        "Nothing to fix on your PC. If the site loads in some apps and not others, that difference " +
                        "itself is the report to send the site: their servers are dropping handshakes selectively."
                };

            return null;
        }

        private static Verdict RuleSteering(SiteProbeService.SiteProbeReport r,
            SiteFingerprintService.SiteFingerprint fp)
        {
            bool rotating = r.Dns.Samples.Count >= 2 && r.Dns.AnswerRotated;
            bool dispersion = fp?.LooksStruggling == true &&
                              fp.Edges.Count >= SiteFingerprintService.EdgeDispersionFloor;
            if (!rotating && !dispersion) return null;

            string dice = rotating
                ? $"DNS handed back a different server set between samples ({r.Dns.DistinctIps.Count} addresses seen across {r.Dns.Samples.Count} looks)"
                : "recorded traffic shows the site hopping across many server addresses";
            return new Verdict
            {
                Kind = VerdictKind.Steering,
                Headline =
                    $"This site rotates its servers constantly — {dice}. Every visit rolls the dice on which server " +
                    "answers, and today some of them are bad ones. Your line is fine (the controls say so).",
                NextStep =
                    "Retry usually lands on a healthy server within a few refreshes. It's the site's steering that " +
                    "needs fixing, not your PC — worth reporting to them."
            };
        }

        private static Verdict RuleFarAway(SiteProbeService.SiteProbeReport r)
        {
            double? controlMs = ControlBaselineMs(r);
            var ok = r.Edges.Where(e => e.TlsDefaultMs.HasValue || e.TlsPlainMs.HasValue).ToList();
            if (ok.Count == 0) return null;

            double worst = ok.Max(e => e.TlsDefaultMs ?? e.TlsPlainMs ?? 0);
            if (controlMs.HasValue && worst > 4 * controlMs.Value && worst > 150)
                return new Verdict
                {
                    Kind = VerdictKind.FarAway,
                    Headline =
                        $"Handshakes complete but slowly — up to {worst:0} ms to this site versus {controlMs:0} ms for " +
                        "your control sites. Parts of this site are served from far away; every request pays the distance.",
                    NextStep =
                        "Nothing local to fix — distance is physics. A VPN endpoint nearer the site's servers is the " +
                        "only lever that moves this number."
                };
            if (!controlMs.HasValue && worst > 400)
                return new Verdict
                {
                    Kind = VerdictKind.FarAway,
                    Headline =
                        $"Handshakes take up to {worst:0} ms — slow even without a control to compare against. " +
                        "The site's servers are answering from far away or under load.",
                    NextStep = "Nothing local to fix; the distance or the load is the site's, not yours."
                };
            return null;
        }

        private static Verdict RuleHealthy(SiteProbeService.SiteProbeReport r,
            SiteFingerprintService.SiteFingerprint fp)
        {
            double? controlMs = ControlBaselineMs(r);
            var bar = Math.Max(controlMs ?? 60, 60) * 2.5;
            bool allFast = r.Edges.Count > 0 && r.Edges
                .Where(e => e.TcpConnectMs.HasValue)
                .All(e => (e.TlsDefaultMs ?? e.TlsPlainMs ?? int.MaxValue) <= bar);
            bool calmHistory = fp?.LooksStruggling != true;

            if (!allFast) return null;

            return new Verdict
            {
                Kind = VerdictKind.Healthy,
                Headline = calmHistory
                    ? "No network problem found from here — connections and handshakes to this site are as fast as " +
                      "your control sites, and its recorded traffic looks calm. The slowness you're seeing is inside " +
                      "the browser or the site's own application."
                    : "Connections are fast right now, but the recorded traffic shows earlier bursts and address-hopping — " +
                      "the problem comes and goes with which server the dice roll lands on.",
                NextStep = calmHistory
                    ? "Try the page in a private window with extensions off; if it's still slow while this tab says " +
                      "the network is fine, the site's own app is the suspect."
                    : "Run the check again the next time it's slow — an intermittent fault has to be caught in the act."
            };
        }

        // ── helpers ───────────────────────────────────────────────────────

        private static bool Failed(double? ms) => !ms.HasValue;

        private static double? ControlBaselineMs(SiteProbeService.SiteProbeReport r)
        {
            var good = r.Controls.Where(c => c.TlsMs.HasValue).ToList();
            return good.Count > 0 ? good.Min(c => c.TlsMs.Value) : (double?)null;
        }

        private static List<string> ExtractProbeEvidence(SiteProbeService.SiteProbeReport r)
        {
            var lines = new List<string>();
            foreach (var e in r.Edges)
            {
                string tcp = e.TcpConnectMs.HasValue ? $"TCP {e.TcpConnectMs:0} ms" : $"TCP FAILED ({e.TcpError})";
                string tls = Describe(e.TlsDefaultMs, e.TlsDefaultVersion, e.TlsDefaultError, "default");
                string plain = Describe(e.TlsPlainMs, e.TlsPlainVersion, e.TlsPlainError, "plain");
                lines.Add($"{e.Ip}: {tcp} · {tls} · {plain}");
            }
            foreach (var c in r.Controls)
                lines.Add(c.TlsMs.HasValue
                    ? $"control {c.Host}: TCP {c.TcpConnectMs:0} ms, TLS {c.TlsMs:0} ms"
                    : $"control {c.Host}: FAILED ({c.Error})");
            return lines;
        }

        private static string Describe(double? ms, string version, string error, string shape) =>
            ms.HasValue
                ? $"{shape} hello → {version} in {ms:0} ms"
                : $"{shape} hello → NO ANSWER ({FirstWords(error, 48)})";

        private static string FirstWords(string s, int max) =>
            string.IsNullOrEmpty(s) ? "timeout" : (s.Length <= max ? s : s[..max] + "…");
    }
}
