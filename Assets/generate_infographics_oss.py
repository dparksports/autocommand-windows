"""Generate the plain-language infographics for people trying AutoCommand
because they are tired of antivirus software.

House style adopted from the Z.ai Chat Recovery README assets (assets/*.svg):
Apple-like light theme — canvas #F5F5F7, white cards with #E5E5EA hairline
borders, #007AFF blue accent, #1E8E3E green / #C35C00 orange with tinted
bands (#E8F8EE, #E5F1FF, #FFF3E0), text #1D1D1F / #86868B. Font is Segoe UI
(fallback DejaVu Sans) at generous sizes: headings 34, card titles 24-25,
body 19 — bigger and more readable than the previous dark theme.

Copy note: the old "built-in helper" wording is gone; asking now means
clicking the row or inviting your own AI assistant (Be My Guest).

Lines are pre-wrapped by hand: matplotlib does not wrap text, and the exact
line counts are what the vertical layout is measured against.

Usage:  py Assets/generate_infographics_oss.py
Outputs: Assets/oss_alternative_infographic.png    (antivirus vs AutoCommand)
         Assets/plain_english_infographic.png      (see it / ask / stop it)
         Assets/first_five_minutes_infographic.png (getting started)
         Assets/row_anatomy_infographic.png        (how to read one row)
Requires: matplotlib, numpy  (py -m pip install matplotlib numpy)
"""
import os

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import Circle, FancyBboxPatch

matplotlib.rcParams["font.family"] = ["Segoe UI", "DejaVu Sans"]

# ---- palette (Z.ai Chat Recovery house style) ------------------------------
PAGE = "#F5F5F7"
CARD = "#FFFFFF"
BORDER = "#E5E5EA"
TEXT = "#1D1D1F"
MUTED = "#86868B"
BLUE = "#007AFF"
BLUE_TINT = "#E5F1FF"
GREEN = "#1E8E3E"
GREEN_TINT = "#E8F8EE"
ORANGE = "#C35C00"
ORANGE_TINT = "#FFF3E0"
RED = "#D70015"
RED_TINT = "#FDECEC"

W, H = 1600, 1000
MARGIN = 56
VERSION = "v2026.10.17.3"
OUT = os.path.dirname(os.path.abspath(__file__))


def new_canvas():
    fig = plt.figure(figsize=(W / 100, H / 100), dpi=150)
    fig.patch.set_facecolor(PAGE)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, W)
    ax.set_ylim(0, H)
    ax.invert_yaxis()
    ax.axis("off")
    return fig, ax


def card(ax, x, y, w, h, face=CARD, edge=BORDER, lw=1.6, rounding=16):
    ax.add_patch(FancyBboxPatch((x, y), w, h,
                                boxstyle=f"round,pad=0,rounding_size={rounding}",
                                linewidth=lw, edgecolor=edge, facecolor=face, zorder=1))


def header(ax, kicker, title, subtitle):
    ax.text(MARGIN, 62, kicker, color=BLUE, fontsize=16, fontweight="bold", va="center")
    ax.text(MARGIN, 112, title, color=TEXT, fontsize=34, fontweight="bold", va="center")
    ax.text(MARGIN, 156, subtitle, color=MUTED, fontsize=20, va="center")
    ax.text(W - MARGIN, 62, VERSION, color=MUTED, fontsize=14.5, fontweight="bold",
            ha="right", va="center",
            bbox=dict(boxstyle="round,pad=0.5", facecolor=CARD, edgecolor=BORDER, lw=1.2))


def green_footer(ax, y, text, h=58):
    card(ax, MARGIN, y, W - 2 * MARGIN, h, face=GREEN_TINT, edge="none", rounding=14)
    ax.text(W / 2, y + h / 2, text, color=GREEN, fontsize=18.5, fontweight="bold",
            ha="center", va="center", zorder=3)


def blue_band(ax, y, h, line1, line2):
    card(ax, MARGIN, y, W - 2 * MARGIN, h, face=BLUE_TINT, edge="none", rounding=14)
    ax.text(W / 2, y + h * 0.36, line1, color=BLUE, fontsize=19.5, fontweight="bold",
            ha="center", va="center", zorder=3)
    ax.text(W / 2, y + h * 0.70, line2, color=BLUE, fontsize=17.5, alpha=0.88,
            ha="center", va="center", zorder=3)


def pill(ax, x, y, text, tint=False, size=16):
    face, edge, color = (GREEN_TINT, "none", GREEN) if tint else (CARD, BORDER, TEXT)
    t = ax.text(x, y, text, color=color, fontsize=size, fontweight="bold",
                ha="left", va="center", zorder=3,
                bbox=dict(boxstyle="round,pad=0.55", facecolor=face, edgecolor=edge,
                          lw=1.4 if edge != "none" else 0))
    return t


def pill_row(ax, fig, y, items):
    """Items auto-space by measured width; ("text", True) renders as a green tint pill."""
    renderer = fig.canvas.get_renderer()
    px = MARGIN + 24
    for item in items:
        text, tint = (item, False) if isinstance(item, str) else item
        t = pill(ax, px, y, text, tint=tint)
        bb = t.get_window_extent(renderer=renderer).transformed(ax.transData.inverted())
        px = bb.x1 + 26


def chip(ax, x, y, text, size=15.5):
    ax.text(x, y, text, color=TEXT, fontsize=size, fontweight="bold",
            va="center", zorder=3,
            bbox=dict(boxstyle="round,pad=0.5", facecolor=CARD, edgecolor=BORDER, lw=1.4))


def bullet_block(ax, x, y, bullets_, size=19, lh=32, gap=16, dot=BLUE, color=TEXT):
    """Each bullet is a list of pre-wrapped lines; returns the running y cursor."""
    for lines in bullets_:
        ax.text(x - 22, y, "•", color=dot, fontsize=size + 2, fontweight="bold",
                va="center", ha="center", zorder=3)
        for i, seg in enumerate(lines):
            ax.text(x, y + i * lh, seg, color=color, fontsize=size, va="center", zorder=3)
        y += len(lines) * lh + gap
    return y


def divider(ax, x, y, w):
    ax.plot([x, x + w], [y, y], color=BORDER, lw=1.6, zorder=2)


def step_cards(ax, fig, steps, y=196, h=560):
    """steps: list of (number, title, [lines]). Blue number circles, white cards."""
    gap = 36
    pw = (W - 2 * MARGIN - 2 * gap) / 3
    for i, (num, title, lines) in enumerate(steps):
        x = MARGIN + i * (pw + gap)
        card(ax, x, y, pw, h)
        cx = x + pw / 2
        ax.add_patch(Circle((cx, y + 88), 30, facecolor=BLUE, edgecolor="none", zorder=2))
        ax.text(cx, y + 88, num, color="#FFFFFF", fontsize=26, fontweight="bold",
                ha="center", va="center", zorder=3)
        ax.text(cx, y + 170, title, color=TEXT, fontsize=25, fontweight="bold",
                ha="center", va="center", zorder=3)
        yy = y + 218
        for seg in lines:
            if seg:
                ax.text(cx, yy, seg, color=TEXT, fontsize=19, ha="center", va="center", zorder=3)
            yy += 34
        if i < 2:
            ax.annotate("", xy=(x + pw + gap - 8, y + 250), xytext=(x + pw + 8, y + 250),
                        arrowprops=dict(arrowstyle="-|>", color=BLUE, lw=2.8))
    return y + h


# ============================================================ 1: AV vs AutoCommand
def oss_alternative():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  FREE & OPEN SOURCE",
           "Your antivirus says “No threats found.”",
           "So why does your computer still feel wrong?")

    cy, ch = 196, 580
    cw = 676

    # Left: your antivirus
    card(ax, MARGIN, cy, cw, ch)
    ax.text(MARGIN + 32, cy + 54, "YOUR ANTIVIRUS", color=ORANGE, fontsize=24,
            fontweight="bold", va="center", zorder=3)
    bullet_block(ax, MARGIN + 56, cy + 106, [
        ["Matches files against a list of", "known bad ones — wanted-poster style."],
        ["Removes or quarantines what it", "recognizes."],
        ["Says “No threats found” when", "nothing on your PC matches its list."],
    ], dot=ORANGE, gap=14)
    divider(ax, MARGIN + 32, cy + 336, cw - 64)
    ax.text(MARGIN + 32, cy + 376, "WHAT IT CAN'T TELL YOU", color=MUTED, fontsize=18,
            fontweight="bold", va="center", zorder=3)
    bullet_block(ax, MARGIN + 56, cy + 420, [
        ["Why the fan is spinning when", "nothing is open."],
        ["What that program you don't recognize is."],
        ["Who any program is talking to right now."],
    ], dot=ORANGE, color=MUTED, gap=14)

    # Arrow between cards
    ax.annotate("", xy=(MARGIN + cw + 116, cy + ch / 2), xytext=(MARGIN + cw + 16, cy + ch / 2),
                arrowprops=dict(arrowstyle="-|>", color=BLUE, lw=3.2))

    # Right: AutoCommand
    rx = MARGIN + cw + 132
    card(ax, rx, cy, cw, ch)
    ax.text(rx + 32, cy + 54, "AUTOCOMMAND", color=BLUE, fontsize=24,
            fontweight="bold", va="center", zorder=3)
    bullet_block(ax, rx + 56, cy + 106, [
        ["Shows every program talking to the", "internet — live, in one list."],
        ["Names each one in plain English:", "“Chrome's updater, downloading", "from Google.”"],
        ["Explains the weird ones before you", "worry — and remembers what it saw,", "so yesterday's mystery is still", "solvable tomorrow."],
        ["One click stops anything you don't", "like. Undo works too."],
    ], dot=BLUE, gap=14)

    # Bottom: what it costs / where it runs
    pill_row(ax, fig, 816, [
        "FREE FOREVER", "OPEN SOURCE", "NO ACCOUNT NEEDED", "RUNS ON YOUR PC",
        ("NOTHING LEAVES IT", True),
    ])
    ax.text(W / 2, 872, "No subscription, no upsells, no cloud. Anyone can read the code and see exactly what it does.",
            color=MUTED, fontsize=18, ha="center", va="center", zorder=3)

    green_footer(ax, 906, "Works alongside your antivirus — it's the magnifying glass your guard dog was missing.")
    fig.savefig(os.path.join(OUT, "oss_alternative_infographic.png"), dpi=150, facecolor=PAGE)
    plt.close(fig)


# ============================================================ 2: see / ask / stop
def plain_english():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  NO TECHNICAL SKILLS NEEDED",
           "See something odd? Ask in plain English.",
           "You don't read logs. You shouldn't have to.")

    step_cards(ax, fig, [
        ("1", "SEE IT", [
            "Every program that talks",
            "to the internet, in one list.",
            "",
            "Each row names the program,",
            "shows where it connects and",
            "how much it sends — like a",
            "phone bill for your PC.",
        ]),
        ("2", "ASK ABOUT IT", [
            "Click any row: who started",
            "it, where it goes.",
            "",
            "Use an AI assistant? Invite",
            "it in — Be My Guest — to",
            "read the same facts, while",
            "you watch every question.",
        ]),
        ("3", "STOP IT", [
            "Don't like what you see?",
            "One click blocks it.",
            "",
            "Windows needs some of these",
            "programs, so AutoCommand",
            "warns you before anything",
            "important is touched — and",
            "every block has an undo.",
        ]),
    ])

    blue_band(ax, 786, 118,
              "Real example: “svchost.exe is uploading packets” turned out to be Chrome's updater",
              "pulling from Google's own download servers — proven in one row. No panic, no reinstall.")
    green_footer(ax, 924, "Three steps: see it, ask about it, stop it. That's the whole skill required.")
    fig.savefig(os.path.join(OUT, "plain_english_infographic.png"), dpi=150, facecolor=PAGE)
    plt.close(fig)


# ============================================================ 3: first five minutes
def first_five_minutes():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  FREE & OPEN SOURCE",
           "Try it in five minutes.",
           "No installer, no account, no obligation — look around and see what your PC is doing.")

    step_cards(ax, fig, [
        ("1", "OPEN IT", [
            "Download the zip and unzip",
            "it anywhere — no installer.",
            "",
            "Right-click AutoCommand.exe",
            "→ Run as administrator.",
            "",
            "(That lets it see the whole",
            "machine, not just your files.)",
        ]),
        ("2", "LOOK AROUND", [
            "The first screen lists every",
            "program talking to the",
            "internet right now — most",
            "are ordinary Windows parts.",
            "",
            "Click any row to see who",
            "started it and where it goes.",
            "Looking changes nothing.",
        ]),
        ("3", "ASK SOMETHING", [
            "See a name you don't know?",
            "Click it: who started it,",
            "where it goes.",
            "",
            "Use an AI assistant? Invite",
            "it in — Be My Guest — to",
            "read the same facts.",
            "A Block button is nearby.",
        ]),
    ])

    blue_band(ax, 786, 118,
              "If you can use a web browser, you can use this.",
              "No installer, no account, no subscription — and deleting the folder uninstalls it completely.")
    green_footer(ax, 924, "Free forever · open source · nothing you do ever leaves your computer.")
    fig.savefig(os.path.join(OUT, "first_five_minutes_infographic.png"), dpi=150, facecolor=PAGE)
    plt.close(fig)


# ============================================================ 4: how to read one row
def row_anatomy():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  WHAT AM I LOOKING AT?",
           "One row, five things to know.",
           "The main screen is a list of rows like this one — here's how to read it.")

    card(ax, MARGIN, 196, W - 2 * MARGIN, 516)

    rows_ = [
        ("WHO", "chrome.exe — the program's name",
         "Most are Windows parts or apps you already know. The odd ones are the interesting ones."),
        ("WHERE", "→ Google · United States — who's on the other end",
         "AutoCommand turns numbers like 47.246.23.183 into a company name you recognize, when it can."),
        ("HOW MUCH", "↓ 12 MB received, ↑ 340 KB sent",
         "A few kilobytes is a heartbeat. Gigabytes are worth a second look."),
        ("HOW LONG", "Connected since 2:14 PM — dead programs are marked “exited”",
         "And it remembers what it saw: yesterday's mystery is still solvable tomorrow."),
        ("BUTTONS", "Block — one click stops it. Undo puts it back.",
         "Windows needs some of these, so it warns you first — and a panic button restores everything."),
    ]

    y = 252
    for label, example, explain in rows_:
        chip(ax, 88, y, label)
        ax.text(260, y, example, color=TEXT, fontsize=19, va="center",
                fontweight="bold", zorder=3)
        ax.text(260, y + 30, explain, color=MUTED, fontsize=17, va="center", zorder=3)
        if label != "BUTTONS":
            divider(ax, 88, y + 58, W - 2 * MARGIN - 64)
        y += 88

    blue_band(ax, 748, 112,
              "When a row surprises you, that's the moment to click it — or invite a second opinion.",
              "You don't have to understand every row. Most people never need to.")
    green_footer(ax, 880, "Every label on this screen is something the app actually shows you — no guessing.")
    fig.savefig(os.path.join(OUT, "row_anatomy_infographic.png"), dpi=150, facecolor=PAGE)
    plt.close(fig)


if __name__ == "__main__":
    oss_alternative()
    plain_english()
    first_five_minutes()
    row_anatomy()
    print("wrote oss_alternative_infographic.png, plain_english_infographic.png, "
          "first_five_minutes_infographic.png, row_anatomy_infographic.png")
