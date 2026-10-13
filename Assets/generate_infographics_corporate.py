"""Generate AutoCommand README infographics (corporate light theme, matplotlib).

Usage:  <python with matplotlib> Assets/generate_infographics_corporate.py
Outputs (Assets/):
  corp_banner.png      hero
  corp_why.png         scattered tools -> one dashboard
  corp_visibility.png  Process Monitor + Sysmon Audit
  corp_control.png     Update Control + Telemetry
  corp_proof.png       Block Proof + telemetry sinkhole
  corp_hardening.png   Firewall + guards + Secure Boot/DBX
  corp_debloat.png     Default Apps + Fresh Setup
Requires: matplotlib, numpy
"""
import os

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

matplotlib.rcParams["font.family"] = "DejaVu Sans"

# ---- corporate palette ----------------------------------------------------
PAGE = "#FFFFFF"
SURFACE = "#F4F7FB"
BORDER = "#D7DFE9"
NAVY = "#16294C"
INK = "#26303B"
MUTED = "#5B6B7E"
ACCENT = "#2563EB"
ACCENT_SOFT = "#EAF1FE"
GREEN = "#15803D"
GREEN_SOFT = "#E8F5EE"
AMBER = "#B45309"
AMBER_SOFT = "#FBF3E4"
RED = "#B42318"
RED_SOFT = "#FBEAE8"

VERSION = "v2026.11.03"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)))
TITLE_Y_PAD = 74


def canvas(w, h):
    fig = plt.figure(figsize=(w / 100, h / 100), dpi=150)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, w)
    ax.set_ylim(0, h)
    ax.invert_yaxis()
    ax.axis("off")
    fig.patch.set_facecolor(PAGE)
    # thin corporate accent rule across the very top
    ax.add_patch(FancyBboxPatch((0, 0), w, 6, boxstyle="round,pad=0,rounding_size=0",
                                linewidth=0, facecolor=ACCENT, zorder=3))
    return fig, ax


def card(ax, x, y, w, h, face=SURFACE, edge=BORDER, lw=1.2, bar=None, bar_w=5, r=10):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle=f"round,pad=0,rounding_size={r}",
                                linewidth=lw, edgecolor=edge, facecolor=face, zorder=1))
    if bar:
        ax.add_patch(FancyBboxPatch((x, y), bar_w, h, boxstyle="round,pad=0,rounding_size=3",
                                    linewidth=0, facecolor=bar, zorder=2))


def chip(ax, x, y, text, face=ACCENT_SOFT, color=NAVY, size=11.5, pad=0.42, bold=False):
    ax.text(x, y, text, fontsize=size, color=color, va="center", zorder=4,
            fontweight="bold" if bold else "normal",
            bbox=dict(boxstyle=f"round,pad={pad}", facecolor=face, edgecolor="none", linewidth=0))


def page_header(ax, w, kicker, title, subtitle=None):
    ax.text(56, TITLE_Y_PAD - 28, kicker.upper(), fontsize=11.5, color=ACCENT,
            fontweight="bold", va="center")
    ax.text(56, TITLE_Y_PAD + 6, title, fontsize=27, color=NAVY, fontweight="bold", va="center")
    if subtitle:
        ax.text(56, TITLE_Y_PAD + 40, subtitle, fontsize=13.5, color=MUTED, va="center")


def footer(ax, w, h, note=""):
    ax.plot([56, w - 56], [h - 40, h - 40], color=BORDER, linewidth=1.0, zorder=2)
    ax.text(56, h - 22, f"AutoCommand  ·  {VERSION}  ·  Apache-2.0", fontsize=10.5,
            color=MUTED, va="center")
    if note:
        ax.text(w - 56, h - 22, note, fontsize=10.5, color=MUTED, va="center", ha="right")


def arrow_down(ax, x, y1, y2, color=ACCENT):
    ax.annotate("", xy=(x, y2), xytext=(x, y1),
                arrowprops=dict(arrowstyle="-|>", color=color, linewidth=1.6), zorder=3)


def arrow_right(ax, y, x1, x2, color=ACCENT):
    ax.annotate("", xy=(x2, y), xytext=(x1, y),
                arrowprops=dict(arrowstyle="-|>", color=color, linewidth=1.6), zorder=3)


# ---------------------------------------------------------------------------
def corp_banner():
    w, h = 1600, 620
    fig, ax = canvas(w, h)

    ax.text(64, 150, "AutoCommand", fontsize=52, color=NAVY, fontweight="bold", va="center")
    ax.add_patch(FancyBboxPatch((64, 196), 120, 6, boxstyle="round,pad=0,rounding_size=3",
                                linewidth=0, facecolor=ACCENT, zorder=3))
    ax.text(64, 242, "See what your Windows PC is really doing —",
            fontsize=21, color=INK, va="center")
    ax.text(64, 276, "then take it back with one click.",
            fontsize=21, color=MUTED, va="center")
    chip(ax, 64, 330, "Windows 10 / 11", face=SURFACE, color=INK, size=12)
    chip(ax, 250, 330, "Administrator dashboard", face=SURFACE, color=INK, size=12)
    chip(ax, 510, 330, "Free · open source · Apache-2.0", face=SURFACE, color=INK, size=12)
    ax.text(64, 386, "github.com/dparksports/autocommand-windows", fontsize=12.5,
            color=ACCENT, va="center")

    pillars = [
        ("●", "See everything", "Every process, connection and DNS query — attributed,\nrecorded and traceable even after the process exits.", ACCENT),
        ("●", "Control updates & telemetry", "Pin an app at the version that works; opt out of usage\nbeacons — and block the ones that ignore it, with proof.", GREEN),
        ("●", "Harden & clean", "Firewall lockdown, 23-app debloat, boot-chain guards,\nSecure Boot DBX updates — confirmed before anything runs.", AMBER),
    ]
    x0, y0, cw, ch, gap = 960, 108, 576, 128, 22
    for i, (glyph, title, body, accent) in enumerate(pillars):
        y = y0 + i * (ch + gap)
        card(ax, x0, y, cw, ch, face=SURFACE, bar=accent)
        ax.text(x0 + 26, y + 34, glyph, fontsize=13, color=accent, va="center", zorder=4)
        ax.text(x0 + 52, y + 34, title, fontsize=16, color=NAVY, fontweight="bold", va="center")
        ax.text(x0 + 52, y + 78, body, fontsize=11.8, color=MUTED, va="center", linespacing=1.5)

    footer(ax, w, h, "Security and control dashboard")
    fig.savefig(os.path.join(OUT, "corp_banner.png"), dpi=150)
    plt.close(fig)


# ---------------------------------------------------------------------------
def corp_why():
    w, h = 1500, 900
    fig, ax = canvas(w, h)
    page_header(ax, w, "Why AutoCommand",
                "The answers exist — scattered across a dozen tools",
                "AutoCommand puts the whole picture in one administrator window and turns the scary parts into single clicks.")

    left = [
        ("Task Manager", "Forgets a process the instant it\nexits — no record, no parent,\nno command line."),
        ("Event Viewer", "Never recorded the launch in the\nfirst place; no process-creation\naudit by default."),
        ("Settings, CPLs, forums", "Firewall, debloat, privacy and\nhardening spread across dialogs\nand copy-pasted commands."),
        ("Self-updating apps", "Replace themselves in place;\nusage beacons ship enabled.\nYou find out later."),
    ]
    right = [
        ("One live grid", "Every outbound connection with\nprocess, packets and hostname —\nsorted by real recency."),
        ("A record, not a guess", "Sysmon installed and configured\nfor you: every launch, connection\nand DNS query, kept locally."),
        ("One dashboard, one click", "Profiles, sweeps and guards\napplied from a single window —\nconfirmed before anything runs."),
        ("Pinned and silenced", "Freeze self-updaters at the version\nthat works; opt apps out of\ntelemetry — reversibly."),
    ]
    x0, x1 = 56, 810
    cw, ch, gap = 556, 162, 12
    y0 = 150
    for i in range(4):
        y = y0 + i * (ch + gap)
        card(ax, x0, y, cw, ch, face=SURFACE, bar="#9AA7B8")
        ax.text(x0 + 24, y + 32, left[i][0], fontsize=14.5, color=MUTED, fontweight="bold", va="center")
        ax.text(x0 + 24, y + 108, left[i][1], fontsize=11.4, color=MUTED, va="center", linespacing=1.5)

        card(ax, x1, y, cw, ch, face=ACCENT_SOFT, edge="#C4D7F6", bar=ACCENT)
        ax.text(x1 + 24, y + 32, right[i][0], fontsize=14.5, color=NAVY, fontweight="bold", va="center")
        ax.text(x1 + 24, y + 108, right[i][1], fontsize=11.4, color=INK, va="center", linespacing=1.5)
        arrow_right(ax, y + ch / 2, x0 + cw + 10, x1 - 12)

    footer(ax, w, h, "Know — then act — without leaving the window")
    fig.savefig(os.path.join(OUT, "corp_why.png"), dpi=150)
    plt.close(fig)


# ---------------------------------------------------------------------------
def corp_visibility():
    w, h = 1500, 940
    fig, ax = canvas(w, h)
    page_header(ax, w, "Process Monitor  +  Sysmon Audit",
                "See everything — and know who launched it",
                "Live connection tracking with attribution that survives process exit, backed by a local audit trail Windows does not keep.")

    # left: pipeline
    card(ax, 56, 156, 700, 236, face=SURFACE, bar=ACCENT)
    ax.text(84, 190, "Collect", fontsize=15, color=NAVY, fontweight="bold", va="center")
    ax.text(84, 224, "Sysmon events — process creations (ID 1), network\nconnections (ID 3), DNS queries (ID 22)", fontsize=12.2, color=INK, va="center", linespacing=1.5)
    ax.text(84, 288, "Raw-socket sniffer (SIO_RCVALL) — per-row packet counters,\nno WinPcap/Npcap dependency", fontsize=12.2, color=INK, va="center", linespacing=1.5)
    chip(ax, 84, 348, "installed from the app in one click", face=ACCENT_SOFT, color=NAVY, size=11)
    arrow_down(ax, 406, 396, 428)

    card(ax, 56, 432, 700, 196, face=SURFACE, bar=ACCENT)
    ax.text(84, 466, "Attribute", fontsize=15, color=NAVY, fontweight="bold", va="center")
    ax.text(84, 540, "Parent chain for every launch · taskhostw instance GUID correlated\nwith Task Scheduler history · COM-handler CLSID resolved to the\nDLL that actually runs — flagged when the DLL is gone", fontsize=12.2, color=INK, va="center", linespacing=1.55)
    arrow_down(ax, 406, 632, 656)

    card(ax, 56, 660, 700, 218, face=ACCENT_SOFT, edge="#C4D7F6", bar=ACCENT)
    ax.text(84, 694, "Act", fontsize=15, color=NAVY, fontweight="bold", va="center")
    ax.text(84, 778, "Right-click a row: block the IP, block the executable, kill the\nprocess, inspect or disable the scheduled task behind it.\nEvidence exports to CSV; ignore lists keep the view clean.", fontsize=12.2, color=INK, va="center", linespacing=1.55)

    # right: mock status + feed
    card(ax, 810, 156, 634, 250, face=PAGE, edge=BORDER)
    ax.add_patch(FancyBboxPatch((810, 156), 634, 44, boxstyle="round,pad=0,rounding_size=10",
                                linewidth=0, facecolor=NAVY, zorder=2))
    ax.text(830, 178, "Sysmon status", fontsize=13.5, color="white", fontweight="bold", va="center")
    rows = [
        ("●", "Installed — service running, event channel healthy", GREEN, GREEN_SOFT),
        ("●", "12,480 events in the last 24 h · newest 09:51:33", INK, SURFACE),
        ("●", "Config captures ID 1 · 3 · 22 · hash 9f2ac41d…", INK, SURFACE),
        ("●", "Inconsistent? One guided repair clears the leftovers", AMBER, AMBER_SOFT),
    ]
    for i, (glyph, text, color, face) in enumerate(rows):
        y = 214 + i * 44
        chip(ax, 830, y, glyph, face=face, color=color, size=11)
        ax.text(858, y, text, fontsize=12.2, color=INK, va="center")

    card(ax, 810, 432, 634, 446, face=PAGE, edge=BORDER)
    ax.add_patch(FancyBboxPatch((810, 432), 634, 44, boxstyle="round,pad=0,rounding_size=10",
                                linewidth=0, facecolor=NAVY, zorder=2))
    ax.text(830, 454, "Recent process creations (live feed)", fontsize=13.5, color="white",
            fontweight="bold", va="center")
    feed = [
        ("09:51:33", "python.exe", "powershell.exe", "detect_people_offline.py --camera all", INK),
        ("09:50:58", "winget.exe", "agy.exe", "install --id UB-Mannheim.TesseractOCR", INK),
        ("09:48:12", "taskhostw.exe", "svchost.exe", "{015e62d0-…} SoftLanding Deferral", AMBER),
        ("00:37:06", "powershell.exe", "agy.exe", "python detect_people_offline.py", INK),
        ("00:21:53", "agy.exe", "agy.exe", "self-update: agy.exe.old replaced", RED),
    ]
    for i, (t, img, par, cmd, color) in enumerate(feed):
        y = 496 + i * 62
        ax.text(830, y, t, fontsize=11.5, color=MUTED, va="center")
        ax.text(908, y, img, fontsize=12.2, color=NAVY, fontweight="bold", va="center")
        ax.text(1170, y, f"← {par}", fontsize=11.2, color=MUTED, va="center")
        ax.text(908, y + 22, cmd, fontsize=11.2, color=color, va="center")
        if i < 4:
            ax.plot([830, 1424], [y + 40, y + 40], color=BORDER, linewidth=0.8)
    ax.text(830, 846, "Exited processes keep their row — the name, path and parent stay on record.",
            fontsize=11.5, color=MUTED, va="center", style="italic")

    footer(ax, w, h, "Mystery PID hunts are over")
    fig.savefig(os.path.join(OUT, "corp_visibility.png"), dpi=150)
    plt.close(fig)


# ---------------------------------------------------------------------------
def corp_control():
    w, h = 1500, 940
    fig, ax = canvas(w, h)
    page_header(ax, w, "Update Control  +  Telemetry",
                "Pin the version that works — silence the beacons",
                "Both controls are reversible by design: the previous state is snapshotted before anything changes.")

    # left: Update Control
    ax.text(56, 158, "Update Control — freeze a self-updating app", fontsize=16.5,
            color=NAVY, fontweight="bold", va="center")
    steps = [
        ("1", "Discover", "Self-update .old leftovers, updater folders,\nautostart entries — with vendor signatures."),
        ("2", "Snapshot", "icacls /save records the file's exact\npermissions before any change."),
        ("3", "Deny", "Delete/write denied on the exe; read+execute\nuntouched — the app runs normally."),
        ("4", "Result", "The updater fails politely until you unfreeze;\nunfreezing restores the original ACL."),
    ]
    y0 = 190
    for i, (n, t, body) in enumerate(steps):
        y = y0 + i * 128
        card(ax, 56, y, 660, 108, face=SURFACE, bar=ACCENT)
        ax.text(84, y + 54, n, fontsize=22, color=ACCENT, fontweight="bold", va="center")
        ax.text(122, y + 32, t, fontsize=14.5, color=NAVY, fontweight="bold", va="center")
        ax.text(122, y + 64, body, fontsize=11.8, color=MUTED, va="center", linespacing=1.45)
        if i < 3:
            arrow_down(ax, 386, y + 112, y + 124)
    chip(ax, 56, 748, "Windows system binaries are refused — OS components are never frozen", face=AMBER_SOFT, color=AMBER, size=11.5)

    # divider
    ax.plot([756, 756], [150, 850], color=BORDER, linewidth=1.0)

    # right: Telemetry
    ax.text(796, 158, "Telemetry — vendor opt-outs, applied for you", fontsize=16,
            color=NAVY, fontweight="bold", va="center")
    table = [
        ("Ultralytics YOLO", "settings.json → sync = false — boolean, re-read to verify", "Opted out", GREEN, GREEN_SOFT),
        ("Ultralytics, before", 'sync = "false" — a quoted string the library reads as ON', "Not honored", RED, RED_SOFT),
        (".NET CLI", "DOTNET_CLI_TELEMETRY_OPTOUT = 1", "Opted out", GREEN, GREEN_SOFT),
        ("PowerShell 7", "POWERSHELL_TELEMETRY_OPTOUT = 1", "Opted out", GREEN, GREEN_SOFT),
        ("VS Code", "telemetry.telemetryLevel = off", "Opted out", GREEN, GREEN_SOFT),
        ("Edge / Chrome", "MetricsReportingEnabled policy = 0", "Opted out", GREEN, GREEN_SOFT),
        ("Windows diagnostic data", "AllowTelemetry policy = 1", "Review", AMBER, AMBER_SOFT),
    ]
    card(ax, 796, 190, 648, 380, face=PAGE, edge=BORDER)
    for i, (app, how, state, color, face) in enumerate(table):
        y = 216 + i * 58
        ax.text(818, y, app, fontsize=12.5, color=NAVY, fontweight="bold", va="center")
        ax.text(818, y + 22, how, fontsize=11, color=MUTED, va="center")
        chip(ax, 1300, y, state, face=face, color=color, size=11)
        if i < 5:
            ax.plot([818, 1422], [y + 38, y + 38], color=BORDER, linewidth=0.8)
    card(ax, 796, 606, 648, 96, face=ACCENT_SOFT, edge="#C4D7F6", bar=ACCENT)
    ax.text(820, 638, "Verified quiet — or honestly not", fontsize=13.5, color=NAVY, fontweight="bold", va="center")
    ax.text(820, 666, "Writes go in the exact token type the app reads and are re-read to verify;\na wrong-typed opt-out shows as Not honored instead of fake quiet.", fontsize=11.8, color=INK, va="center", linespacing=1.45)
    chip(ax, 796, 742, "App ignores the switch? Pin its beacon domain — or sinkhole it and watch it knock (Block Proof)", face=RED_SOFT, color=RED, size=11)
    chip(ax, 796, 780, "Previous value snapshotted — one click reverts any opt-out", face=GREEN_SOFT, color=GREEN, size=11.5)

    footer(ax, w, h, "Nothing here is a one-way door")
    fig.savefig(os.path.join(OUT, "corp_control.png"), dpi=150)
    plt.close(fig)


# ---------------------------------------------------------------------------
def corp_hardening():
    w, h = 1500, 920
    fig, ax = canvas(w, h)
    page_header(ax, w, "Firewall  ·  Guards  ·  Secure Boot",
                "Lock it down — and keep it locked",
                "Profiles apply in one click; guards re-assert themselves when something drifts back.")

    cols = [
        ("Advanced Firewall", ACCENT, [
            "INetFwPolicy2 COM engine — enumerate,\nenable, disable, create rules directly",
            "Curated profiles: full lockdown, home-LAN,\nper-app — applied in one click",
            "Baseline of every rule's enabled state;\ndrift reported on re-scan",
            "One-click block for any IP or executable,\nfrom the monitor or the firewall page",
        ]),
        ("Self-healing guards", GREEN, [
            "SSTP tunneling service stopped and\ndisabled — re-asserted if re-enabled",
            "Kernel-debug surfaces blocked\n(bcdedit + KDNIC adapter removal)",
            "Background Security Enforcer watches\nfor drift; toast notification on change",
            "Miniport devices removed via SetupAPI\nkernel-mode clean removal",
        ]),
        ("Secure Boot & DBX", AMBER, [
            "Every EFI module on the boot partition\nhashed against a stored baseline",
            "Boot-manager checks gated by SHA256\nand Authenticode (WinVerifyTrust)",
            "Microsoft DBX revocation updates parsed\nas EFI_SIGNATURE_LIST in C#",
            "Applied only after explicit confirmation\n— the boot chain is never touched blindly",
        ]),
    ]
    x0, cw, gap = 56, 448, 22
    for i, (title, accent, items) in enumerate(cols):
        x = x0 + i * (cw + gap)
        card(ax, x, 170, cw, 590, face=SURFACE, bar=accent)
        ax.text(x + 26, 206, title, fontsize=16, color=NAVY, fontweight="bold", va="center")
        for j, item in enumerate(items):
            y = 262 + j * 122
            chip(ax, x + 26, y, "✓", face=PAGE, color=accent, size=11.5)
            ax.text(x + 56, y + 2, item, fontsize=11.8, color=INK, va="center", linespacing=1.5)

    footer(ax, w, h, "Drift is detected — and corrected")
    fig.savefig(os.path.join(OUT, "corp_hardening.png"), dpi=150)
    plt.close(fig)


# ---------------------------------------------------------------------------
def corp_debloat():
    w, h = 1500, 920
    fig, ax = canvas(w, h)
    page_header(ax, w, "Default Apps  +  Fresh Setup",
                "A fresh Windows install, cleaned in one pass",
                "Twenty-three preinstalled apps removed in one confirmation — and the list stays yours.")

    # left: 23 apps
    card(ax, 56, 170, 700, 380, face=SURFACE, bar=ACCENT)
    ax.text(84, 206, "The sweep", fontsize=15.5, color=NAVY, fontweight="bold", va="center")
    apps = ["Copilot", "Teams", "OneDrive", "Xbox", "Solitaire", "Feedback Hub",
            "Get Help", "News", "To Do", "Power Automate", "Quick Assist",
            "Widgets", "Dev Home", "Phone Link", "+ 9 more"]
    cx, cy = 84, 250
    for app in apps:
        wpx = 30 + 7.4 * len(app)
        if cx + wpx > 56 + 700 - 24:
            cx, cy = 84, cy + 46
        chip(ax, cx, cy, app, face=PAGE, color=INK, size=11.2)
        cx += wpx + 10
    ax.text(84, 496, "OneDrive uninstalled through its own Win32 uninstaller;",
            fontsize=11.5, color=MUTED, va="center", style="italic")
    ax.text(84, 518, "System-signed components like mstsc.exe are never offered.",
            fontsize=11.5, color=MUTED, va="center", style="italic")

    # middle: your list
    card(ax, 56, 586, 700, 272, face=ACCENT_SOFT, edge="#C4D7F6", bar=ACCENT)
    ax.text(84, 622, "Your list, your rules", fontsize=15.5, color=NAVY, fontweight="bold", va="center")
    rows = [("✓", "Keep Calculator — toggled off the sweep", GREEN),
            ("✓", "Keep Terminal — you actually use it", GREEN),
            ("✗", "Remove Teams, Copilot, News, Solitaire…", RED)]
    for i, (glyph, text, color) in enumerate(rows):
        chip(ax, 84, 664 + i * 44, glyph, face=PAGE, color=color, size=11.5)
        ax.text(112, 664 + i * 44, text, fontsize=12.2, color=INK, va="center")
    ax.text(84, 812, "Customizations persist machine-wide as a delta over the defaults",
            fontsize=11.5, color=MUTED, va="center", style="italic")

    # right: fresh setup timeline
    card(ax, 800, 170, 644, 688, face=PAGE, edge=BORDER)
    ax.add_patch(FancyBboxPatch((800, 170), 644, 44, boxstyle="round,pad=0,rounding_size=10",
                                linewidth=0, facecolor=NAVY, zorder=2))
    ax.text(820, 192, "Fresh Setup — one guided pass", fontsize=13.5, color="white",
            fontweight="bold", va="center")
    steps = [
        ("1", "Debloat sweep", "23 apps, your list respected"),
        ("2", "Firewall lockdown", "profile applied, rules baselined"),
        ("3", "Privacy & telemetry", "opt-outs applied and verified"),
        ("4", "Hardening", "SSTP, kernel-debug, boot-chain checks"),
        ("5", "Verified", "every step re-checked, drift reported"),
    ]
    for i, (n, t, s) in enumerate(steps):
        y = 250 + i * 118
        ax.text(836, y, n, fontsize=19, color=ACCENT, fontweight="bold", va="center")
        ax.text(872, y - 14, t, fontsize=13.5, color=NAVY, fontweight="bold", va="center")
        ax.text(872, y + 10, s, fontsize=11.5, color=MUTED, va="center")
        if i < 4:
            ax.plot([846, 846], [y + 24, y + 92], color=BORDER, linewidth=1.2)

    footer(ax, w, h, "The first hour after a clean install, in one window")
    fig.savefig(os.path.join(OUT, "corp_debloat.png"), dpi=150)
    plt.close(fig)


# ---------------------------------------------------------------------------
def corp_proof():
    w, h = 1500, 940
    fig, ax = canvas(w, h)
    page_header(ax, w, "Block Proof  ·  Telemetry Sinkhole",
                "You blocked it. Here is the proof.",
                "Pinned telemetry domains fail invisibly — the sinkhole makes every attempt walk up to the door, sign the guest book, and get dropped.")

    # left: the tab, mocked
    card(ax, 56, 156, 700, 116, face=GREEN_SOFT, edge="#BFE3CE", bar=GREEN)
    ax.text(84, 190, "Pinned:  www.google-analytics.com  →  127.0.0.1", fontsize=13.5,
            color=NAVY, fontweight="bold", va="center")
    ax.text(84, 222, "Sinkhole active — listening on 127.0.0.1:443. Attempts are logged and dropped.",
            fontsize=11.8, color=INK, va="center")
    chip(ax, 84, 250, "toggle back to silent 0.0.0.0 any time", face=PAGE, color=MUTED, size=10.5)

    card(ax, 56, 296, 700, 470, face=PAGE, edge=BORDER)
    ax.add_patch(FancyBboxPatch((56, 296), 700, 44, boxstyle="round,pad=0,rounding_size=10",
                                linewidth=0, facecolor=NAVY, zorder=2))
    ax.text(76, 318, "Denied attempts (live)", fontsize=13.5, color="white", fontweight="bold", va="center")
    chip(ax, 676, 318, "2,041 denied since Oct 13", face="#0F2038", color="#9FE0B4", size=10.5)
    feed = [
        ("19:14:56", "www.google-analytics.com", "python.exe · pid 18092", "rescan_panoramic.py"),
        ("19:15:26", "www.google-analytics.com", "python.exe · pid 19404", "detect_people_offline.py --camera all"),
        ("20:02:11", "www.google-analytics.com", "python.exe · pid 21155", "train.py --epochs 50"),
        ("21:40:03", "www.google-analytics.com", "python.exe · pid 23117", "export_model.py --format onnx"),
    ]
    for i, (t, host, proc, cmd) in enumerate(feed):
        y = 372 + i * 84
        ax.text(76, y, t, fontsize=11.5, color=MUTED, va="center")
        ax.text(168, y, host, fontsize=12.2, color=NAVY, fontweight="bold", va="center")
        ax.text(76, y + 24, f"{proc}  ·  {cmd}", fontsize=11.2, color=INK, va="center")
        chip(ax, 640, y, "denied", face=RED_SOFT, color=RED, size=10.5)
        if i < 3:
            ax.plot([76, 736], [y + 48, y + 48], color=BORDER, linewidth=0.8)
    ax.text(76, 736, "Every row: hostname from the TLS hello, PID from the OS connection table,",
            fontsize=11.2, color=MUTED, va="center", style="italic")
    ax.text(76, 754, "process image and command line from the OS. Plain JSONL on disk. Exportable.",
            fontsize=11.2, color=MUTED, va="center", style="italic")

    chip(ax, 56, 792, "port 443 busy → sinkhole stays off and says so; the pin still blocks", face=AMBER_SOFT, color=AMBER, size=11)
    chip(ax, 56, 826, "proof collects while the app runs — with it closed, attempts still fail, unlogged", face=SURFACE, color=INK, size=11)

    # right: the three moments
    steps = [
        ("1", "Pin",
         "The telemetry domain resolves to loopback instead of the real\n"
         "server. Silent pins (0.0.0.0) fail instantly; the sinkhole (127.0.0.1)\n"
         "adds a door worth knocking on.", ACCENT),
        ("2", "Knock",
         "The beacon completes a real TCP handshake and sends its TLS\n"
         "hello. The intended hostname (SNI) is read from it, the peer PID\n"
         "is captured from the OS connection table, then WMI adds the\n"
         "process image and full command line.", GREEN),
        ("3", "Drop",
         "The connection is closed with no reply — ever. The attempt lands\n"
         "in the feed and the counters as plain JSONL. Nothing left the\n"
         "machine at any point, in either pin mode.", RED),
    ]
    y0 = 156
    for i, (n, t, body, accent) in enumerate(steps):
        y = y0 + i * 222
        card(ax, 796, y, 648, 200, face=SURFACE, bar=accent)
        ax.text(824, y + 52, n, fontsize=24, color=accent, fontweight="bold", va="center")
        ax.text(872, y + 36, t, fontsize=16.5, color=NAVY, fontweight="bold", va="center")
        ax.text(872, y + 96, body, fontsize=11.8, color=INK, va="center", linespacing=1.55)

    footer(ax, w, h, "The block stops the traffic — this shows you it working")
    fig.savefig(os.path.join(OUT, "corp_proof.png"), dpi=150)
    plt.close(fig)

# ---------------------------------------------------------------------------
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    corp_banner()
    corp_why()
    corp_visibility()
    corp_control()
    corp_proof()
    corp_hardening()
    corp_debloat()
    print("Corporate infographics written to", OUT)
