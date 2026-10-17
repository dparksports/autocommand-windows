"""Generate the plain-language infographics for people trying AutoCommand
because they are tired of antivirus software.

Same house style as generate_infographics.py (dark cyber theme, matplotlib)
but sized for non-technical readers: bigger type throughout (body 17-19pt,
panel headers 21-23pt) and everyday wording — no jargon, no tab names.
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
from matplotlib.patches import FancyBboxPatch

matplotlib.rcParams["font.family"] = "DejaVu Sans"

# ---- palette (house) ------------------------------------------------------
BG_TOP = "#0A1526"
BG_BOT = "#0C2136"
PANEL = "#0F1E33"
PANEL_EDGE = "#2A4A73"
CYAN = "#00E5FF"
CYAN_DIM = "#0E6E8C"
TXT = "#E8F4FF"
SUB = "#8FAECB"
ORANGE = "#FFAE42"
GREEN = "#3DDC84"

W, H = 1600, 1000
VERSION = "v2026.10.17.2"
OUT = os.path.dirname(os.path.abspath(__file__))


def new_canvas():
    fig = plt.figure(figsize=(W / 100, H / 100), dpi=150)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, W)
    ax.set_ylim(0, H)
    ax.invert_yaxis()
    ax.axis("off")
    grad = np.linspace(0, 1, 256).reshape(-1, 1)
    ax.imshow(grad, extent=[0, W, H, 0], aspect="auto",
              cmap=matplotlib.colors.LinearSegmentedColormap.from_list("bg", [BG_TOP, BG_BOT]), zorder=-2)
    rng = np.random.default_rng(42)
    dots_x, dots_y = rng.uniform(0, W, 90), rng.uniform(0, H, 90)
    ax.scatter(dots_x, dots_y, s=rng.uniform(2, 14, 90), color=CYAN, alpha=rng.uniform(0.04, 0.16, 90), zorder=-1)
    return fig, ax


def panel(ax, x, y, w, h, edge=PANEL_EDGE, lw=1.8, face=PANEL, alpha=0.94):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=16",
                                linewidth=lw, edgecolor=edge, facecolor=face, alpha=alpha, zorder=1))


def topbar(ax, x, y, w, accent, h=10):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=5",
                                linewidth=0, facecolor=accent, zorder=2))


def chip(ax, x, y, text, color=CYAN, edge=CYAN_DIM, size=14.5):
    ax.text(x, y, text, color=color, fontsize=size, fontweight="bold",
            ha="left", va="center",
            bbox=dict(boxstyle="round,pad=0.5", facecolor=PANEL, edgecolor=edge, lw=1.6), zorder=3)


def header(ax, kicker, title, subtitle):
    panel(ax, 40, 34, W - 80, 150, edge=CYAN_DIM, lw=2.4, alpha=0.96)
    ax.add_patch(FancyBboxPatch((40, 34), 16, 150, boxstyle="round,pad=0,rounding_size=8",
                                linewidth=0, facecolor=CYAN, zorder=2))
    ax.text(92, 78, kicker, color=CYAN, fontsize=17, fontweight="bold", va="center")
    ax.text(92, 126, title, color=TXT, fontsize=29, fontweight="bold", va="center")
    ax.text(92, 162, subtitle, color=SUB, fontsize=18.5, va="center")
    ax.text(W - 72, 109, VERSION, color=BG_TOP, fontsize=19, fontweight="bold",
            ha="right", va="center",
            bbox=dict(boxstyle="round,pad=0.5", facecolor=CYAN, edgecolor="none"))


def footer(ax, text):
    panel(ax, 40, H - 78, W - 80, 50, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, H - 53, text, color=CYAN, fontsize=15.5, ha="center", va="center", fontweight="bold")


def bullet_block(ax, x, y, bullets_, size=17.5, lh=30, gap=18, dot=CYAN, color=TXT):
    """Each bullet is a list of pre-wrapped lines. Running cursor: wrapping
    advances it, so continuation lines can never collide with the next bullet."""
    for lines in bullets_:
        ax.text(x - 24, y, "•", color=dot, fontsize=size + 2, fontweight="bold",
                va="center", ha="center")
        for i, seg in enumerate(lines):
            ax.text(x, y + i * lh, seg, color=color, fontsize=size, va="center")
        y += len(lines) * lh + gap
    return y


def divider(ax, x, y, w, color=PANEL_EDGE):
    ax.plot([x, x + w], [y, y], color=color, lw=1.6, zorder=2)


# ============================================================ 1: AV vs AutoCommand
def oss_alternative():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  FREE & OPEN SOURCE",
           "Your antivirus says “No threats found.”",
           "So why does your computer still feel wrong?")

    # Left: your antivirus
    lx, ly, lw_, lh_ = 40, 210, 690, 480
    panel(ax, lx, ly, lw_, lh_, edge="#7A5A20", lw=2.0)
    topbar(ax, lx, ly, lw_, ORANGE)
    ax.text(lx + 30, ly + 56, "YOUR ANTIVIRUS", color=ORANGE, fontsize=23, fontweight="bold", va="center")
    bullet_block(ax, lx + 54, ly + 104, [
        ["Checks files against a list of known bad", "ones — like a wanted-poster matching game."],
        ["Removes or quarantines what it recognizes."],
        ["Says “No threats found” when nothing", "on your PC matches its list."],
    ], dot=ORANGE, gap=14)
    divider(ax, lx + 30, ly + 288, lw_ - 60, "#7A5A20")
    ax.text(lx + 30, ly + 320, "WHAT IT CAN'T TELL YOU", color=SUB, fontsize=19, fontweight="bold", va="center")
    bullet_block(ax, lx + 54, ly + 356, [
        ["Why the fan is spinning when nothing is open."],
        ["What that program you don't recognize is."],
        ["Who any program is talking to right now."],
    ], dot="#C87F2E", color="#F2E4CC", gap=16)

    # Arrow between panels
    ax.annotate("", xy=(908, 450), xytext=(742, 450),
                arrowprops=dict(arrowstyle="-|>", color=CYAN, lw=3.4))

    # Right: AutoCommand
    rx, ry, rw, rh = 870, 210, 690, 480
    panel(ax, rx, ry, rw, rh, edge=CYAN_DIM, lw=2.4)
    topbar(ax, rx, ry, rw, CYAN)
    ax.text(rx + 30, ry + 56, "AUTOCOMMAND", color=CYAN, fontsize=23, fontweight="bold", va="center")
    bullet_block(ax, rx + 54, ry + 108, [
        ["Shows every program talking to the internet", "— live, in one list."],
        ["Names each one in plain English: “Chrome's", "updater, downloading from Google.”"],
        ["Explains the weird ones before you worry —", "and remembers what it saw, so yesterday's", "mystery is still solvable."],
        ["One click stops anything you don't like.", "Undo works too."],
    ])

    # Bottom band: what it costs / where it runs
    panel(ax, 40, 714, W - 80, 168, edge=PANEL_EDGE, alpha=0.95)
    chip(ax, 84, 768, "FREE FOREVER")
    chip(ax, 330, 768, "OPEN SOURCE")
    chip(ax, 576, 768, "NO ACCOUNT NEEDED")
    chip(ax, 890, 768, "RUNS ON YOUR PC")
    chip(ax, 1204, 768, "NOTHING LEAVES IT", color=GREEN, edge="#2E7D5B")
    ax.text(W / 2, 838, "No subscription, no upsells, no cloud. Anyone can read the code and see exactly what it does.",
            color=SUB, fontsize=18, ha="center", va="center")

    footer(ax, "Works alongside your antivirus — it's the magnifying glass your guard dog was missing.")
    fig.savefig(os.path.join(OUT, "oss_alternative_infographic.png"), dpi=150)
    plt.close(fig)


# ============================================================ 2: see / ask / stop
def plain_english():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  NO TECHNICAL SKILLS NEEDED",
           "See something odd? Ask in plain English.",
           "You don't read logs. You shouldn't have to.")

    steps = [
        ("1", "SEE IT", CYAN, [
            "Every program that talks",
            "to the internet, in one list.",
            "",
            "Each row names the program,",
            "shows where it's connected,",
            "and how much it's sending —",
            "like a phone bill for your PC.",
        ]),
        ("2", "ASK ABOUT IT", ORANGE, [
            "Ask the way you'd say it",
            "out loud: “What is this?",
            "Should I worry?”",
            "",
            "The built-in helper investigates",
            "and answers in plain words.",
            "It shows its work — nothing",
            "is guessed.",
        ]),
        ("3", "STOP IT", GREEN, [
            "Don't like what you see?",
            "One click blocks it.",
            "",
            "Windows needs some of these",
            "programs, so AutoCommand warns",
            "you before anything important",
            "is touched — and every block",
            "has an undo.",
        ]),
    ]

    pw, gap = 474, 34
    x0 = (W - (3 * pw + 2 * gap)) / 2
    for i, (num, title, accent, lines) in enumerate(steps):
        x = x0 + i * (pw + gap)
        panel(ax, x, 210, pw, 470, edge=CYAN_DIM, lw=2.2)
        topbar(ax, x, 210, pw, accent)
        ax.text(x + pw / 2, 272, num, color=accent, fontsize=40, fontweight="bold",
                ha="center", va="center",
                bbox=dict(boxstyle="circle,pad=0.3", facecolor=PANEL, edgecolor=accent, lw=2.6))
        ax.text(x + pw / 2, 362, title, color=TXT, fontsize=23, fontweight="bold",
                ha="center", va="center")
        yy = 400
        first = True
        for seg in lines:
            if seg:
                ax.text(x + pw / 2, yy, seg, color=TXT if first else SUB,
                        fontsize=17, ha="center", va="center")
                first = False
            yy += 33
        if i < 2:
            ax.annotate("", xy=(x + pw + gap - 5, 440), xytext=(x + pw + 5, 440),
                        arrowprops=dict(arrowstyle="-|>", color=CYAN_DIM, lw=3.0))

    panel(ax, 40, 714, W - 80, 168, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, 772, "Real example: “svchost.exe is uploading packets” turned out to be Chrome's updater",
            color=TXT, fontsize=19, ha="center", va="center", fontweight="bold")
    ax.text(W / 2, 822, "pulling from Google's own download servers. AutoCommand proved it in one row — no panic, no reinstall.",
            color=SUB, fontsize=17.5, ha="center", va="center")

    footer(ax, "Three steps: see it, ask about it, stop it. That's the whole skill required.")
    fig.savefig(os.path.join(OUT, "plain_english_infographic.png"), dpi=150)
    plt.close(fig)


# ============================================================ 3: first five minutes
def first_five_minutes():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  FREE & OPEN SOURCE",
           "Try it in five minutes.",
           "No installer, no account, no obligation — look around and see what your PC is doing.")

    steps = [
        ("1", "OPEN IT", CYAN, [
            "Download the zip and unzip it",
            "anywhere — no installer.",
            "",
            "Right-click AutoCommand.exe →",
            "Run as administrator.",
            "",
            "(That lets it see the whole",
            "machine, not just your files.)",
        ]),
        ("2", "LOOK AROUND", ORANGE, [
            "The first screen lists every",
            "program talking to the",
            "internet right now — most are",
            "ordinary Windows parts.",
            "",
            "Click any row to see who",
            "started it and where it goes.",
            "Looking changes nothing.",
        ]),
        ("3", "ASK SOMETHING", GREEN, [
            "See a name you don't know?",
            "",
            "Ask the built-in helper,",
            "like you'd ask a person:",
            "“What is this? Should I worry?”",
            "",
            "You get a plain-English answer",
            "— and a Block button.",
        ]),
    ]

    pw, gap = 474, 34
    x0 = (W - (3 * pw + 2 * gap)) / 2
    for i, (num, title, accent, lines) in enumerate(steps):
        x = x0 + i * (pw + gap)
        panel(ax, x, 210, pw, 470, edge=CYAN_DIM, lw=2.2)
        topbar(ax, x, 210, pw, accent)
        ax.text(x + pw / 2, 272, num, color=accent, fontsize=40, fontweight="bold",
                ha="center", va="center",
                bbox=dict(boxstyle="circle,pad=0.3", facecolor=PANEL, edgecolor=accent, lw=2.6))
        ax.text(x + pw / 2, 362, title, color=TXT, fontsize=23, fontweight="bold",
                ha="center", va="center")
        yy = 400
        first = True
        for seg in lines:
            if seg:
                ax.text(x + pw / 2, yy, seg, color=TXT if first else SUB,
                        fontsize=17, ha="center", va="center")
                first = False
            yy += 33
        if i < 2:
            ax.annotate("", xy=(x + pw + gap - 5, 440), xytext=(x + pw + 5, 440),
                        arrowprops=dict(arrowstyle="-|>", color=CYAN_DIM, lw=3.0))

    panel(ax, 40, 714, W - 80, 168, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, 772, "If you can use a web browser, you can use this.",
            color=TXT, fontsize=19, ha="center", va="center", fontweight="bold")
    ax.text(W / 2, 822, "No installer, no account, no subscription — and deleting the folder uninstalls it completely.",
            color=SUB, fontsize=17.5, ha="center", va="center")

    footer(ax, "Free forever · open source · nothing you do ever leaves your computer.")
    fig.savefig(os.path.join(OUT, "first_five_minutes_infographic.png"), dpi=150)
    plt.close(fig)


# ============================================================ 4: how to read one row
def row_anatomy():
    fig, ax = new_canvas()
    header(ax, "AUTOCOMMAND  •  WHAT AM I LOOKING AT?",
           "One row, five things to know.",
           "The main screen is a list of rows like this one — here's how to read it.")

    panel(ax, 40, 210, W - 80, 470, edge=CYAN_DIM, lw=2.2)
    topbar(ax, 40, 210, W - 80, CYAN)

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

    y = 268
    for label, example, explain in rows_:
        chip(ax, 84, y + 15, label, size=15)
        ax.text(270, y, example, color=TXT, fontsize=17.5, va="center", fontweight="bold")
        ax.text(270, y + 32, explain, color=SUB, fontsize=16, va="center")
        y += 84

    panel(ax, 40, 714, W - 80, 168, edge=PANEL_EDGE, alpha=0.95)
    ax.text(W / 2, 772, "When a row surprises you, that's the moment to click it or ask the helper — not before.",
            color=TXT, fontsize=19, ha="center", va="center", fontweight="bold")
    ax.text(W / 2, 822, "You don't have to understand every row. Most people never need to.",
            color=SUB, fontsize=17.5, ha="center", va="center")

    footer(ax, "Every label on this screen is something the app actually shows you — no guessing.")
    fig.savefig(os.path.join(OUT, "row_anatomy_infographic.png"), dpi=150)
    plt.close(fig)


if __name__ == "__main__":
    oss_alternative()
    plain_english()
    first_five_minutes()
    row_anatomy()
    print("wrote oss_alternative_infographic.png, plain_english_infographic.png, "
          "first_five_minutes_infographic.png, row_anatomy_infographic.png")
