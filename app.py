"""Claude Account Switcher – main GUI window."""

import tkinter as tk
from tkinter import messagebox, simpledialog
import sys

try:
    import customtkinter as ctk  # type: ignore
    CTK = True
except ImportError:
    CTK = False

from account_manager import AccountManager

# ── colour palette ────────────────────────────────────────────────────────────
ACCENT   = "#D97706"   # amber – distinct from Claude's purple
BG_DARK  = "#1E1E2E"
BG_MID   = "#2A2A3E"
BG_CARD  = "#313149"
FG_MAIN  = "#E0E0F0"
FG_DIM   = "#888899"
GREEN    = "#22C55E"
RED      = "#EF4444"
FONT     = ("Segoe UI", 11) if sys.platform == "win32" else ("Helvetica", 11)
FONT_B   = (FONT[0], 11, "bold")
FONT_SM  = (FONT[0], 9)


# ── helpers ───────────────────────────────────────────────────────────────────

def _btn(parent, text, command, fg=FG_MAIN, bg=BG_MID, width=None, **kw):
    opts = dict(text=text, command=command, fg=fg, bg=bg,
                activeforeground=fg, activebackground=BG_DARK,
                relief="flat", cursor="hand2", font=FONT, padx=8, pady=4, **kw)
    if width:
        opts["width"] = width
    return tk.Button(parent, **opts)


def _label(parent, text, fg=FG_MAIN, font=None, **kw):
    return tk.Label(parent, text=text, fg=fg, bg=kw.pop("bg", BG_DARK),
                    font=font or FONT, **kw)


# ── dialog for adding a new account ──────────────────────────────────────────

class AddAccountDialog(tk.Toplevel):
    def __init__(self, parent, title="Add Account"):
        super().__init__(parent)
        self.title(title)
        self.configure(bg=BG_DARK)
        self.resizable(False, False)
        self.grab_set()
        self.result = None

        self._build()
        self.update_idletasks()
        x = parent.winfo_x() + (parent.winfo_width()  - self.winfo_width())  // 2
        y = parent.winfo_y() + (parent.winfo_height() - self.winfo_height()) // 2
        self.geometry(f"+{x}+{y}")
        self.wait_window()

    def _build(self):
        pad = dict(padx=16, pady=6)

        _label(self, "Account name:", fg=FG_DIM, font=FONT_SM).grid(
            row=0, column=0, sticky="w", **pad)
        self._name = tk.Entry(self, bg=BG_MID, fg=FG_MAIN, insertbackground=FG_MAIN,
                              font=FONT, relief="flat", width=28)
        self._name.grid(row=1, column=0, columnspan=2, sticky="ew", padx=16, pady=(0, 8))
        self._name.focus_set()

        _label(self, "Source:", fg=FG_DIM, font=FONT_SM).grid(
            row=2, column=0, sticky="w", **pad)

        self._mode = tk.StringVar(value="capture")
        for val, txt in [("capture", "Capture current ~/.claude credentials"),
                         ("apikey",  "Paste ANTHROPIC_API_KEY")]:
            tk.Radiobutton(self, text=txt, variable=self._mode, value=val,
                           bg=BG_DARK, fg=FG_MAIN, selectcolor=BG_MID,
                           activeforeground=FG_MAIN, activebackground=BG_DARK,
                           font=FONT, command=self._on_mode).grid(
                row=3 if val == "capture" else 4, column=0, sticky="w", padx=16)

        self._key_lbl = _label(self, "API key:", fg=FG_DIM, font=FONT_SM, bg=BG_DARK)
        self._key_lbl.grid(row=5, column=0, sticky="w", padx=16, pady=(8, 2))
        self._key_entry = tk.Entry(self, bg=BG_MID, fg=FG_MAIN, insertbackground=FG_MAIN,
                                   font=FONT, relief="flat", width=28, show="•")
        self._key_entry.grid(row=6, column=0, columnspan=2, sticky="ew", padx=16)
        self._on_mode()

        frm = tk.Frame(self, bg=BG_DARK)
        frm.grid(row=7, column=0, columnspan=2, pady=16)
        _btn(frm, "Add",    self._ok,     bg=ACCENT, width=8).pack(side="left", padx=6)
        _btn(frm, "Cancel", self.destroy, bg=BG_MID,  width=8).pack(side="left", padx=6)

    def _on_mode(self):
        show = self._mode.get() == "apikey"
        state = "normal" if show else "disabled"
        self._key_lbl.config(fg=FG_MAIN if show else FG_DIM)
        self._key_entry.config(state=state)

    def _ok(self):
        name = self._name.get().strip()
        if not name:
            messagebox.showwarning("Missing name", "Please enter an account name.", parent=self)
            return
        if self._mode.get() == "apikey":
            key = self._key_entry.get().strip()
            if not key:
                messagebox.showwarning("Missing key", "Please paste your API key.", parent=self)
                return
            self.result = ("apikey", name, key)
        else:
            self.result = ("capture", name, None)
        self.destroy()


# ── main window ───────────────────────────────────────────────────────────────

class ClaudeSwitcherApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Claude Account Switcher")
        self.configure(bg=BG_DARK)
        self.resizable(False, False)
        self.geometry("440x540")
        self.manager = AccountManager()
        self._build_ui()
        self._refresh()

    # ── UI construction ──────────────────────────────────────────────────

    def _build_ui(self):
        # ── header ──
        hdr = tk.Frame(self, bg=BG_MID, pady=12)
        hdr.pack(fill="x")
        _label(hdr, "CLAUDE ACCOUNT SWITCHER", fg=ACCENT, font=(FONT[0], 13, "bold"),
               bg=BG_MID).pack()
        self._active_lbl = _label(hdr, "Active: —", fg=FG_DIM, font=FONT_SM, bg=BG_MID)
        self._active_lbl.pack(pady=(2, 0))

        # ── account list (scrollable) ──
        list_outer = tk.Frame(self, bg=BG_DARK)
        list_outer.pack(fill="both", expand=True, padx=12, pady=(10, 4))

        canvas = tk.Canvas(list_outer, bg=BG_DARK, highlightthickness=0)
        scrollbar = tk.Scrollbar(list_outer, orient="vertical", command=canvas.yview)
        self._list_frame = tk.Frame(canvas, bg=BG_DARK)

        self._list_frame.bind(
            "<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
        )
        canvas.create_window((0, 0), window=self._list_frame, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)

        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        canvas.bind_all("<MouseWheel>", lambda e: canvas.yview_scroll(-1 * (e.delta // 120), "units"))

        # ── footer buttons ──
        footer = tk.Frame(self, bg=BG_DARK, pady=10)
        footer.pack(fill="x", padx=12)
        _btn(footer, "+ Add Account", self._on_add, bg=ACCENT).pack(side="left")

        # ── status bar ──
        self._status = _label(self, "", fg=FG_DIM, font=FONT_SM, bg=BG_MID)
        self._status.pack(fill="x", pady=0)

        # ── note about browser ──
        note = (
            "Tip: after switching, reload VS Code / Cursor windows.\n"
            "Browser tabs require a manual account switch on claude.ai."
        )
        _label(self, note, fg=FG_DIM, font=FONT_SM, bg=BG_DARK,
               justify="center").pack(pady=(0, 8))

    # ── refresh list ─────────────────────────────────────────────────────

    def _refresh(self):
        for w in self._list_frame.winfo_children():
            w.destroy()

        accounts = self.manager.all_accounts()
        active = self.manager.active_account()
        active_id = active["id"] if active else None

        self._active_lbl.config(
            text=f"Active: {active['name']}" if active else "Active: —",
            fg=GREEN if active else FG_DIM,
        )

        if not accounts:
            _label(self._list_frame,
                   "No accounts saved yet.\nClick '+ Add Account' to start.",
                   fg=FG_DIM, font=FONT).pack(pady=30)
            return

        for acc in accounts:
            self._make_card(acc, is_active=(acc["id"] == active_id))

    def _make_card(self, acc: dict, is_active: bool):
        border_color = GREEN if is_active else BG_MID
        card = tk.Frame(self._list_frame, bg=BG_CARD, pady=8, padx=10,
                        highlightbackground=border_color, highlightthickness=2)
        card.pack(fill="x", padx=4, pady=4)

        # name row
        name_row = tk.Frame(card, bg=BG_CARD)
        name_row.pack(fill="x")
        badge = " ✓ ACTIVE" if is_active else ""
        _label(name_row, acc["name"] + badge,
               fg=GREEN if is_active else FG_MAIN, font=FONT_B, bg=BG_CARD).pack(side="left")

        # added date
        added = acc.get("added", "")[:10]
        _label(card, f"Added {added}", fg=FG_DIM, font=FONT_SM, bg=BG_CARD).pack(anchor="w")

        # buttons
        btn_row = tk.Frame(card, bg=BG_CARD)
        btn_row.pack(anchor="w", pady=(6, 0))

        if not is_active:
            _btn(btn_row, "Switch Here",
                 lambda aid=acc["id"]: self._on_switch(aid),
                 bg=ACCENT, width=12).pack(side="left", padx=(0, 6))

        _btn(btn_row, "Rename",
             lambda aid=acc["id"]: self._on_rename(aid),
             bg=BG_MID, width=8).pack(side="left", padx=(0, 6))
        _btn(btn_row, "Delete",
             lambda aid=acc["id"]: self._on_delete(aid),
             fg=RED, bg=BG_MID, width=8).pack(side="left")

    # ── actions ──────────────────────────────────────────────────────────

    def _on_add(self):
        dlg = AddAccountDialog(self)
        if not dlg.result:
            return
        mode, name, extra = dlg.result
        try:
            if mode == "capture":
                acc = self.manager.capture_current(name)
                self._set_status(f'Captured current credentials as "{name}".')
            else:
                acc = self.manager.add_api_key_account(name, extra)
                self._set_status(f'Added API-key account "{name}".')
        except Exception as exc:
            messagebox.showerror("Error", str(exc), parent=self)
            return
        self._refresh()

    def _on_switch(self, account_id: str):
        try:
            self.manager.switch_to(account_id)
        except Exception as exc:
            messagebox.showerror("Switch failed", str(exc), parent=self)
            return
        acc = self.manager._get(account_id)
        self._set_status(f'Switched to "{acc["name"]}". Reload VS Code / Cursor windows.')
        self._refresh()

    def _on_rename(self, account_id: str):
        acc = self.manager._get(account_id)
        new_name = simpledialog.askstring(
            "Rename", f'New name for "{acc["name"]}":', parent=self
        )
        if new_name and new_name.strip():
            self.manager.rename(account_id, new_name.strip())
            self._refresh()

    def _on_delete(self, account_id: str):
        acc = self.manager._get(account_id)
        if messagebox.askyesno("Delete", f'Delete account "{acc["name"]}"?', parent=self):
            self.manager.delete(account_id)
            self._set_status(f'Deleted "{acc["name"]}".')
            self._refresh()

    def _set_status(self, msg: str):
        self._status.config(text=f"  {msg}")
        self.after(6000, lambda: self._status.config(text=""))
