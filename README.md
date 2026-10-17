# AutoCommand — see what your Windows PC is really doing

![AutoCommand](Assets/corp_banner.png?v=fluent2)

**Your antivirus says "No threats found." Your computer still feels wrong anyway.**

Antivirus programs can only catch things they already know about — like a guard dog matching faces against a book of wanted posters. When nothing matches, they say "no threats found" and go back to sleep. But that answer doesn't help with the questions you actually have at 11 p.m.: *what is this program? why is my fan spinning? who is my computer talking to right now?*

This free, open-source app answers those questions in plain English. It shows you every program on your Windows PC that's talking to the internet, names each one ("Chrome's updater, downloading from Google"), explains the strange ones before you worry — and stops anything you don't want, with one click and an undo button.

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

## What do I do?

1. **Download** the app from [Releases](../../releases) — `AutoCommand-v…-win-x64.zip` for Windows 10 or 11. Unzip it.
2. **Run `AutoCommand.exe` as Administrator** (right-click → *Run as administrator* — it needs permission because it watches the whole machine, not just your files). The list you see first is every program currently talking to the internet, each row named in plain words.
3. **Ask about anything that looks odd.** Type your question the way you'd say it out loud — *"what is this? should I worry?"* — and the built-in helper investigates and answers in plain words. If you decide you don't like something, the same window has a **Block** button.

That's it. Watch, ask, block.

![Three steps](Assets/plain_english_infographic.png?v=oss1)

> **One thing to know:** AutoCommand is not a second antivirus, and you don't have to remove yours. Antivirus scans files; AutoCommand shows you what's *happening* — the visibility your antivirus never gives you. They do different jobs, and this is the missing one.

## Why can't my antivirus answer these questions?

![Why your antivirus can't help](Assets/oss_alternative_infographic.png?v=oss1)

Because scanning and understanding are different jobs. A scanner compares files against a list of known bad ones; it has nothing to say about a perfectly "clean" program that's phoning home more than it should, or a Windows part doing something unusual. AutoCommand watches behavior and explains it — and it remembers what it saw, so yesterday's mystery is still solvable tomorrow.

## Common questions

| What you see | What to do |
|---|---|
| "A program I don't recognize is on the list" | Most are normal Windows parts doing their jobs. Double-click the row — AutoCommand shows who started it, where it's connecting, and whether that's usual. Still unsure? Ask the helper. |
| "svchost.exe is sending data and I've heard that's bad" | It's Windows' own delivery service. AutoCommand shows exactly what it delivered and from where — usually an update from Google, Microsoft, or your browser maker. Real case: "svchost uploading packets" turned out to be Chrome's updater on Google's download servers, proven in one row. |
| "It asked for Administrator permission" | That's how it can see system processes and other users' programs, not just yours. It never does anything with that permission without asking you first. |
| "Should I get rid of my antivirus?" | No need — keep it for scanning. AutoCommand is the other half: it shows and explains what's happening live. |
| "What does it send about me?" | Nothing, by default. Everything happens on your computer. (The one optional extra — an anonymous usage count, no identifiers at all — is off unless you say yes, and you can turn it off anytime.) |
| "I blocked something and the internet broke" | Breathe. The 🚑 **Restore Internet** button in the corner turns off every block AutoCommand made, in one click. |

## When in doubt

**Look, ask, then block — in that order.** Every block is reversible, and AutoCommand warns you before touching anything Windows needs. The dangerous move was always the confident guess; this app exists so you never need one.

## What else is inside

The same plain-English treatment across a few more windows: a **"phone bill"** view of who talks to whom and how much, **one-click firewall presets**, a tab that shows **what Windows delivered and updated overnight**, a **telemetry opt-out** section that proves apps actually honored your "stop sending data" setting — and the same honest rule everywhere: *nothing runs without you, nothing is guessed, and everything it remembers is a readable file on your own disk.*

The complete list — every tab, every technical detail, every known limitation — is documented in plain sight in the [Technical Notes](docs/TECHNICAL_NOTES.md).

> **🔧 Setting up a new PC instead?** The first-hour ritual — debloat, firewall presets, privacy opt-outs — lives in its own app: [**Shields Up**](https://github.com/dparksports/shields-up/releases/latest) (same author, same undo-first design).

## Downloads

| File | For |
|---|---|
| `AutoCommand-v…-win-x64.zip` | Windows 10 and 11 — unzip anywhere, right-click `AutoCommand.exe` → **Run as administrator**. No installer, nothing else to install. |

**Windows note:** Windows may show a blue "protected your PC" screen because the app isn't from a big company. Click **More info → Run anyway**. (The app is open source — anyone can read exactly what it does, and that's rather the point.)

## Your privacy, in one sentence

Everything happens on your computer; the app talks to no one by default, and the only optional exception is an anonymous usage count with no identifier of any kind — off unless you say yes.

## For developers and tinkerers

```powershell
# .NET 10 SDK required
git clone https://github.com/dparksports/autocommand-windows.git
cd autocommand-windows
dotnet build AutoCommand.csproj -c Release

# self-contained build (what the releases ship)
dotnet publish AutoCommand.csproj -c Release -r win-x64 --self-contained
```

How it all actually works — the IP Helper connection table, Sysmon, the svchost decoder, the AI-assistant consent model, every engine's offline self-test — is documented in **[TECHNICAL_NOTES.md](docs/TECHNICAL_NOTES.md)**. Apache 2.0.

## Credits & license

Made with ❤️ in California

Apache License 2.0 — free to use, share, and modify

© 2026 Dan Park, magicpoint.ai

Questions, stories, things you caught? [Say hello in Discussions](../../discussions).
