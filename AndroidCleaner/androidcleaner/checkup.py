"""`checkup`: one read-only pass over everything, with a summary and recommended commands.

Prints a report in Indonesian (the user-facing language of this project) and saves the same
content as Markdown in reports/checkup-<serial>-<time>.md. Nothing on the device is changed.
"""
from __future__ import annotations

import json
import re
import time
from dataclasses import dataclass, field
from datetime import datetime

from . import adblock, cleaner, debloat, usage
from .adb import PROJECT_ROOT, Adb
from .packages import inventory
from .scanner import scan

REPORT_DIR = PROJECT_ROOT / "reports"
OK, WARN, BAD = "OK", "PERHATIAN", "BAHAYA"


@dataclass
class Section:
    title: str
    status: str = OK
    lines: list[str] = field(default_factory=list)
    actions: list[tuple[str, str]] = field(default_factory=list)  # (command, why)


def _internal_cache_kb(adb: Adb) -> int:
    out = adb.shell("dumpsys diskstats", timeout=300)
    m = re.search(r"^Cache Sizes: (\[.*\])\s*$", out, re.M)
    try:
        return sum(json.loads(m.group(1))) // 1024 if m else 0
    except (json.JSONDecodeError, TypeError):
        return 0


def _patch_age_days(patch: str) -> int | None:
    try:
        return (datetime.now() - datetime.strptime(patch, "%Y-%m-%d")).days
    except ValueError:
        return None


def run(adb: Adb, days: int = 30, log=print) -> tuple[list[Section], dict]:
    h = cleaner.human
    sections: list[Section] = []
    log("  [1/6] membaca info perangkat...")
    props = {k: adb.getprop(p) for k, p in [
        ("brand", "ro.product.manufacturer"), ("model", "ro.product.model"),
        ("android", "ro.build.version.release"), ("patch", "ro.build.version.security_patch")]}
    st = cleaner.storage(adb)
    mem = adb.shell("cat /proc/meminfo")
    memv = {l.split(":")[0]: int(l.split()[1]) for l in mem.splitlines() if len(l.split()) >= 2}
    pkgs = inventory(adb)
    user_apps = sum(1 for p in pkgs.values() if not p.system and p.installed)

    s = Section("Perangkat")
    s.lines += [f"{props['brand']} {props['model']}, Android {props['android']}",
                f"Patch keamanan: {props['patch']}"]
    age = _patch_age_days(props["patch"])
    if age is not None and age > 180:
        s.status = WARN
        s.lines.append(f"Patch keamanan sudah {age} hari. Cek Settings > About phone > System update.")
    if st:
        s.lines.append(f"Penyimpanan: {h(st.avail_kb)} kosong dari {h(st.size_kb)} ({st.pct:.0f}% terpakai)")
        if st.pct >= 85:
            s.status = WARN
    s.lines.append(f"RAM: {h(memv.get('MemAvailable', 0))} bebas dari {h(memv.get('MemTotal', 0))}")
    s.lines.append(f"Aplikasi: {user_apps} aplikasi user, {len(pkgs) - user_apps} sistem/komponen")
    sections.append(s)

    log("  [2/6] scan malware/adware...")
    findings, access, protect = scan(adb, pkgs=pkgs, progress=lambda *_: None)
    high = [f for f in findings if f.verdict == "HIGH"]
    med = [f for f in findings if f.verdict == "MEDIUM"]
    s = Section("Malware / adware")
    if high:
        s.status = BAD
    elif med:
        s.status = WARN
    s.lines.append(f"{len(high)} risiko tinggi, {len(med)} risiko sedang")
    for f in high + med:
        s.lines.append(f"[{f.verdict}] {f.pkg}: {'; '.join(f.reasons[:3])}")
    if high:
        s.actions.append((f"ac quarantine {' '.join(f.pkg for f in high if not f.system)}",
                          "hapus aplikasi berisiko tinggi (APK di-backup dulu)"))
    if med:
        s.actions.append(("ac scan -v", "lihat detail aplikasi berisiko sedang, putuskan manual"))
    verifier = protect["play_protect_verifier"]
    if verifier not in ("1", "null", ""):
        s.status = BAD if s.status != BAD else s.status
        s.lines.append("Play Protect MATI. Nyalakan di Play Store > Profil > Play Protect.")
    for key, label in [("device_admin", "Device admin"), ("accessibility", "Accessibility")]:
        if access[key]:
            s.lines.append(f"{label}: {', '.join(sorted(access[key]))}")
    sections.append(s)

    log("  [3/6] cek pemblokir iklan...")
    mode, spec = adblock.dns_status(adb)
    providers = {host for host, _ in adblock.DNS_PROVIDERS.values()}
    s = Section("Pemblokir iklan")
    if mode == "hostname":
        valid = adblock.dns_validated(adb)
        s.lines.append(f"Private DNS: {spec}" + (" (tersambung)" if valid else " (BELUM tersambung)"))
        probe = adblock.ad_block_probe(adb)
        s.lines.append("Uji domain iklan (doubleclick.net): " + {
            "blocked": "terblokir", "not blocked": "TIDAK terblokir", "unknown": "tidak bisa diuji"}[probe])
        if probe == "not blocked" and spec in providers:
            s.status = WARN
        if not valid:
            s.status = WARN
            s.lines.append("HP belum bisa memakai server DNS ini. Internet bisa gagal; cek jaringan.")
        if spec not in providers:
            s.lines.append("Server ini bukan dari daftar pemblokir iklan bawaan tools.")
    else:
        s.status = WARN
        s.lines.append("Private DNS pemblokir iklan belum aktif.")
        s.actions.append(("ac ads dns adguard", "blokir iklan di semua aplikasi & browser"))
    third = {p.name for p in pkgs.values() if not p.system and p.installed and p.enabled}
    overlay = adblock.overlay_apps(adb, third)
    if overlay:
        s.lines.append(f"Aplikasi user yang boleh tampil di atas aplikasi lain: {', '.join(overlay)}")
    sections.append(s)

    log("  [4/6] cek bloatware...")
    brand = debloat.detect_brand(adb)
    rec = [c for c in debloat.candidates(pkgs, brand, "recommended") if c.state == "enabled"]
    adv = [c for c in debloat.candidates(pkgs, brand, "advanced")
           if c.state == "enabled" and c.level == "advanced"]
    s = Section(f"Bloatware ({brand or 'generic'})")
    if rec:
        s.status = WARN
        s.lines.append(f"{len(rec)} bloatware/iklan/telemetry yang direkomendasikan untuk dihapus: "
                       + ", ".join(c.pkg for c in rec))
        s.actions.append(("ac debloat --apply", "hapus bloatware level recommended (bisa di-restore)"))
    else:
        s.lines.append("Semua bloatware level recommended sudah dihapus.")
    if adv:
        s.lines.append(f"{len(adv)} aplikasi opsional (level advanced). Lihat dengan: "
                       "ac debloat --level advanced")
    sections.append(s)

    log("  [5/6] cari aplikasi yang jarang dipakai...")
    unused, window, coverage = usage.find_unused(adb, pkgs, days)
    s = Section(f"Aplikasi tidak dipakai > {days} hari")
    if window:
        s.lines.append(f"Riwayat pemakaian yang tersedia: {window}")
    if coverage is not None and coverage < days:
        # Too little history: "never used" would include apps used every day before the reset.
        s.lines.append(f"Riwayat pemakaian di HP ini hanya {coverage:.1f} hari (kurang dari {days} hari), "
                       "jadi belum bisa dinilai aplikasi mana yang tidak dipakai. Cek lagi nanti "
                       "setelah riwayatnya terkumpul.")
        unused = []
        s.lines.append(f"(Kalau tetap ingin melihat daftar: ac unused --days {max(int(coverage), 1)} "
                       "atau ac unused --force)")
    elif unused:
        total = sum(u.size_bytes for u in unused) / 1024
        s.status = WARN
        s.lines.append(f"{len(unused)} aplikasi, sekitar {h(total)}")
        for u in unused[:10]:
            when = u.last_used[:10] if u.last_used else "tidak pernah"
            size = h(u.size_bytes / 1024) if u.size_bytes else "?"
            s.lines.append(f"{u.pkg} ({size}, terakhir: {when})")
        if len(unused) > 10:
            s.lines.append(f"... dan {len(unused) - 10} lainnya")
        s.actions.append(("ac unused", "pilih aplikasi yang mau dihapus dari daftar bernomor"))
    elif coverage is None or coverage >= days:
        s.lines.append("Tidak ada. Semua aplikasi dipakai.")
    sections.append(s)

    log("  [6/6] hitung file sampah & cache...")
    ext = sum(kb for _, kb in cleaner.external_cache_dirs(adb))
    junk_dirs = sum(cleaner.du_kb(adb, d) for d in cleaner.JUNK_DIRS)
    junk_files = sum(kb for _, kb in cleaner.find_junk_files(adb))
    apks = cleaner.find_apk_installers(adb)
    internal = _internal_cache_kb(adb)
    junk_total = ext + junk_dirs + junk_files
    s = Section("Sampah & cache")
    # diskstats is a snapshot the system refreshes about once a day, so right after a clean it
    # still shows the old cache size. Ignore it if we cleaned within the last 24 hours.
    cleaned = cleaner.last_cleaned(adb.serial)
    if cleaned and time.time() - cleaned < 24 * 3600:
        s.lines.append("Cache aplikasi (internal): sudah dibersihkan "
                       + time.strftime("%Y-%m-%d %H:%M", time.localtime(cleaned)))
        internal = 0
    else:
        s.lines.append(f"Cache aplikasi (internal, snapshot harian sistem): {h(internal)}")
    s.lines.append(f"Cache aplikasi di penyimpanan bersama: {h(ext)}")
    s.lines.append(f"Thumbnail, log, file sementara: {h(junk_dirs + junk_files)}")
    if apks:
        s.lines.append(f"{len(apks)} file installer APK ({h(sum(kb for _, kb in apks))}). Tinjau dulu, "
                       "bisa jadi sengaja disimpan; hapus dengan: ac clean --apks")
    if internal + junk_total > 200 * 1024:
        s.status = WARN
        s.actions.append(("ac clean", "bersihkan cache & sampah"))
    sections.append(s)

    data = {"serial": adb.serial, "time": time.strftime("%Y-%m-%d %H:%M:%S"), **props,
            "high": [f.pkg for f in high], "medium": [f.pkg for f in med],
            "dns": {"mode": mode, "host": spec}, "debloat_pending": [c.pkg for c in rec],
            "unused": [u.pkg for u in unused], "junk_kb": junk_total, "internal_cache_kb": internal}
    return sections, data


def render(sections: list[Section], data: dict, markdown: bool = False) -> str:
    out = []
    icon = {OK: "OK  ", WARN: "!!  ", BAD: "XX  "}
    if markdown:
        out += [f"# Laporan cek HP: {data['brand']} {data['model']}", "",
                f"Serial `{data['serial']}`, dibuat {data['time']}.", "",
                "| Bagian | Status |", "|---|---|"]
        out += [f"| {s.title} | {s.status} |" for s in sections]
        out.append("")
    for s in sections:
        if markdown:
            out += [f"## {s.title}: {s.status}", ""] + [f"- {l}" for l in s.lines] + [""]
        else:
            out.append(f"\n== {icon[s.status]}{s.title} [{s.status}] " + "=" * max(0, 44 - len(s.title)))
            out += [f"  {l}" for l in s.lines]
    actions = [a for s in sections for a in s.actions]
    if markdown:
        out += ["## Rekomendasi", ""]
        out += [f"- `{cmd}`: {why}" for cmd, why in actions] or ["- Tidak ada. HP dalam kondisi baik."]
    else:
        out.append("\n== REKOMENDASI " + "=" * 47)
        if not actions:
            out.append("  Tidak ada. HP dalam kondisi baik.")
        for i, (cmd, why) in enumerate(actions, 1):
            out.append(f"  {i}. {cmd:38} # {why}")
        if actions:
            out.append("\n  Semua perubahan bisa dibatalkan dengan: ac restore")
    return "\n".join(out)


def save(sections: list[Section], data: dict) -> str:
    REPORT_DIR.mkdir(exist_ok=True)
    path = REPORT_DIR / f"checkup-{data['serial']}-{time.strftime('%Y%m%d-%H%M%S')}.md"
    path.write_text(render(sections, data, markdown=True) + "\n", encoding="utf-8")
    return str(path)
