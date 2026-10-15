"""AutoCommand README infographics — Fluent light theme, matching the app.

Same palette as ThemeService Light: canvas #F0F2F6, white cards, hairline
#D9DEE7 borders, muted blue #2F74B5 accent, text #1A1D23 / #5B6472.
Single-hue blue identity (no secondary accent colors), soft card shadows.

Design rules (unchanged): ONE headline (<= 7 words), ONE subline (<= 12 words),
exactly 3 steps: big number + 2-4 word label + ONE line of <= 9 words.

Usage:  python Assets/regenerate_infographics.py
Outputs: corp_banner, corp_why, corp_debloat, corp_visibility,
         corp_control, corp_hardening, corp_ai (.png, 2250x1410)
Requires: matplotlib
"""
import os
import textwrap

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Circle, FancyBboxPatch

matplotlib.rcParams["font.family"] = "DejaVu Sans"

# ── Fluent light palette (identical to the app's ThemeService) ───────────
PAGE = "#F0F2F6"
CARD = "#FFFFFF"
BORDER = "#D9DEE7"
TEXT = "#1A1D23"
MUTED = "#5B6472"
BLUE = "#2F74B5"        # primary accent
BLUE_BRIGHT = "#3E86D6" # gradient partner
BLUE_SOFT = "#E2EAF4"   # nav/selection tint
SHADOW = "#94A3B8"      # card shadow tint

W, H = 2250, 1410
MARGIN = 130
OUT = os.path.dirname(os.path.abspath(__file__))


def canvas():
    fig = plt.figure(figsize=(W / 100, H / 100), dpi=100)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, W)
    ax.set_ylim(0, H)
    ax.invert_yaxis()
    ax.axis("off")
    fig.patch.set_facecolor(PAGE)
    # top accent bar: the app's blue→blue identity gradient, simplified two-tone
    ax.add_patch(FancyBboxPatch((0, 0), W / 2, 10, boxstyle="round,pad=0,rounding_size=0",
                                linewidth=0, facecolor=BLUE))
    ax.add_patch(FancyBboxPatch((W / 2, 0), W / 2, 10, boxstyle="round,pad=0,rounding_size=0",
                                linewidth=0, facecolor=BLUE_BRIGHT))
    return fig, ax


def header(ax, fig, kicker, title, subline):
    ax.text(MARGIN, 130, kicker.upper(), fontsize=20, color=BLUE,
            fontweight="bold", va="center")
    # two-tone headline: last word in the accent blue
    parts = title.rsplit(" ", 1)
    if len(parts) == 2:
        left, right = parts[0] + " ", parts[1]
        t1 = ax.text(MARGIN, 215, left, fontsize=46, color=TEXT, fontweight="bold", va="center")
        w1 = t1.get_window_extent(fig.canvas.get_renderer()).transformed(ax.transData.inverted()).x1
        ax.text(w1 + 14, 215, right, fontsize=46, color=BLUE, fontweight="bold", va="center")
    else:
        ax.text(MARGIN, 215, title, fontsize=46, color=TEXT, fontweight="bold", va="center")
    ax.text(MARGIN, 300, subline, fontsize=25, color=MUTED, va="center")


def step_card(ax, x, y, w, h, num, label, line):
    # Fluent floating card: soft shadow, then white surface + hairline border
    ax.add_patch(FancyBboxPatch((x + 6, y + 10), w, h, boxstyle="round,pad=0,rounding_size=28",
                                linewidth=0, facecolor=SHADOW, alpha=0.16))
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=28",
                                linewidth=1.6, edgecolor=BORDER, facecolor=CARD))
    cx = x + 110
    cy = y + h / 2
    ax.add_patch(Circle((cx, cy), 56, facecolor=BLUE_SOFT, edgecolor="none"))
    ax.text(cx, cy, str(num), fontsize=40, color=BLUE, fontweight="bold",
            ha="center", va="center")
    x_text = x + 205
    usable = w - 205 - 70
    wrap_at = max(14, int(usable / 12.2))
    lines = textwrap.wrap(line, wrap_at)[:3]
    ax.text(x_text, cy - 58, label, fontsize=34, color=TEXT, fontweight="bold", va="center")
    for i, ln in enumerate(lines):
        ax.text(x_text, cy + 14 + i * 46, ln, fontsize=22, color=MUTED, va="center")


def three_steps(ax, fig, steps):
    gap = 60
    cw = (W - 2 * MARGIN - 2 * gap) / 3
    y, h = 480, 620
    for i, (label, line) in enumerate(steps):
        step_card(ax, MARGIN + i * (cw + gap), y, cw, h, i + 1, label, line)


def pill_row(ax, fig, items, y, fontsize=23):
    renderer = fig.canvas.get_renderer()
    px = MARGIN
    for p in items:
        t = ax.text(px, y, p, fontsize=fontsize, color=BLUE, va="center", fontweight="bold",
                    bbox=dict(boxstyle="round,pad=0.65", facecolor=CARD,
                              edgecolor=BLUE, linewidth=2))
        bb = t.get_window_extent(renderer=renderer).transformed(ax.transData.inverted())
        px = bb.x1 + 55


def finish(fig, name):
    ax = fig.axes[0]
    ax.text(W - MARGIN, H - 60, "AutoCommand · Apache-2.0", fontsize=15,
            color=MUTED, ha="right", va="center")
    fig.savefig(os.path.join(OUT, name), dpi=100, facecolor=PAGE)
    plt.close(fig)
    print("wrote", name)


# ── 1. banner ────────────────────────────────────────────────────────────
def banner():
    fig, ax = canvas()
    ax.text(MARGIN, 520, "Auto", fontsize=104, color=TEXT, fontweight="bold", va="center")
    w_auto = ax.texts[-1].get_window_extent(fig.canvas.get_renderer()).transformed(ax.transData.inverted()).x1
    ax.text(w_auto + 12, 520, "Command", fontsize=104, color=BLUE, fontweight="bold", va="center")
    ax.text(MARGIN, 690, "See what your Windows PC is really doing — then take it back, one click at a time.",
            fontsize=30, color=MUTED, va="center")
    pill_row(ax, fig, ["See every connection", "Know who launched it",
                       "Block in one click", "Undo just as fast",
                       "AI only with consent"], 900)
    finish(fig, "corp_banner.png")


# ── 2. why ───────────────────────────────────────────────────────────────
def why():
    fig, ax = canvas()
    header(ax, fig, "Why AutoCommand", "Twelve tools. One dashboard.",
           "The questions that cost an afternoon become a row, a color, a click.")
    three_steps(ax, fig, [
        ("Spot it", "The process, the IP, the owner — one row."),
        ("Understand it", "Who launched it, and what it does."),
        ("Take it back", "Block, kill, disable — one click each."),
    ])
    finish(fig, "corp_why.png")


# ── 3. debloat ───────────────────────────────────────────────────────────
def debloat():
    fig, ax = canvas()
    header(ax, fig, "Quick Scan", "A fresh Windows, clean in one pass.",
           "Debloat, firewall, privacy and hardening — one guided checklist.")
    three_steps(ax, fig, [
        ("Sweep", "23 preinstalled apps gone in one confirmation."),
        ("Lock down", "Firewall, privacy and hardening presets applied."),
        ("Verify", "Every step re-checks itself when the pass ends."),
    ])
    finish(fig, "corp_debloat.png")


# ── 4. visibility ────────────────────────────────────────────────────────
def visibility():
    fig, ax = canvas()
    header(ax, fig, "Process Monitor + Sysmon Audit", "See every connection — know who launched it.",
           "A live, attributed view that survives process exit.")
    three_steps(ax, fig, [
        ("Watch", "Live grid: process, destination, hostname."),
        ("Attribute", "The parent survives process exit — no mystery PIDs."),
        ("Act", "Block the IP or app; kill the process."),
    ])
    finish(fig, "corp_visibility.png")


# ── 5. control ───────────────────────────────────────────────────────────
def control():
    fig, ax = canvas()
    header(ax, fig, "Update Control + Telemetry", "Pin the version that works. Silence the beacons.",
           "Apps run normally; their self-updaters fail politely.")
    three_steps(ax, fig, [
        ("Freeze", "The app runs; its updater is locked out."),
        ("Opt out", "The vendor's own documented switch — nothing hacked."),
        ("Verify quiet", "The DNS cache proves the beacons stopped."),
    ])
    finish(fig, "corp_control.png")


# ── 6. hardening ─────────────────────────────────────────────────────────
def hardening():
    fig, ax = canvas()
    header(ax, fig, "OS Hardening + DBX Safety", "Lock it down — and keep it locked.",
           "Guards that re-assert themselves when something drifts back.")
    three_steps(ax, fig, [
        ("Guard", "SSTP and kernel-debug surfaces stay off."),
        ("Watch", "The Secure Boot chain is hashed and checked."),
        ("Confirm first", "DBX updates apply only with your yes."),
    ])
    finish(fig, "corp_hardening.png")


# ── 7. be my guest (AI access) ───────────────────────────────────────────
def ai():
    fig, ax = canvas()
    header(ax, fig, "Be My Guest", "You decide what the AI reads.",
           "Consent first, a live activity log, and one click to revoke.")
    three_steps(ax, fig, [
        ("Consent", "One dialog states exactly what is readable."),
        ("Watch", "Every tool call lands in the activity log."),
        ("Revoke", "Stop the session — its token dies with it."),
    ])
    finish(fig, "corp_ai.png")


if __name__ == "__main__":
    banner(); why(); debloat(); visibility(); control(); hardening(); ai()
