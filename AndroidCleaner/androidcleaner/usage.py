"""Find apps that are never or rarely used, from `dumpsys usagestats` and `dumpsys diskstats`."""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from datetime import datetime

from .adb import Adb
from .packages import Package

EPOCH_PREFIX = "1970-01-01"
_PKG_STATS = re.compile(r"^\s*package=([\w.]+)\s+(.*)$")
_FIELD = re.compile(r'(\w+)="([^"]*)"|(\w+)=(\S+)')

# Never offered for removal even if unused: removing them breaks the phone.
PROTECTED = {
    "com.android.settings", "com.android.vending", "com.google.android.gms",
    "com.android.phone", "com.android.contacts", "com.google.android.dialer",
    "com.android.mms", "com.google.android.apps.messaging", "com.android.camera",
    "com.miui.securitycenter", "com.miui.home", "com.android.systemui",
    "com.android.documentsui", "com.google.android.documentsui", "com.android.stk",
    "com.android.incallui", "com.miui.cleaner", "com.xiaomi.account",
}


@dataclass
class AppUsage:
    pkg: str
    last_used: str = ""          # "" = no foreground use recorded in the stats window
    total_seconds: int = 0
    launches: int = 0
    size_bytes: int = 0
    days_idle: int | None = None  # None = never used in the window
    first_install: str = ""
    kind: str = ""               # user | preload | system


def _duration_seconds(text: str) -> int:
    """usagestats durations look like 'MM.SS', 'HH.MM.SS' or 'D.HH.MM.SS' style dot groups."""
    parts = [int(p) for p in re.findall(r"\d+", text)]
    total = 0
    for p in parts:
        total = total * 60 + p
    return total


def parse_usagestats(output: str) -> tuple[dict[str, dict], str]:
    """Merge every interval (daily/weekly/monthly/yearly) per package, keeping the latest use and
    the largest totals. Returns (stats, yearly time range text)."""
    stats: dict[str, dict] = {}
    window = ""
    in_yearly = False
    for line in output.splitlines():
        if "In-memory yearly stats" in line:
            in_yearly = True
            continue
        if in_yearly and not window:
            m = re.search(r'timeRange="([^"]+)"', line)
            if m:
                window = m.group(1)
        m = _PKG_STATS.match(line)
        if not m:
            continue
        fields = {}
        for f in _FIELD.finditer(m.group(2)):
            key = f.group(1) or f.group(3)
            fields[key] = f.group(2) if f.group(1) else f.group(4)
        s = stats.setdefault(m.group(1), {"last_used": "", "total": 0, "launches": 0})
        last = fields.get("lastTimeUsed", "")
        if last and not last.startswith(EPOCH_PREFIX) and last > s["last_used"]:
            s["last_used"] = last
        s["total"] = max(s["total"], _duration_seconds(fields.get("totalTimeUsed", "0")))
        try:
            s["launches"] = max(s["launches"], int(fields.get("appLaunchCount", "0")))
        except ValueError:
            pass
    return stats, window


def parse_diskstats(output: str) -> dict[str, int]:
    """Package -> app + data + cache bytes (snapshot taken periodically by the system)."""
    arrays = {}
    for key in ("Package Names", "App Sizes", "App Data Sizes", "Cache Sizes"):
        m = re.search(rf"^{key}: (\[.*\])\s*$", output, re.M)
        if m:
            try:
                arrays[key] = json.loads(m.group(1))
            except json.JSONDecodeError:
                pass
    names = arrays.get("Package Names", [])
    sizes = {}
    for i, name in enumerate(names):
        total = 0
        for key in ("App Sizes", "App Data Sizes"):  # data size already includes cache
            vals = arrays.get(key, [])
            if i < len(vals):
                total += int(vals[i])
        sizes[name] = sizes.get(name, 0) + total
    return sizes


def _days_since(stamp: str, now: datetime) -> int | None:
    try:
        return (now - datetime.strptime(stamp[:19], "%Y-%m-%d %H:%M:%S")).days
    except ValueError:
        return None


def find_unused(adb: Adb, pkgs: dict[str, Package], days: int = 30,
                include_system: bool = False, now: datetime | None = None
                ) -> tuple[list[AppUsage], str]:
    stats, window = parse_usagestats(adb.shell("dumpsys usagestats", timeout=600))
    sizes = parse_diskstats(adb.shell("dumpsys diskstats", timeout=600))
    now = now or datetime.now()
    home = adb.shell("cmd package resolve-activity --brief -a android.intent.action.MAIN "
                     "-c android.intent.category.HOME").strip().splitlines()
    protected = PROTECTED | {l.split("/")[0] for l in home if "/" in l}
    return select_unused(pkgs, stats, sizes, days, include_system, now, protected), window


def select_unused(pkgs: dict[str, Package], stats: dict[str, dict], sizes: dict[str, int],
                  days: int, include_system: bool, now: datetime,
                  protected: set[str] = PROTECTED) -> list[AppUsage]:
    result = []
    for p in pkgs.values():
        # Only apps the user can actually open; background components are not "apps".
        if not (p.installed and p.enabled and p.has_launcher) or p.name in protected:
            continue
        preload = not p.system and p.path and not p.path.startswith("/data/")
        kind = "system" if p.system else ("preload" if preload else "user")
        if kind == "system" and not include_system:
            continue
        # Give freshly installed apps a chance before calling them unused.
        if p.first_install:
            age = _days_since(p.first_install, now)
            if age is not None and age < days:
                continue
        s = stats.get(p.name, {})
        last = s.get("last_used", "")
        idle = _days_since(last, now) if last else None
        if idle is not None and idle < days:
            continue
        result.append(AppUsage(p.name, last, s.get("total", 0), s.get("launches", 0),
                               sizes.get(p.name, 0), idle, p.first_install, kind))
    # Never used first, then longest idle, then biggest.
    result.sort(key=lambda u: (u.days_idle is not None, -(u.days_idle or 0), -u.size_bytes))
    return result


def parse_selection(text: str, count: int) -> list[int]:
    """'1,3,5-7' or 'all' -> zero-based indexes. Raises ValueError on bad input."""
    text = text.strip().lower()
    if text in ("all", "semua"):
        return list(range(count))
    picked = []
    for part in re.split(r"[,\s]+", text):
        if not part:
            continue
        if "-" in part:
            a, b = (int(x) for x in part.split("-", 1))
            rng = range(a, b + 1)
        else:
            rng = [int(part)]
        for n in rng:
            if not 1 <= n <= count:
                raise ValueError(f"{n} is out of range 1-{count}")
            if n - 1 not in picked:
                picked.append(n - 1)
    return picked
