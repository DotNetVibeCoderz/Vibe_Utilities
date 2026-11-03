"""Brand-aware bloatware lists (androidcleaner/data/debloat.json)."""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .adb import Adb
from .packages import Package

DATA = Path(__file__).parent / "data" / "debloat.json"
LEVELS = ("recommended", "advanced")


@dataclass
class Candidate:
    pkg: str
    level: str
    desc: str
    source: str
    state: str  # enabled | disabled | removed


def load() -> dict:
    with DATA.open(encoding="utf-8") as f:
        return json.load(f)


def detect_brand(adb: Adb, data: dict | None = None) -> str | None:
    data = data or load()
    ident = " ".join(adb.getprop(k).lower() for k in
                     ("ro.product.manufacturer", "ro.product.brand"))
    for brand, keys in data["brands"].items():
        if any(k in ident for k in keys):
            return brand
    return None


def candidates(pkgs: dict[str, Package], brand: str | None, level: str,
               data: dict | None = None) -> list[Candidate]:
    data = data or load()
    allowed = LEVELS[: LEVELS.index(level) + 1]
    sources = ["generic"] + ([brand] if brand and brand in data["lists"] else [])
    out = []
    for src in sources:
        for item in data["lists"][src]:
            p = pkgs.get(item["pkg"])
            if not p or item["level"] not in allowed:
                continue
            state = "removed" if not p.installed else ("enabled" if p.enabled else "disabled")
            out.append(Candidate(item["pkg"], item["level"], item["desc"], src, state))
    return out
