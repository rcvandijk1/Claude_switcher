# Claude Account Switcher

A small desktop app to switch between multiple Claude accounts across VS Code, Cursor, and other Claude Code integrations.

## How it works

Claude Code stores its OAuth credentials in `~/.claude/.credentials.json`.  
This app manages named snapshots of that file, letting you swap accounts in one click.

## Setup

```bash
# Python 3.10+ with tkinter (built-in on Windows/macOS; on Ubuntu: sudo apt install python3-tk)
python main.py
```

Optional modern styling:
```bash
pip install customtkinter
python main.py
```

## Usage

### First run — save your accounts

1. **Log in** to Account A inside VS Code (Claude Code extension).
2. Open this app → click **+ Add Account** → choose *"Capture current credentials"* → name it (e.g. `Work`).
3. Log out in VS Code, log in as Account B.
4. Click **+ Add Account** again → name it (e.g. `Personal`).

### Switching

- Click **Switch Here** on any account card.
- The previously-active account's credentials are saved automatically.
- **Reload your VS Code / Cursor windows** after switching (Command Palette → `Developer: Reload Window`).

### API-key accounts

If you have an `ANTHROPIC_API_KEY` instead of an OAuth login, choose *"Paste ANTHROPIC_API_KEY"* when adding an account.

### Browser tabs

Browser sessions on `claude.ai` are independent of Claude Code credentials.  
To switch there you still need to sign out and sign in manually.

## Data storage

Account profiles are stored in `~/.claude_switcher/profiles.json`.  
Credentials are stored in plain JSON (same format Claude Code already uses).

## Limitations

- Switching affects all Claude Code instances that share `~/.claude/` (VS Code, Cursor, etc.) — they all read the same file.
- Active browser sessions are unaffected.
