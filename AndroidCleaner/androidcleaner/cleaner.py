"""Storage cleanup: app caches, junk files on shared storage, and storage reporting."""
from __future__ import annotations

import re
from dataclasses import dataclass

import time

from .adb import PROJECT_ROOT, Adb, q

SDCARD = "/sdcard"

# Directories whose contents are always disposable (regenerated on demand).
JUNK_DIRS = [
    "/sdcard/DCIM/.thumbnails",
    "/sdcard/Pictures/.thumbnails",
    "/sdcard/Movies/.thumbnails",
    "/sdcard/Music/.thumbnails",
    "/sdcard/LOST.DIR",
    "/sdcard/.Trash",
    "/sdcard/MIUI/debug_log",
    "/sdcard/MIUI/.cache",
    "/sdcard/log",
    "/sdcard/logs",
    "/sdcard/tencent/msflogs",
    "/sdcard/Android/media/com.whatsapp/WhatsApp/.Shared",
    "/sdcard/WhatsApp/.Shared",          # pre-Android 11 WhatsApp location
    "/data/local/tmp",
]
# Files on shared storage that are leftovers (searched with find, depth-limited).
JUNK_FILE_PATTERNS = ["*.tmp", "*.temp", "*.log", "thumbdata*", ".thumbdata*"]
# Folders where *.log / *.tmp names can be real files someone received or saved.
KEEP_PATH_PARTS = ("/Download/", "/Documents/", "/WhatsApp Documents/", "/WhatsApp/Media/")


def keep_file(path: str) -> bool:
    """True for files the junk-file sweep must not touch: user document folders, and anything
    inside a JUNK_DIRS folder (already emptied and counted there)."""
    if any(part in path for part in KEEP_PATH_PARTS):
        return True
    return any(path.startswith(d.rstrip("/") + "/") for d in JUNK_DIRS)


@dataclass
class Storage:
    mount: str
    size_kb: int
    used_kb: int
    avail_kb: int

    @property
    def pct(self) -> float:
        return 100.0 * self.used_kb / self.size_kb if self.size_kb else 0.0


def parse_df(output: str) -> list[Storage]:
    rows = []
    for line in output.splitlines()[1:]:
        parts = line.split()
        if len(parts) < 6:
            continue
        try:
            rows.append(Storage(parts[5], int(parts[1]), int(parts[2]), int(parts[3])))
        except ValueError:
            continue
    return rows


def storage(adb: Adb) -> Storage | None:
    rows = parse_df(adb.shell("df -k /data"))
    return rows[0] if rows else None


def human(kb: float) -> str:
    for unit in ("KB", "MB", "GB", "TB"):
        if abs(kb) < 1024 or unit == "TB":
            return f"{kb:.1f} {unit}" if unit != "KB" else f"{kb:.0f} {unit}"
        kb /= 1024
    return f"{kb:.1f} TB"


def du_kb(adb: Adb, path: str) -> int:
    out = adb.shell(f"du -sk {q(path)} 2>/dev/null").strip()
    m = re.match(r"^(\d+)", out)
    return int(m.group(1)) if m else 0


def external_cache_dirs(adb: Adb) -> list[tuple[str, int]]:
    """Per-app cache folders on shared storage with their size in KB (largest first)."""
    out = adb.shell("du -sk /sdcard/Android/data/*/cache 2>/dev/null")
    rows = []
    for line in out.splitlines():
        m = re.match(r"^(\d+)\s+(\S.*)$", line.strip())
        if m and int(m.group(1)) > 4:
            rows.append((m.group(2), int(m.group(1))))
    return sorted(rows, key=lambda r: -r[1])


def find_junk_files(adb: Adb) -> list[tuple[str, int]]:
    names = " -o ".join(f"-name {q(p)}" for p in JUNK_FILE_PATTERNS)
    out = adb.shell(f"find {SDCARD}/ -maxdepth 4 -type f \\( {names} \\) "
                    f"-not -path '*/Android/obb/*' -exec du -k {{}} + 2>/dev/null", timeout=600)
    rows = []
    for line in out.splitlines():
        m = re.match(r"^(\d+)\s+(\S.*)$", line.strip())
        if m and not keep_file(m.group(2)):
            rows.append((m.group(2), int(m.group(1))))
    return rows


def find_apk_installers(adb: Adb) -> list[tuple[str, int]]:
    out = adb.shell(f"find {SDCARD}/ -maxdepth 3 -type f \\( -iname '*.apk' -o -iname '*.xapk' "
                    f"-o -iname '*.apks' \\) -not -path '*/Android/*' -exec du -k {{}} + 2>/dev/null",
                    timeout=600)
    rows = []
    for line in out.splitlines():
        m = re.match(r"^(\d+)\s+(\S.*)$", line.strip())
        if m:
            rows.append((m.group(2), int(m.group(1))))
    return rows


def large_files(adb: Adb, min_mb: int = 100) -> list[tuple[str, int]]:
    out = adb.shell(f"find {SDCARD}/ -type f -size +{min_mb}M -not -path '*/Android/obb/*' "
                    f"-exec du -k {{}} + 2>/dev/null", timeout=600)
    rows = []
    for line in out.splitlines():
        m = re.match(r"^(\d+)\s+(\S.*)$", line.strip())
        if m:
            rows.append((m.group(2), int(m.group(1))))
    return sorted(rows, key=lambda r: -r[1])


def empty_dirs(adb: Adb) -> list[str]:
    out = adb.shell(f"find {SDCARD}/ -mindepth 2 -type d -empty -not -path '*/Android/*' "
                    f"2>/dev/null", timeout=600)
    return [line.strip() for line in out.splitlines() if line.strip()]


class Cleaner:
    def __init__(self, adb: Adb, dry_run: bool = False, log=print):
        self.adb = adb
        self.dry_run = dry_run
        self.log = log

    def _rm(self, target: str, contents_only: bool = True) -> None:
        if contents_only:
            # Not `rm dir/*`: a shell glob over tens of thousands of files (Samsung Gallery
            # thumbnails) fails with "Argument list too long". find batches the arguments.
            cmd = f"find {q(target)} -mindepth 1 -maxdepth 1 -exec rm -rf {{}} + 2>/dev/null"
        else:
            cmd = f"rm -rf {q(target)} 2>/dev/null"
        if self.dry_run:
            self.log(f"  [dry-run] adb shell {cmd}")
        else:
            self.adb.shell(cmd)

    def trim_app_caches(self) -> None:
        """Ask the package manager to free cache space of every app (internal storage)."""
        cmd = "pm trim-caches 999999999999999"
        if self.dry_run:
            self.log(f"  [dry-run] adb shell {cmd}")
            return
        out = self.adb.shell(cmd, timeout=600).strip()
        if out:
            self.log(f"  pm trim-caches: {out}")

    def clean_external_caches(self) -> int:
        dirs = external_cache_dirs(self.adb)
        total = sum(kb for _, kb in dirs)
        for path, kb in dirs[:10]:
            self.log(f"    {human(kb):>10}  {path}")
        if len(dirs) > 10:
            self.log(f"    ... and {len(dirs) - 10} more")
        if dirs:
            # Same "Argument list too long" risk as _rm, so delete via find instead of a glob.
            cmd = ("find /sdcard/Android/data/ -mindepth 3 -maxdepth 3 -path '*/cache/*' "
                   "-exec rm -rf {} + 2>/dev/null")
            if self.dry_run:
                self.log(f"  [dry-run] adb shell {cmd}")
            else:
                self.adb.shell(cmd, timeout=600)
        return total

    def clean_junk_dirs(self) -> int:
        total = 0
        for d in JUNK_DIRS:
            kb = du_kb(self.adb, d)
            if kb > 4:
                self._rm(d)
                left = kb if self.dry_run else du_kb(self.adb, d)
                freed = kb - left if not self.dry_run else kb
                note = f"  (could not delete {human(left)})" if left > max(1024, kb // 10) else ""
                self.log(f"    {human(kb):>10}  {d}{note}")
                total += freed
        return total

    def clean_junk_files(self) -> int:
        files = find_junk_files(self.adb)
        total = sum(kb for _, kb in files)
        for path, kb in files:
            if self.dry_run:
                self.log(f"  [dry-run] rm {path}")
            else:
                self.adb.shell(f"rm -f {q(path)}")
        if files:
            self.log(f"    {len(files)} temp/log files, {human(total)}")
        return total

    def clean_apks(self, files: list[tuple[str, int]]) -> int:
        for path, _ in files:
            if self.dry_run:
                self.log(f"  [dry-run] rm {path}")
            else:
                self.adb.shell(f"rm -f {q(path)}")
        return sum(kb for _, kb in files)

    def clean_empty_dirs(self) -> int:
        dirs = empty_dirs(self.adb)
        for d in dirs:
            if self.dry_run:
                self.log(f"  [dry-run] rmdir {d}")
            else:
                self.adb.shell(f"rmdir {q(d)} 2>/dev/null")
        return len(dirs)

    def kill_background(self) -> None:
        cmd = "am kill-all"
        if self.dry_run:
            self.log(f"  [dry-run] adb shell {cmd}")
        else:
            self.adb.shell(cmd)


def _last_clean_file(serial: str):
    return PROJECT_ROOT / "journal" / f"{serial}.lastclean"


def mark_cleaned(serial: str) -> None:
    path = _last_clean_file(serial)
    path.parent.mkdir(exist_ok=True)
    path.write_text(str(int(time.time())), encoding="utf-8")


def last_cleaned(serial: str) -> float | None:
    """Epoch seconds of the last real `clean` run on this device, if any."""
    try:
        return float(_last_clean_file(serial).read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return None
