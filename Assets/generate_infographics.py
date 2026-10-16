"""Generate AutoCommand README infographics (dark cyber theme, matplotlib).

Usage:  py Assets/generate_infographics.py
Outputs: Assets/autocommand_infographic.png          (hero)
         Assets/security_stack_infographic.png       (suite at a glance)
         Assets/firewall_profiles_infographic.png    (firewall profiles + pipeline)
         Assets/bloatware_infographic.png            (default apps & bloatware)
         Assets/process_monitor_infographic.png      (process monitor pipeline)
         Assets/fresh_setup_infographic.png          (one-click setup plan)
         Assets/ai_assistant_infographic.png         (AI assistant)
         Assets/bits_infographic.png                 (BITS Transfers tab)
Requires: matplotlib, numpy  (py -m pip install matplotlib numpy)
"""
import os

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import FancyBboxPatch, Circle

matplotlib.rcParams["font.family"] = "DejaVu Sans"

# ---- palette -------------------------------------------------------------
BG_TOP = "#0A1526"
BG_BOT = "#0C2136"
PANEL = "#0F1E33"
PANEL_EDGE = "#2A4A73"
CYAN = "#00E5FF"
CYAN_DIM = "#0E6E8C"
TXT = "#E8F4FF"
SUB = "#8FAECB"
ORANGE = "#FFAE42"
RED = "#FF5A5A"
GREEN = "#3DDC84"
BLUE = "#4FC3F7"
PURPLE = "#B388FF"

W, H = 1600, 1000
VERSION = "v2026.10.17"


def new_canvas():
    fig = plt.figure(figsize=(W / 100, H / 100), dpi=150)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, W)
    ax.set_ylim(0, H)
    ax.invert_yaxis()
    ax.axis("off")
    # vertical gradient background
    grad = np.linspace(0, 1, 256).reshape(-1, 1)
    ax.imshow(grad, extent=[0, W, H, 0], aspect="auto",
              cmap=matplotlib.colors.LinearSegmentedColormap.from_list("bg", [BG_TOP, BG_BOT]), zorder=-2)
    rng = np.random.default_rng(42)
    dots_x, dots_y = rng.uniform(0, W, 90), rng.uniform(0, H, 90)
    ax.scatter(dots_x, dots_y, s=rng.uniform(2, 14, 90), color=CYAN, alpha=rng.uniform(0.04, 0.16, 90), zorder=-1)
    return fig, ax


def panel(ax, x, y, w, h, edge=PANEL_EDGE, lw=1.6, face=PANEL, alpha=0.92):
    p = FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=14",
                       linewidth=lw, edgecolor=edge, facecolor=face, alpha=alpha, zorder=1)
    ax.add_patch(p)
    return p


def topbar(ax, x, y, w, accent, h=8):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=4",
                                linewidth=0, facecolor=accent, zorder=2))


def section_title(ax, x, y, text, color=CYAN):
    ax.text(x, y, text, color=color, fontsize=17, fontweight="bold", va="center")


def header(ax, title, subtitle, version_chip=VERSION):
    panel(ax, 40, 34, W - 80, 108, edge=CYAN_DIM, lw=2.2, alpha=0.95)
    ax.add_patch(FancyBboxPatch((40, 34), 14, 108, boxstyle="round,pad=0,rounding_size=7",
                                linewidth=0, facecolor=CYAN, zorder=2))
    ax.text(84, 72, title, color=TXT, fontsize=24.5, fontweight="bold", va="center")
    ax.text(84, 112, subtitle, color=SUB, fontsize=14.5, va="center")
    ax.text(W - 72, 88, version_chip, color=BG_TOP, fontsize=19, fontweight="bold",
            ha="right", va="center",
            bbox=dict(boxstyle="round,pad=0.5", facecolor=CYAN, edgecolor="none"))


def footer(ax, text, accent=CYAN):
    panel(ax, 40, H - 72, W - 80, 46, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, H - 49, text, color=accent, fontsize=13.5, ha="center", va="center", fontweight="bold")


def arrow_h(ax, x1, x2, y):
    ax.annotate("", xy=(x2, y), xytext=(x1, y),
                arrowprops=dict(arrowstyle="-|>", color=CYAN_DIM, lw=2.4))


# ============================================================ hero
def hero_infographic():
    fig, ax = new_canvas()

    # Brand band
    panel(ax, 40, 40, W - 80, 210, edge=CYAN_DIM, lw=2.4, alpha=0.95)
    ax.add_patch(FancyBboxPatch((40, 40), 16, 210, boxstyle="round,pad=0,rounding_size=8",
                                linewidth=0, facecolor=CYAN, zorder=2))
    ax.text(90, 112, "AUTOCOMMAND", color=TXT, fontsize=48, fontweight="bold", va="center")
    ax.text(92, 166, "Enterprise-grade Windows security, hardening & monitoring — one dashboard",
            color=SUB, fontsize=16.5, va="center")
    ax.text(W - 76, 88, VERSION, color=BG_TOP, fontsize=19, fontweight="bold",
            ha="right", va="center",
            bbox=dict(boxstyle="round,pad=0.5", facecolor=CYAN, edgecolor="none"))
    chips = ["WINDOWS 10 / 11", ".NET 10  •  WPF", "ZERO POWERSHELL", "RUN AS ADMIN"]
    chip_x = W - 76
    for chip in reversed(chips):
        wpx = len(chip) * 8.6 + 40
        ax.text(chip_x - wpx / 2, 214, chip, color=CYAN, fontsize=11.5, fontweight="bold",
                ha="center", va="center",
                bbox=dict(boxstyle="round,pad=0.45", facecolor=PANEL, edgecolor=CYAN_DIM, lw=1.4))
        chip_x -= wpx + 16

    section_title(ax, 47, 294, "EIGHT MODULES  •  ONE DASHBOARD")

    tiles = [
        ("ONE-CLICK SETUP", CYAN,
         "7-step fresh-install plan.\nApplies, then verifies every\nstep against live system\nstate — doubles as a\nstatus dashboard."),
        ("ADVANCED FIREWALL", ORANGE,
         "Native INetFwPolicy2 COM.\nFive one-click profiles,\nper-rule overrides, and\nbaseline drift detection\nafter reboots / updates."),
        ("SECURITY ENFORCER", RED,
         "Event-driven SSTP &\nkernel-debug adapter guard\nwith a self-healing state\nreconciliation sweep and\ntoast alerts."),
        ("PROCESS MONITOR", GREEN,
         "Sysmon + raw-socket\nvisibility: live connections,\nGeo-DNS names, scheduled-\ntask attribution, and one-\nclick block / kill response."),
        ("DEFAULT APPS\n& BLOATWARE", BLUE,
         "Windows Settings parity\napp list, a 23-app one-click\nsweep (OneDrive included),\nand a fully user-editable\nbloatware list."),
        ("UEFI & SECURE BOOT", PURPLE,
         "DBX update pipeline, EFI\nintegrity baselines, boot\nmanager signature checks,\nand verified tool downloads\n(SHA256 + Authenticode)."),
        ("AI ASSISTANT", CYAN,
         "Gemini cloud or fully local\nGGUF models (LLamaSharp).\nEvery generated command is\nshown for review before it\ncan run."),
        ("OS HARDENING", ORANGE,
         "LSA protection, UAC\nenforcement, telemetry off,\nWiFi Direct and hibernation\ntoggles, SetupAPI device\ntakedown — no pnputil."),
    ]

    cw, ch, gap = 366, 270, 14
    x0, y0 = 47, 316
    for i, (name, accent, body) in enumerate(tiles):
        col, row = i % 4, i // 4
        x = x0 + col * (cw + gap)
        y = y0 + row * (ch + 22)
        panel(ax, x, y, cw, ch)
        topbar(ax, x, y, cw, accent)
        ax.text(x + 20, y + 44, name, color=accent, fontsize=16.5 if "\n" not in name else 15,
                fontweight="bold", va="center", linespacing=1.25)
        ax.text(x + 20, y + 92, body, color=SUB, fontsize=12, va="top", linespacing=1.65)

    footer(ax, "WINDOWS 10 / 11   •   .NET 10 (WPF)   •   ZERO-POWERSHELL CORE   •   ADMINISTRATOR PRIVILEGES   •   APACHE-2.0")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "autocommand_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ stack
def stack_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  SECURITY & HARDENING SUITE",
           "Zero-PowerShell monitoring & mitigation  •  WPF dashboard  •  integrated AI co-pilot")

    panels = [
        ("EVENT-DRIVEN ENFORCER", CYAN, [
            "WMI watcher: instant SSTP / kernel-debug",
            "adapter detection (zero idle CPU)",
            "State reconciliation sweep: SSTP service",
            "and BCD kernel-debug state re-asserted",
            "Toast alerts + auto-mitigate toggle",
        ]),
        ("ADVANCED FIREWALL", ORANGE, [
            "5 one-click profiles (Shield Up → Home)",
            "Group enable / disable via native COM",
            "1-click inbound+outbound process block",
            "Live apply progress + per-group report",
            "Config overrides with drift detection",
        ]),
        ("AI SECURITY CO-PILOT", PURPLE, [
            "Dedicated AI Assistant tab",
            "Gemini cloud assistant",
            "Local GGUF models via LLamaSharp",
            "Review-before-run command execution",
            "AI-auditable pages (GetAuditContext)",
        ]),
        ("NETWORK ANALYTICS", GREEN, [
            "Sysmon operational-log EventLogWatcher",
            "Raw-socket packet sniffer",
            "Live connections + Geo-DNS reverse map",
            "Recency-first live-sorted monitor grid",
            "SVCHOST service monitor + DNS service",
        ]),
        ("HARDENING & UEFI", BLUE, [
            "UEFI DBX download / apply + revocation",
            "EFI integrity baseline drift checks",
            "Signature-verified sigcheck auto-download",
            "LSA protection / UAC / telemetry toggles",
            "SetupAPI P/Invoke device takedown",
        ]),
        ("APP & BLOATWARE CONTROL", RED, [
            "Windows Settings parity app list",
            "Deployment-validated signature status",
            "One-click sweep of 23 preinstalled apps",
            "OneDrive via its own uninstaller (Win32)",
            "User-editable list with match preview",
        ]),
    ]

    cw, ch, gap = 489, 300, 22
    x0, y0 = 44, 180
    for i, (title, accent, bullets) in enumerate(panels):
        col, row = i % 3, i // 3
        x = x0 + col * (cw + gap)
        y = y0 + row * (ch + 26)
        panel(ax, x, y, cw, ch)
        topbar(ax, x, y, cw, accent)
        ax.text(x + 24, y + 44, title, color=accent, fontsize=17, fontweight="bold", va="center")
        body = "\n".join("•  " + b for b in bullets)
        ax.text(x + 24, y + 78, body, color=SUB, fontsize=12.4, va="top", linespacing=1.75)

    footer(ax, "WINDOWS 10 / 11   •   .NET 10 (WPF)   •   ADMINISTRATOR PRIVILEGES   •   APACHE-2.0   •   github.com/dparksports/autocommand-windows")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "security_stack_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ firewall
def firewall_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  ADVANCED FIREWALL MANAGEMENT",
           "Five one-click profiles  •  native INetFwPolicy2 COM  •  live progress & completion reporting")

    profiles = [
        ("SHIELD UP", ORANGE, "Paranoid lockdown",
         "Disables every grouped rule,\nre-enables whitelists only:\nmDNS + Core Networking"),
        ("PUBLIC STRICT", RED, "Public Wi-Fi / travel",
         "Disables File & Printer\nSharing, Network Discovery,\nRemote Desktop"),
        ("GAMING", GREEN, "Play & stream",
         "Enables Network Discovery,\nCast to Device functionality\n(stream / cast targets)"),
        ("OFFICE", CYAN, "Workstation on a LAN",
         "Enables File & Printer\nSharing, Network Discovery,\nRemote Desktop"),
        ("HOME", BLUE, "Trusted home network",
         "Enables File & Printer\nSharing, Network Discovery"),
    ]

    cw, ch, gap = 292, 330, 15
    x0 = (W - (cw * 5 + gap * 4)) / 2
    y0 = 192
    section_title(ax, x0, y0 - 16, "QUICK PROFILE PRESETS")
    for i, (name, accent, tag, body) in enumerate(profiles):
        x = x0 + i * (cw + gap)
        panel(ax, x, y0, cw, ch)
        topbar(ax, x, y0, cw, accent)
        ax.add_patch(Circle((x + 38, y0 + 54), 21, linewidth=2.4, edgecolor=accent, facecolor="none", zorder=3))
        ax.text(x + 38, y0 + 54, name[0], color=accent, fontsize=21, fontweight="bold",
                ha="center", va="center", zorder=3)
        ax.text(x + 68, y0 + 47, name, color=TXT, fontsize=16, fontweight="bold", va="center")
        ax.text(x + 68, y0 + 72, tag, color=accent, fontsize=11.5, va="center", style="italic")
        ax.text(x + cw / 2, y0 + 165, body, color=SUB, fontsize=12.2, ha="center", va="center", linespacing=1.7)

    section_title(ax, x0, y0 + ch + 44, "ENGINE PIPELINE  —  WHAT HAPPENS WHEN YOU PICK A PROFILE")

    steps = [
        ("1. ENUMERATE", "Read every rule via\nINetFwPolicy2 COM —\nzero PowerShell", CYAN),
        ("2. RESOLVE", "Translate indirect names:\n@FirewallAPI.dll,-32752\n→ \u201cNetwork Discovery\u201d", CYAN),
        ("3. MATCH", "Case-insensitive match on\nraw + resolved group names\n(never misses a rule)", CYAN),
        ("4. APPLY", "Live per-group progress,\ncontrols locked during\nthe transaction", GREEN),
        ("5. REPORT", "Completion dialog: matched /\nchanged / failed + errors —\nauto-refresh, enabled first", ORANGE),
    ]
    sw, sh, sy = 282, 200, y0 + ch + 66
    sx0 = (W - (sw * 5 + gap * 4)) / 2
    for i, (title, body, accent) in enumerate(steps):
        x = sx0 + i * (sw + gap)
        panel(ax, x, sy, sw, sh, edge=accent if i >= 3 else PANEL_EDGE)
        ax.text(x + 18, sy + 36, title, color=accent, fontsize=14.5, fontweight="bold", va="center")
        ax.text(x + 18, sy + 68, body, color=SUB, fontsize=11.8, va="top", linespacing=1.55)
        if i < 4:
            arrow_h(ax, x + sw + 6, x + sw + gap - 6, sy + sh / 2)

    ax.text(W / 2, sy + sh + 46,
            "Typical run — applying \u201cHome\u201d touches ~84 rules; every matched / changed / failed rule is counted and reported.",
            color=SUB, fontsize=13, ha="center", va="center", style="italic")

    footer(ax, "ZERO-POWERSHELL CORE   •   NATIVE COM (HNetCfg.FwPolicy2)   •   WINDOWS 10 / 11   •   RUN AS ADMINISTRATOR")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "firewall_profiles_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ bloatware
def bloatware_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  DEFAULT APPS & BLOATWARE CONTROL",
           "Windows Settings parity list  •  one-click 23-app sweep  •  fully user-editable list")

    apps = [
        "Outlook", "Xbox", "Family", "Phone Link", "Copilot", "Feedback Hub",
        "Get Help", "Microsoft News", "Teams", "To Do", "Mobile Devices", "Power Automate",
        "Quick Assist", "Solitaire & Casual Games", "Windows Calendar", "Calculator",
        "Sound Recorder", "Web Experience Pack", "Terminal", "Widgets Platform Runtime",
        "Dev Home", "Remote Desktop (Store)", "OneDrive",
    ]

    section_title(ax, 38, 168, "THE 23-APP DEFAULT SWEEP")

    cw, ch, gx, gy = 244, 46, 12, 10
    cols = 6
    x0 = (W - (cw * cols + gx * (cols - 1))) / 2
    y0 = 190
    for i, name in enumerate(apps):
        col, row = i % cols, i // cols
        x = x0 + col * (cw + gx)
        y = y0 + row * (ch + gy)
        p = panel(ax, x, y, cw, ch)
        ax.text(x + cw / 2, y + ch / 2, name, color=TXT, fontsize=11.2,
                ha="center", va="center")
    # 24th cell: your own additions
    x = x0 + 5 * (cw + gx)
    y = y0 + 3 * (ch + gy)
    p = FancyBboxPatch((x, y), cw, ch, boxstyle="round,pad=0,rounding_size=14",
                       linewidth=2.0, edgecolor=GREEN, facecolor=PANEL, alpha=0.92, zorder=1)
    ax.add_patch(p)
    ax.text(x + cw / 2, y + ch / 2, "+  ADD YOUR OWN", color=GREEN, fontsize=11.5,
            fontweight="bold", ha="center", va="center")

    section_title(ax, 44, 448, "THREE WAYS TO EDIT  —  SAVED AS A DELTA IN bloatware_config.json")

    editors = [
        ("ROW TOGGLE", GREEN,
         "Every grid row carries an add /\nopt-out toggle: add the exact\npackage, or stop treating a\ndefault match as bloatware."),
        ("MANAGE LIST DIALOG", CYAN,
         "Defaults on / off, custom exact\npackages, advanced substring\npatterns — with a live match\npreview and a one-click reset."),
        ("DIRECT JSON", PURPLE,
         "Machine-wide config under\nProgramData\\AutoCommand. Delta\nstorage keeps future default\nupdates flowing to you."),
    ]
    ew, eh, egap = 489, 180, 22
    ex0, ey = 44, 470
    for i, (title, accent, body) in enumerate(editors):
        x = ex0 + i * (ew + egap)
        panel(ax, x, ey, ew, eh)
        topbar(ax, x, ey, ew, accent)
        ax.text(x + 24, ey + 38, title, color=accent, fontsize=15.5, fontweight="bold", va="center")
        ax.text(x + 24, ey + 66, body, color=SUB, fontsize=11.6, va="top", linespacing=1.6)

    section_title(ax, 65, 700, "EVERY REMOVE RUNS THE SAME PIPELINE")

    steps = [
        ("SCAN", "the effective list —\ndefaults minus opt-outs,\nplus your additions", CYAN),
        ("PREVIEW", "exactly what matched,\nwith full package\nidentity", CYAN),
        ("CONFIRM", "one dialog lists every\npackage before\nanything is touched", GREEN),
        ("UNINSTALL", "per app — OneDrive via\nits own uninstaller,\nwinget fallback", ORANGE),
        ("REPORT", "removed vs failed,\nper-package result\ndialog", ORANGE),
    ]
    sw, sh, sgap = 282, 110, 15
    sx0 = (W - (sw * 5 + sgap * 4)) / 2
    sy = 722
    for i, (title, body, accent) in enumerate(steps):
        x = sx0 + i * (sw + sgap)
        panel(ax, x, sy, sw, sh)
        ax.text(x + 18, sy + 26, f"{i + 1}. {title}", color=accent, fontsize=14, fontweight="bold", va="center")
        ax.text(x + 18, sy + 46, body, color=SUB, fontsize=10.6, va="top", linespacing=1.45)
        if i < 4:
            arrow_h(ax, x + sw + 3, x + sw + sgap - 3, sy + sh / 2)

    ny = 862
    panel(ax, 40, ny, W - 80, 52, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, ny + 26,
            "OneDrive is a Win32 app — removed via its own OneDriveSetup.exe /uninstall   •   inbox mstsc.exe is an OS component and is never offered",
            color=SUB, fontsize=12.2, ha="center", va="center")

    footer(ax, "SYSTEM-SIGNED, NON-REMOVABLE COMPONENTS ARE NEVER OFFERED   •   FRESH SETUP FOLLOWS THE SAME EFFECTIVE LIST")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bloatware_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ process monitor
def process_monitor_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  PROCESS MONITOR",
           "Zero-PowerShell network visibility  •  recency-first live ordering  •  one-click response")

    stages = [
        ("SOURCES", CYAN, [
            "Sysmon EventLogWatcher on the",
            "Operational channel (guided",
            "installer + inconsistent-state",
            "repair flow)",
            "",
            "Raw-socket packet sniffer —",
            "packet-level counts without",
            "WinPcap dependencies",
            "",
            "Geo-DNS reverse resolution",
            "with a persistent cache",
        ]),
        ("LIVE TRACKING", BLUE, [
            "One row per remote IP with",
            "Rx / Tx packets and bytes",
            "",
            "Self-explaining rows: name and",
            "image path survive process exit",
            "",
            "taskhostw rows resolved to the",
            "scheduled task that launched",
            "them (path + run-as account)",
            "",
            "Ignore list silences noisy",
            "processes for good",
        ]),
        ("RECENCY-FIRST ORDER", GREEN, [
            "Live sort: a row jumps back to",
            "the top the moment a packet",
            "arrives",
            "",
            "Last Packet header sorts on the",
            "real timestamp, never on the",
            "\u201c5s ago\u201d display text",
            "",
            "Remote-IP tiebreaker keeps",
            "equal-timestamp rows stable",
            "",
            "Rows idle 5+ minutes fade to",
            "half opacity",
        ]),
        ("KNOW YOUR ROW", PURPLE, [
            "Hover the Process column for",
            "the full executable path",
            "",
            "Hover task-attributed rows for",
            "task path and account",
            "",
            "PID-reuse guard compares the",
            "live process with the row's",
            "known identity before a kill",
            "",
            "Everything comes from the",
            "Sysmon event — no guesses",
        ]),
    ]

    cw, ch, gap = 366, 430, 14
    x0, y0 = 47, 190
    for i, (title, accent, lines) in enumerate(stages):
        x = x0 + i * (cw + gap)
        panel(ax, x, y0, cw, ch)
        topbar(ax, x, y0, cw, accent)
        ax.text(x + 20, y0 + 40, title, color=accent, fontsize=15.5, fontweight="bold", va="center")
        ax.text(x + 20, y0 + 70, "\n".join(lines), color=SUB, fontsize=11.2, va="top", linespacing=1.5)
        if i < 3:
            arrow_h(ax, x + cw + 2, x + cw + gap - 2, y0 + ch / 2)

    section_title(ax, 50, 652, "RIGHT-CLICK RESPONSE")

    actions = [
        ("IGNORE PROCESS", CYAN, "never appears in the\nmonitor again"),
        ("BLOCK REMOTE IP", ORANGE, "TCP/UDP × in/out\nWindows Firewall rules"),
        ("BLOCK PROCESS", RED, "inbound + outbound\nby executable path"),
        ("KILL PROCESS", RED, "with the stale-PID\nreuse guard"),
        ("TASK DETAILS", BLUE, "state, triggers,\nactions, author"),
        ("DISABLE TASK", PURPLE, "stops the scheduler\nrelaunch loop"),
    ]
    aw, ah, agap = 240, 120, 12
    ax0 = (W - (aw * 6 + agap * 5)) / 2
    ay = 676
    for i, (title, accent, body) in enumerate(actions):
        x = ax0 + i * (aw + agap)
        panel(ax, x, ay, aw, ah)
        topbar(ax, x, ay, aw, accent)
        ax.text(x + aw / 2, ay + 38, title, color=accent, fontsize=12.6, fontweight="bold",
                ha="center", va="center")
        ax.text(x + aw / 2, ay + 66, body, color=SUB, fontsize=10.8, ha="center", va="top", linespacing=1.5)

    ny = 840
    panel(ax, 40, ny, W - 80, 52, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, ny + 26,
            "No Sysmon? The guided installer downloads and configures it — and repairs broken or inconsistent installs.",
            color=SUB, fontsize=12.5, ha="center", va="center")

    footer(ax, "CSV AUTO-SAVE EVERY HOUR   •   BLOCKS PERSIST IN blocked.txt   •   IGNORES IN ignored_processes.txt")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "process_monitor_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ fresh setup
def fresh_setup_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  ONE-CLICK SETUP",
           "A fresh-install hardening plan that verifies itself  •  doubles as a status dashboard")

    steps = [
        ("WAN Miniports & KDNET", RED,
         "Removes RAS tunneling adapters and the kernel-debug NIC at the device level (SetupAPI, no pnputil).",
         "verify: no WAN Miniport / KDNIC devices present"),
        ("Firewall: Shield Up profile", ORANGE,
         "Disables every grouped rule, then re-enables only the mDNS + Core Networking whitelists.",
         "verify: only whitelisted rule groups remain enabled"),
        ("Privacy: usage data & telemetry", PURPLE,
         "Turns off Windows diagnostic-data collection for the machine.",
         "verify: telemetry policy state read back from registry"),
        ("OS Hardening", BLUE,
         "IPv6 disabled and UAC at maximum strictness (reboot recommended after applying).",
         "verify: both registry states re-read live"),
        ("Launch at logon", CYAN,
         "Creates the highest-privilege scheduled task that starts AutoCommand when you log in.",
         "verify: schtasks query finds the logon task"),
        ("Default Apps: remove bloatware", GREEN,
         "Runs the 23-app bloatware sweep with one master confirmation.",
         "verify: a re-scan finds no bloatware left installed"),
        ("Hibernation: disable", ORANGE,
         "Runs powercfg /hibernate off so no hiberfile is kept on disk.",
         "verify: powercfg /a reports no hibernation"),
    ]

    y0, rh, rgap = 180, 78, 10
    for i, (title, accent, body, verify) in enumerate(steps):
        y = y0 + i * (rh + rgap)
        panel(ax, 40, y, W - 80, rh)
        ax.add_patch(FancyBboxPatch((40, y), 12, rh, boxstyle="round,pad=0,rounding_size=6",
                                    linewidth=0, facecolor=accent, zorder=2))
        ax.add_patch(Circle((92, y + rh / 2), 22, linewidth=2.2, edgecolor=accent, facecolor="none", zorder=3))
        ax.text(92, y + rh / 2, str(i + 1), color=accent, fontsize=17, fontweight="bold",
                ha="center", va="center", zorder=3)
        ax.text(134, y + 26, title, color=TXT, fontsize=15.5, fontweight="bold", va="center")
        ax.text(134, y + 54, body, color=SUB, fontsize=11.8, va="center")
        ax.text(W - 74, y + rh / 2, verify, color=GREEN, fontsize=10.8, ha="right", va="center",
                bbox=dict(boxstyle="round,pad=0.5", facecolor=PANEL, edgecolor=PANEL_EDGE, lw=1.2))

    ax.text(W / 2, 828,
            "One master confirmation up front: review the full plan once, then apply. Every step re-reads live system state,",
            color=SUB, fontsize=13, ha="center", va="center", style="italic")
    ax.text(W / 2, 854,
            "so after the run the same checklist is a hardening status dashboard.",
            color=SUB, fontsize=13, ha="center", va="center", style="italic")

    footer(ax, "EACH STEP REUSES THE EXACT ACTION BEHIND THE CORRESPONDING DEDICATED PAGE")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fresh_setup_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ ai assistant
def ai_assistant_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  AI SECURITY ASSISTANT",
           "Cloud or fully local  •  commands always reviewed before they run")

    cards = [
        ("GEMINI CLOUD", BLUE, [
            "Describe a task in natural",
            "language, get a command back",
            "",
            "Dedicated full-height tab with",
            "chat history and clear-chat",
            "",
            "API key stored locally on",
            "your machine",
        ]),
        ("LOCAL OFFLINE LLM", GREEN, [
            "Integrated LLamaSharp runtime",
            "runs GGUF models directly",
            "",
            "CPU or CUDA 12 backends",
            "",
            "No cloud, no telemetry —",
            "works air-gapped",
        ]),
        ("REVIEW BEFORE RUN", ORANGE, [
            "Every generated command is",
            "shown for explicit approval",
            "",
            "Nothing executes silently —",
            "you approve, edit, or discard",
            "",
            "Execution stays in the",
            "zero-PowerShell core",
        ]),
        ("PAGE-AWARE AUDITS", PURPLE, [
            "Every page implements",
            "IAiAuditable",
            "",
            "The model sees the live app",
            "context (rules, apps, rows,",
            "boot files)",
            "",
            "One-click audit dialog per page",
        ]),
    ]

    cw, ch, gap = 366, 340, 14
    x0, y0 = 47, 190
    for i, (title, accent, lines) in enumerate(cards):
        x = x0 + i * (cw + gap)
        panel(ax, x, y0, cw, ch)
        topbar(ax, x, y0, cw, accent)
        ax.text(x + 20, y0 + 40, title, color=accent, fontsize=15.5, fontweight="bold", va="center")
        ax.text(x + 20, y0 + 72, "\n".join(lines), color=SUB, fontsize=11.6, va="top", linespacing=1.55)
        if i < 3:
            arrow_h(ax, x + cw + 2, x + cw + gap - 2, y0 + ch / 2)

    section_title(ax, 47, 576, "HOW A REQUEST FLOWS")

    flow = [
        ("1. DESCRIBE", "the task in plain\nEnglish", CYAN),
        ("2. DRAFT", "cloud Gemini or a\nlocal GGUF model", BLUE),
        ("3. REVIEW", "you approve, edit,\nor discard", GREEN),
        ("4. RUN", "via the native,\nzero-PowerShell core", ORANGE),
    ]
    fw, fh, fgap = 366, 120, 14
    for i, (title, body, accent) in enumerate(flow):
        x = x0 + i * (fw + fgap)
        panel(ax, x, 600, fw, fh)
        ax.text(x + 20, 600 + 34, title, color=accent, fontsize=14.5, fontweight="bold", va="center")
        ax.text(x + 20, 600 + 60, body, color=SUB, fontsize=11.4, va="top", linespacing=1.5)
        if i < 3:
            arrow_h(ax, x + fw + 2, x + fw + fgap - 2, 600 + fh / 2)

    footer(ax, "YOUR KEY STAYS LOCAL   •   OFFLINE MODE AVAILABLE   •   NO COMMAND RUNS WITHOUT YOU")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ai_assistant_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ why / benefits
def why_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  WHY RUN IT?",
           "What it does for you  —  not just what it does")

    benefits = [
        ("SEE EVERY CONNECTION", CYAN,
         "Know which process talked to which\naddress, when it went quiet, and\nwhich scheduled task launched it —\nlive, with Geo-DNS names, without\nbecoming a Wireshark expert."),
        ("SHUT THREATS DOWN IN SECONDS", RED,
         "Right-click a suspicious row to block\nits remote IP or the whole executable,\nkill the process — or disable the\nscheduled task that would just\nresurrect it."),
        ("DEBLOAT WINDOWS IN ONE CLICK", GREEN,
         "Remove 23 preinstalled apps — Copilot,\nTeams, OneDrive, Xbox, Solitaire… —\nwith one confirmation instead of 23\nuninstallers. Keep any app by toggling\nit off your list."),
        ("LOCK YOUR PERIMETER IN SECONDS", ORANGE,
         "Five firewall profiles from paranoid\nlockdown to trusted home LAN, applied\nclassifier-free across every rule —\nand silent rule drift after Windows\nUpdates gets detected."),
        ("TRUST WHAT BOOTS", PURPLE,
         "Every EFI module hashed and watched\nfor drift, your boot manager signature\nchecked, and Microsoft's latest DBX\nrevocations applied — the earliest\nlink in the chain, verified."),
        ("GET COMMANDS, NOT RISKS", BLUE,
         "Describe what you need in plain\nEnglish — cloud AI or a fully local\nmodel — and every generated command\nwaits for your approval before it\ncan run."),
    ]

    cw, ch, gap = 489, 270, 22
    x0, y0 = 44, 180
    for i, (title, accent, body) in enumerate(benefits):
        col, row = i % 3, i // 3
        x = x0 + col * (cw + gap)
        y = y0 + row * (ch + 20)
        panel(ax, x, y, cw, ch)
        topbar(ax, x, y, cw, accent)
        ax.text(x + 24, y + 42, title, color=accent, fontsize=15.5, fontweight="bold", va="center")
        ax.text(x + 24, y + 74, body, color=SUB, fontsize=11.8, va="top", linespacing=1.65)

    section_title(ax, 44, 772, "WHO IT'S FOR")

    personas = [
        ("HOME POWER USERS", CYAN, "tired of preinstalled apps and opaque\nbackground chatter on their own PCs"),
        ("IT TECHNICIANS", GREEN, "imaging new machines that need the\nsame hard baseline every single time"),
        ("SECURITY-MINDED PROS", ORANGE, "who want evidence and one-click\nresponse, not scripts of hope"),
    ]
    pw, ph, pgap = 489, 96, 22
    for i, (title, accent, body) in enumerate(personas):
        x = x0 + i * (pw + pgap)
        panel(ax, x, 794, pw, ph)
        topbar(ax, x, 794, pw, accent)
        ax.text(x + 24, 794 + 28, title, color=accent, fontsize=13.5, fontweight="bold", va="center")
        ax.text(x + 24, 794 + 48, body, color=SUB, fontsize=11.2, va="top", linespacing=1.45)

    footer(ax, "ONE ADMINISTRATOR DASHBOARD   •   ZERO-POWERSHELL CORE   •   YOUR MACHINE, YOUR RULES")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "why_autocommand_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


# ============================================================ BITS transfers
def bits_infographic():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  —  BITS TRANSFERS",
           "Every Background Intelligent Transfer job  •  where · what · how often · why · content  •  firewall bypass, answered")

    # -- the incident that built the tab ------------------------------------
    section_title(ax, 44, 168, "THE QUESTION THAT BUILT IT")
    panel(ax, 40, 180, W - 80, 86, edge=PANEL_EDGE)
    ax.text(66, 214, "The monitor flagged:  svchost.exe -k netsvcs -p -s BITS   uploaded 294 packets to 34.104.35.123",
            color=TXT, fontsize=13.8, va="center", fontweight="bold")
    ax.text(66, 244, "The tab's answer, in one row:  Chrome Component Updater — three download jobs from Google's gvt1.com CDN, "
                     "creator chrome.exe PID 19716, status 0x0.  Verdict: Normal.",
            color=SUB, fontsize=12.4, va="center")

    # -- two read-only sources (720-wide panels + a true 80px gutter) --------
    section_title(ax, 44, 292, "TWO READ-ONLY SOURCES  •  STITCHED BY JOB GUID")
    sy, sh_ = 304, 148
    panel(ax, 40, sy, 720, sh_)
    topbar(ax, 40, sy, 720, CYAN)
    ax.text(64, sy + 34, "EVENT LOG  —  THE HISTORY", color=CYAN, fontsize=14.5, fontweight="bold", va="center")
    ax.text(64, sy + 60,
            "Microsoft-Windows-Bits-Client/Operational, backfilled whole at start:\n"
            "#3 job created - creating process + PID, owner  ·  #59/#60 start/stop - URL + status\n"
            "#4 complete - file count.  History survives app restarts.",
            color=SUB, fontsize=11.2, va="top", linespacing=1.7)
    ax.text(800, sy + sh_ / 2, "GUID", color=CYAN, fontsize=13, fontweight="bold", ha="center", va="center",
            bbox=dict(boxstyle="round,pad=0.45", facecolor=PANEL, edgecolor=CYAN_DIM, lw=1.6))
    panel(ax, 840, sy, 720, sh_)
    topbar(ax, 840, sy, 720, GREEN)
    ax.text(864, sy + 34, "BITS COM QUEUE  —  THE LIVE TRUTH", color=GREEN, fontsize=14.5, fontweight="bold", va="center")
    ax.text(864, sy + 60,
            "All users, strictly read-only: hand-declared vtable interop, no PowerShell.\n"
            "Direction (download vs UPLOAD), live state + byte progress, per-file remote\n"
            "AND local paths — none of which the event log records.",
            color=SUB, fontsize=11.2, va="top", linespacing=1.7)

    # -- the five questions ---------------------------------------------------
    section_title(ax, 44, 478, "EVERY JOB ANSWERS FIVE QUESTIONS")
    tiles = [
        ("WHERE", CYAN,
         "URL host -> resolved IPs\n(cached forward DNS) ->\nowner org: built-in\nMicrosoft / Akamai / Gcore /\nGoogle ranges, RDAP for\nanything unknown."),
        ("WHAT THEY MOVE", BLUE,
         "Direction (download vs\nupload), byte progress,\nper-file remote + local\nnames, size + SHA-256 of\neach delivered file on\ncompletion."),
        ("HOW OFTEN", GREEN,
         "Patterns view rolls every\ntransfer up per creator ->\nhost: counts, per-day\naverages, and interval\nregularity (beacon CV -\nlow means scheduled)."),
        ("WHY", PURPLE,
         "A reason catalog names\nit: Chrome on gvt1, cloud\nfonts, Defender signatures,\nOffice CDN, Windows\nUpdate.  No match ->\n'Unattributed - review'."),
        ("THE CONTENT", ORANGE,
         "Hash + size automatic,\nlocal-only (Sysmon-style).\nThe bytes appear ONLY via\nthe per-file Preview click -\nnever stored, exported,\nor sent anywhere."),
    ]
    tw, th, tgap = 288, 196, 18
    ty = 490
    for i, (name, accent, body) in enumerate(tiles):
        x = 44 + i * (tw + tgap)
        panel(ax, x, ty, tw, th)
        topbar(ax, x, ty, tw, accent)
        ax.text(x + 18, ty + 32, name, color=accent, fontsize=13.5, fontweight="bold", va="center")
        ax.text(x + 18, ty + 60, body, color=SUB, fontsize=10.2, va="top", linespacing=1.58)
        if i < 4:
            arrow_h(ax, x + tw + 4, x + tw + tgap - 4, ty + th / 2)

    # -- firewall bypass -------------------------------------------------------
    section_title(ax, 44, 710, "DOES IT BYPASS THE FIREWALL?  —  ANSWERED IN THREE HONEST LAYERS")
    layers = [
        ("1. DEFAULT OUTBOUND ACTION", CYAN,
         "Profile defaults outbound to Allow (the Windows\ndefault)? Then no rule is evaluated at all — bypass\nis the default state, for BITS and all traffic."),
        ("2. BITS-SCOPED ALLOW RULES", ORANGE,
         "Under a default-deny, count the exemptions: allow\nrules scoped to the BITS service or svchost.exe.\nRead live via INetFwPolicy2 COM, cached 5 min."),
        ("3. STRUCTURAL — ALWAYS TRUE", RED,
         "Transfers execute inside trusted svchost: a block\non the creating process never matches (ATT&CK\nT1197). Blocking BITS breaks Windows Update."),
    ]
    lw_, lh, lgap = 489, 136, 22
    ly = 722
    for i, (title, accent, body) in enumerate(layers):
        x = 44 + i * (lw_ + lgap)
        panel(ax, x, ly, lw_, lh)
        topbar(ax, x, ly, lw_, accent)
        ax.text(x + 22, ly + 28, title, color=accent, fontsize=13.5, fontweight="bold", va="center")
        ax.text(x + 22, ly + 52, body, color=SUB, fontsize=10.6, va="top", linespacing=1.5)

    # -- upload alert strip (its own band, clear of the footer) ---------------
    panel(ax, 40, 872, W - 80, 38, edge=CYAN_DIM, alpha=0.95)
    ax.text(W / 2, 891, "UPLOAD-TYPE JOBS:  REVIEW VERDICT  +  APP THREAT STATUS BAR   •   STRICTLY READ-ONLY TOWARD WINDOWS",
            color=ORANGE, fontsize=12.2, ha="center", va="center", fontweight="bold")

    footer(ax, "BITS DELIVERS WINDOWS UPDATE   •   METADATA ONLY BY DEFAULT   •   CONTENT = EXPLICIT PER-FILE CLICK   •   NOTHING LEAVES THE MACHINE")

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bits_infographic.png")
    fig.savefig(out, dpi=150)
    plt.close(fig)
    return out


if __name__ == "__main__":
    for generate in (
        why_infographic,
        hero_infographic,
        stack_infographic,
        firewall_infographic,
        bloatware_infographic,
        process_monitor_infographic,
        fresh_setup_infographic,
        ai_assistant_infographic,
        bits_infographic,
    ):
        print(generate())
