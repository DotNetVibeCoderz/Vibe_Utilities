"""Records every change made to a device so it can be undone with `restore`.

One JSON object per line in journal/<serial>.jsonl. Each entry carries an `undo` list of steps:
  {"shell": "<command>"}   run with `adb shell`
  {"install": [apk, ...]}  reinstall APK(s) saved in quarantine/
"""
from __future__ import annotations

import json
import time
from pathlib import Path

from .adb import PROJECT_ROOT

JOURNAL_DIR = PROJECT_ROOT / "journal"


class Journal:
    def __init__(self, serial: str):
        self.path = JOURNAL_DIR / f"{serial}.jsonl"

    def record(self, action: str, target: str, undo: list[dict], detail: str = "") -> None:
        JOURNAL_DIR.mkdir(exist_ok=True)
        entry = {
            "ts": time.strftime("%Y-%m-%d %H:%M:%S"),
            "action": action,
            "target": target,
            "detail": detail,
            "undo": undo,
            "restored": False,
        }
        with self.path.open("a", encoding="utf-8") as f:
            f.write(json.dumps(entry) + "\n")

    def entries(self) -> list[dict]:
        if not self.path.exists():
            return []
        with self.path.open(encoding="utf-8") as f:
            return [json.loads(line) for line in f if line.strip()]

    def mark_restored(self, indexes: set[int]) -> None:
        entries = self.entries()
        for i in indexes:
            entries[i]["restored"] = True
        with self.path.open("w", encoding="utf-8") as f:
            for e in entries:
                f.write(json.dumps(e) + "\n")
