# AutoCommand — see what your Windows PC is really doing

![AutoCommand](Assets/corp_banner.png?v=light1)

**Tired of antivirus software? Nagging popups, yearly subscriptions, scans that finish with "no threats found" — and still no answer when your computer acts weird?**

You don't need another guard dog. You need to *see what's going on*. AutoCommand is a free, open-source app that shows you every program on your PC that's talking to the internet, explains each one in plain English, and stops anything you don't want — one click, undo included. It sends nothing anywhere, costs nothing, and you can delete it when you're done.

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

## Try it in five minutes

![Try it in five minutes](Assets/first_five_minutes_infographic.png?v=oss3)

1. **Download** `AutoCommand-v…-win-x64.zip` from [Releases](../../releases) and unzip it anywhere. There's no installer.
2. **Right-click `AutoCommand.exe` → Run as administrator.** (That permission lets it watch the whole machine — the same reason Task Manager asks.) The first screen lists every program talking to the internet right now.
3. **Look around, ask something.** Click any row to see who started it. And if you use an AI assistant, the **Be My Guest** tab can invite it to read the same facts — but only while you keep the door open (more below).

That's the whole tutorial. Looking changes nothing; deleting the folder uninstalls it completely.

> **Windows may show a blue "protected your PC" screen** because the app isn't from a big company. Click **More info → Run anyway**. It's open source — anyone can read exactly what it does, and that's rather the point.

## What am I looking at?

![How to read one row](Assets/row_anatomy_infographic.png?v=oss3)

The main screen is a list of rows — one per connection. Every row tells you who's talking, to whom, how much, for how long, and gives you a Block button (with an undo, and a panic button that restores everything). When a row surprises you, *that's* the moment to click it or ask for a second opinion — not before. Most rows, you'll never need to think about.

## When something looks odd

![See it, ask, stop it](Assets/plain_english_infographic.png?v=oss3)

Real example, from this app's own development machine: *"svchost.exe is uploading packets"* sounds alarming — it turned out to be Chrome's updater downloading from Google's own servers, and AutoCommand proved it in one row. No panic, no reinstall, no forum archaeology at midnight.

## Why this instead of another antivirus?

![Why your antivirus can't help](Assets/oss_alternative_infographic.png?v=oss3)

Keep your antivirus if you like it — this isn't a replacement, and you don't have to uninstall anything. It's the piece antivirus never gives you: **visibility**. A scanner matches files against a list of known bad ones; it has nothing to say about a "clean" program phoning home more than it should. AutoCommand watches behavior, names it in plain words, remembers what it saw (so yesterday's mystery is still solvable tomorrow), and never asks for a subscription, an account, or your credit card.

## Common questions

| What you see | What to do |
|---|---|
| "A program I don't recognize is on the list" | Most are normal Windows parts. Double-click the row — it shows who started it and where it connects. Still unsure? Invite your AI assistant to take a look — see **Be My Guest** below. |
| "It asked for Administrator permission" | That's how it sees system processes, not just yours. It never uses that permission without asking you first. |
| "Should I remove my antivirus?" | Your call — they do different jobs. AutoCommand shows what's happening; antivirus scans files. Many people run both. |
| "What does it send about me?" | Nothing, by default. Everything happens on your computer. (The one optional extra — an anonymous usage count, no identifiers at all — is off unless you say yes.) |
| "I blocked something and the internet broke" | Breathe. The 🚑 **Restore Internet** button in the corner undoes every block AutoCommand made, in one click. |

## Be My Guest — your AI assistant, invited

Maybe you've already asked ChatGPT or Claude about a weird-sounding program — and had to describe your screen in words, because an assistant can't see your computer. The **Be My Guest** tab finishes that thought, carefully. It works like having a guest over:

- **Nobody comes in until you open the door.** AI access is off until you start it. Stop the session — or close the app — and the invitation is withdrawn on the spot.
- **A guest looks; a guest doesn't wander.** Your assistant can't run commands, open files, or rummage through your PC. It can only ask AutoCommand's own questions — the same ones the app already answers for you: *what is this program, who started it, where is it going, is that normal?*
- **Nothing is touched without your yes.** Looking is all it can do unless you also switch on actions — and even then, every block, stop, or change shows the same confirmation box you'd see if you'd clicked it yourself. It can suggest; you decide.
- **You see every question it asked.** The tab keeps a plain-English list of every request and answer, for as long as the visit lasts.

The door only opens for assistants that speak a standard connection language called **MCP** — Claude Desktop and other MCP-capable assistants qualify. If yours doesn't, nothing happens and nothing is lost: AutoCommand works exactly the same with the door shut.

One honest line about privacy, because you'd wonder: AutoCommand itself still talks to no one. It hands facts to your assistant's own app on your own PC — and where that app sends them is the trust you already chose when you picked it. Prefer it saw nothing? Leave the door closed. You lose nothing.

## What else is inside

The same plain-English treatment across a few more windows: a **"phone bill"** view of who talks to whom and how much, **one-click firewall presets**, a tab showing **what Windows delivered and updated overnight**, and a **telemetry opt-out** section that proves apps actually honored your "stop sending data" setting. One rule everywhere: *nothing runs without you, nothing is guessed, and everything it remembers is a readable file on your own disk.* The complete, honest list — including every limitation — is in the [Technical Notes](docs/TECHNICAL_NOTES.md).

> **🔧 Setting up a new PC instead?** The first-hour ritual — debloat, firewall presets, privacy opt-outs — lives in its own app: [**Shields Up**](https://github.com/dparksports/shields-up/releases/latest) (same author, same undo-first design).

## Downloads

| File | For |
|---|---|
| `AutoCommand-v…-win-x64.zip` | Windows 10 and 11 — unzip anywhere, right-click `AutoCommand.exe` → **Run as administrator**. Nothing else to install. |

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

How it actually works — the dual-family kernel connection table, the Sysmon archive, the svchost decoder, the AI-assistant consent model, every engine's offline self-test — is documented in **[TECHNICAL_NOTES.md](docs/TECHNICAL_NOTES.md)**. Apache 2.0.

## Credits & license

Made with ❤️ in California

Apache License 2.0 — free to use, share, and modify

© 2026 Dan Park, magicpoint.ai

Questions, stories, things you caught? [Say hello in Discussions](../../discussions).
