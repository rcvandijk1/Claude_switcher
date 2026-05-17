"""Manages Claude account profiles and credential switching."""

import json
import os
import uuid
from pathlib import Path
from datetime import datetime

CLAUDE_DIR = Path.home() / ".claude"
CREDENTIALS_FILE = CLAUDE_DIR / ".credentials.json"
PROFILES_DIR = Path.home() / ".claude_switcher"
PROFILES_FILE = PROFILES_DIR / "profiles.json"


class AccountManager:
    def __init__(self):
        PROFILES_DIR.mkdir(exist_ok=True)
        self.profiles = self._load_profiles()

    def _load_profiles(self) -> dict:
        if PROFILES_FILE.exists():
            try:
                with open(PROFILES_FILE) as f:
                    return json.load(f)
            except (json.JSONDecodeError, OSError):
                pass
        return {"accounts": [], "active_id": None}

    def _save_profiles(self):
        with open(PROFILES_FILE, "w") as f:
            json.dump(self.profiles, f, indent=2)

    # ------------------------------------------------------------------ #
    # Credential helpers
    # ------------------------------------------------------------------ #

    def read_live_credentials(self) -> dict | None:
        """Return the credentials currently on disk (the live account)."""
        if CREDENTIALS_FILE.exists():
            try:
                with open(CREDENTIALS_FILE) as f:
                    return json.load(f)
            except (json.JSONDecodeError, OSError):
                return None
        return None

    def write_credentials(self, credentials: dict):
        CLAUDE_DIR.mkdir(exist_ok=True)
        with open(CREDENTIALS_FILE, "w") as f:
            json.dump(credentials, f, indent=2)

    # ------------------------------------------------------------------ #
    # Account CRUD
    # ------------------------------------------------------------------ #

    def capture_current(self, name: str) -> dict:
        """Save whatever is in ~/.claude/.credentials.json as a new profile."""
        creds = self.read_live_credentials()
        account = {
            "id": str(uuid.uuid4()),
            "name": name,
            "credentials": creds,
            "added": datetime.now().isoformat(),
        }
        self.profiles["accounts"].append(account)
        self._save_profiles()
        return account

    def add_api_key_account(self, name: str, api_key: str) -> dict:
        """Create a profile backed by a bare ANTHROPIC_API_KEY."""
        account = {
            "id": str(uuid.uuid4()),
            "name": name,
            "credentials": {"claudeApiKey": api_key},
            "added": datetime.now().isoformat(),
        }
        self.profiles["accounts"].append(account)
        self._save_profiles()
        return account

    def rename(self, account_id: str, new_name: str):
        for acc in self.profiles["accounts"]:
            if acc["id"] == account_id:
                acc["name"] = new_name
                break
        self._save_profiles()

    def delete(self, account_id: str):
        self.profiles["accounts"] = [
            a for a in self.profiles["accounts"] if a["id"] != account_id
        ]
        if self.profiles.get("active_id") == account_id:
            self.profiles["active_id"] = None
        self._save_profiles()

    # ------------------------------------------------------------------ #
    # Switching
    # ------------------------------------------------------------------ #

    def switch_to(self, account_id: str):
        """
        1. Persist current live credentials back to the currently active profile.
        2. Write the selected profile's credentials to disk.
        3. Mark the new profile as active.
        """
        target = self._get(account_id)
        if target is None:
            raise ValueError(f"Unknown account id: {account_id}")

        # Persist live credentials into the currently-active profile
        current_id = self.profiles.get("active_id")
        if current_id and current_id != account_id:
            live = self.read_live_credentials()
            for acc in self.profiles["accounts"]:
                if acc["id"] == current_id:
                    acc["credentials"] = live
                    break

        # Write target credentials to disk
        if target.get("credentials"):
            self.write_credentials(target["credentials"])

        self.profiles["active_id"] = account_id
        self._save_profiles()

    # ------------------------------------------------------------------ #
    # Queries
    # ------------------------------------------------------------------ #

    def all_accounts(self) -> list[dict]:
        return self.profiles["accounts"]

    def active_account(self) -> dict | None:
        aid = self.profiles.get("active_id")
        return self._get(aid) if aid else None

    def _get(self, account_id: str) -> dict | None:
        for acc in self.profiles["accounts"]:
            if acc["id"] == account_id:
                return acc
        return None
