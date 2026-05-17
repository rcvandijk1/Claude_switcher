#!/usr/bin/env python3
"""Entry point for Claude Account Switcher."""

import sys


def _check_deps():
    missing = []
    try:
        import tkinter  # noqa: F401
    except ImportError:
        missing.append("tkinter  (install python3-tk on Linux)")
    if missing:
        print("Missing dependencies:", *missing, sep="\n  ")
        sys.exit(1)


if __name__ == "__main__":
    _check_deps()
    from app import ClaudeSwitcherApp

    app = ClaudeSwitcherApp()
    app.mainloop()
