"""Command-line interface: python -m androidcleaner <command>."""
from __future__ import annotations

import argparse
import json
import sys
import time

from . import adblock, cleaner, debloat, usage
from .adb import PROJECT_ROOT, Adb, AdbError, find_adb, list_devices, q
from .journal import Journal
from .packages import PackageActions, inventory, restore
from .scanner import scan

REPORT_DIR = PROJECT_ROOT / "reports"


# ---------------------------------------------------------------- helpers
def confirm(args, question: str) -> bool:
    if args.dry_run:
        return True
    if args.yes:
        return True
    try:
        return input(f"{question} [y/N] ").strip().lower() in ("y", "yes")
    except EOFError:
        return False


def header(title: str) -> None:
    print(f"\n== {title} " + "=" * max(0, 60 - len(title)))


def get_adb(args) -> Adb:
    return Adb(args.serial, verbose=args.verbose)


def report_actions(results: list[tuple[str, bool, str]]) -> None:
    for pkg, ok, out in results:
        status = "OK  " if ok else "FAIL"
        extra = f"  ({out})" if out else ""
        print(f"  {status} {pkg}{extra}")


# ---------------------------------------------------------------- commands
def cmd_devices(args) -> int:
    print(f"adb: {find_adb()}")
    devices = list_devices()
    if not devices:
        print("No devices. Enable Developer options > USB debugging and connect via USB.")
        return 1
    for d in devices:
        model = d.props.get("model", "")
        note = "  <- unlock phone and accept the USB debugging prompt" if d.state == "unauthorized" else ""
        print(f"  {d.serial:24} {d.state:13} {model}{note}")
    return 0


def cmd_info(args) -> int:
    adb = get_adb(args)
    header("Device")
    for label, prop in [("Manufacturer", "ro.product.manufacturer"), ("Model", "ro.product.model"),
                        ("Android", "ro.build.version.release"), ("SDK", "ro.build.version.sdk"),
                        ("Security patch", "ro.build.version.security_patch"),
                        ("Build", "ro.build.display.id")]:
        print(f"  {label:15} {adb.getprop(prop)}")
    brand = debloat.detect_brand(adb)
    print(f"  {'Debloat list':15} {brand or 'generic only'}")
    st = cleaner.storage(adb)
    if st:
        print(f"  {'Storage':15} {cleaner.human(st.used_kb)} used / {cleaner.human(st.size_kb)} "
              f"({st.pct:.0f}%), {cleaner.human(st.avail_kb)} free")
    mem = adb.shell("cat /proc/meminfo")
    total = next((l.split()[1] for l in mem.splitlines() if l.startswith("MemTotal")), "0")
    avail = next((l.split()[1] for l in mem.splitlines() if l.startswith("MemAvailable")), "0")
    print(f"  {'RAM':15} {cleaner.human(int(avail))} free of {cleaner.human(int(total))}")
    pkgs = inventory(adb, with_details=False)
    third = sum(1 for p in pkgs.values() if not p.system and p.installed)
    system = sum(1 for p in pkgs.values() if p.system and p.installed)
    disabled = sum(1 for p in pkgs.values() if not p.enabled)
    print(f"  {'Apps':15} {third} user, {system} system, {disabled} disabled")
    return 0


def cmd_scan(args) -> int:
    adb = get_adb(args)
    header("Malware / adware scan")
    findings, access, protect = scan(adb, include_system=args.all,
                                     virustotal=args.virustotal)
    flagged = [f for f in findings if f.score > 0]
    shown = flagged if not args.all else findings
    if not shown:
        print("  No suspicious apps found.")
    for f in shown:
        if f.verdict == "LOW" and not args.verbose and not args.all:
            continue
        kind = "system" if f.system else "user"
        print(f"\n  [{f.verdict:6}] {f.pkg}  (score {f.score}, {kind}, installer: {f.installer or '-'})")
        for r in f.reasons:
            print(f"           - {r}")
        if f.sha256:
            print(f"           sha256 {f.sha256}  {f.vt}")
    low = sum(1 for f in flagged if f.verdict == "LOW")
    if low and not args.verbose and not args.all:
        print(f"\n  ({low} more LOW-risk apps hidden; use -v to show)")

    header("Special access")
    labels = {"device_admin": "Device admins", "accessibility": "Accessibility services",
              "notification_listener": "Notification readers", "overlay": "Draw over apps",
              "install_unknown": "Can install apps", "input_method": "Keyboards",
              "home": "Launcher"}
    for key, label in labels.items():
        print(f"  {label:23} {', '.join(sorted(access[key])) or '-'}")

    header("Protection settings")
    verifier = protect["play_protect_verifier"]
    print(f"  Play Protect verifier   {'on' if verifier in ('1', 'null', '') else 'OFF'}")
    print(f"  Private DNS            {protect['private_dns_mode']} {protect['private_dns_specifier']}")
    if protect["always_on_vpn_app"] not in ("", "null"):
        print(f"  Always-on VPN app      {protect['always_on_vpn_app']}")

    REPORT_DIR.mkdir(exist_ok=True)
    report = REPORT_DIR / f"scan-{adb.serial}-{time.strftime('%Y%m%d-%H%M%S')}.json"
    report.write_text(json.dumps({
        "serial": adb.serial,
        "findings": [f.__dict__ for f in findings],
        "access": {k: sorted(v) for k, v in access.items()},
        "protection": protect,
    }, indent=2), encoding="utf-8")
    print(f"\n  Report: {report}")
    high = [f.pkg for f in findings if f.verdict == "HIGH" and not f.system]
    if high:
        print("\n  Next step: review the HIGH apps above, then remove them with")
        print(f"    python -m androidcleaner quarantine {' '.join(high)}")
    return 0


def cmd_quarantine(args) -> int:
    adb = get_adb(args)
    actions = PackageActions(adb, args.dry_run)
    header("Quarantine")
    acc = adb.shell("settings get secure enabled_accessibility_services").strip()
    notif = adb.shell("settings get secure enabled_notification_listeners").strip()
    admins = adb.shell("dumpsys device_policy")
    for pkg in args.packages:
        if not adb.shell(f"pm path {q(pkg)}").strip():
            print(f"  {pkg}: not installed")
            continue
        if not confirm(args, f"Back up APK, strip access, and uninstall {pkg}?"):
            continue
        print(f"  {pkg}")
        actions._exec(f"am force-stop {q(pkg)}")
        if pkg + "/" in acc:
            keep = ":".join(c for c in acc.split(":") if not c.startswith(pkg + "/"))
            actions.put_setting("secure", "enabled_accessibility_services", keep or None)
            print("    removed accessibility service")
        if pkg + "/" in notif:
            keep = ":".join(c for c in notif.split(":") if not c.startswith(pkg + "/"))
            actions.put_setting("secure", "enabled_notification_listeners", keep or None)
            print("    removed notification access")
        actions.set_appop(pkg, "SYSTEM_ALERT_WINDOW", "deny")
        if f"{{{pkg}/" in admins or f"{pkg}/" in admins:
            comps = [line for line in admins.split() if line.startswith(f"ComponentInfo{{{pkg}/")]
            for c in comps:
                comp = c[len("ComponentInfo{"):].rstrip("}:")
                out = adb.shell(f"dpm remove-active-admin --user 0 {q(comp)}")
                if "Success" not in out:
                    print("    !! is a device administrator and refused removal. On the phone go to")
                    print("       Settings > Security > Device admin apps, deactivate it, then rerun.")
                    if not args.dry_run:
                        adb.shell("am start -a android.settings.SECURITY_SETTINGS")
        ok, out = actions.remove_app(pkg, backup=not args.no_backup)
        if ok:
            print("    uninstalled (APK kept in quarantine/, `restore` reinstalls it)")
        else:
            print(f"    uninstall failed: {out}. Disabling instead.")
            ok, out = actions.disable(pkg)
            print(f"    {'disabled' if ok else 'disable failed: ' + out}")
    return 0


def _apply(args, pkgs: list[str], mode: str) -> int:
    adb = get_adb(args)
    actions = PackageActions(adb, args.dry_run)
    fn = {"disable": actions.disable, "uninstall": actions.uninstall_for_user,
          "enable": actions.enable}[mode]
    results = []
    for pkg in pkgs:
        ok, out = fn(pkg)
        results.append((pkg, ok, out))
    report_actions(results)
    return 0 if all(ok for _, ok, _ in results) else 1


def cmd_disable(args) -> int:
    if not confirm(args, f"Disable {len(args.packages)} package(s)?"):
        return 1
    return _apply(args, args.packages, "disable")


def cmd_enable(args) -> int:
    return _apply(args, args.packages, "enable")


def cmd_uninstall(args) -> int:
    if not confirm(args, f"Uninstall {len(args.packages)} package(s) for user 0?"):
        return 1
    return _apply(args, args.packages, "uninstall")


def cmd_apps(args) -> int:
    adb = get_adb(args)
    pkgs = inventory(adb, with_details=False)
    rows = [p for p in pkgs.values() if p.installed or args.all]
    if not args.system:
        rows = [p for p in rows if not p.system]
    for p in sorted(rows, key=lambda p: p.name):
        flags = ("S" if p.system else "U") + ("" if p.enabled else " disabled") + \
                ("" if p.has_launcher else " hidden") + ("" if p.installed else " removed")
        print(f"  {p.name:55} {flags:22} {p.installer}")
    print(f"\n  {len(rows)} packages")
    return 0


def cmd_debloat(args) -> int:
    adb = get_adb(args)
    data = debloat.load()
    brand = args.brand or debloat.detect_brand(adb, data)
    pkgs = inventory(adb, with_details=False)
    cands = debloat.candidates(pkgs, brand, args.level, data)
    header(f"Debloat ({brand or 'generic'}, level {args.level})")
    todo = [c for c in cands if c.state == "enabled"]
    for c in cands:
        mark = "*" if c.state == "enabled" else " "
        print(f"  {mark} {c.pkg:48} {c.level:11} {c.state:9} {c.desc}")
    if not todo:
        print("  Nothing left to remove at this level.")
        return 0
    print(f"\n  {len(todo)} package(s) marked * will be {args.mode}d.")
    if not args.apply:
        print("  Preview only. Add --apply to do it (use --mode uninstall to also remove for user 0).")
        return 0
    if not confirm(args, "Proceed?"):
        return 1
    return _apply(args, [c.pkg for c in todo], args.mode)


def cmd_unused(args) -> int:
    adb = get_adb(args)
    header(f"Apps not used in the last {args.days} days")
    pkgs = inventory(adb)
    apps, window = usage.find_unused(adb, pkgs, args.days, args.system)
    if window:
        print(f"  Usage history available on the phone: {window}")
        print("  ('never' = not opened anywhere in that period)\n")
    if not apps:
        print("  None. Every app was used recently.")
        return 0
    print(f"  {'#':>3}  {'last used':16} {'idle':>7} {'launches':>8} {'size':>10}  {'type':8} package")
    for i, u in enumerate(apps, 1):
        last = u.last_used[:16] if u.last_used else "never"
        idle = f"{u.days_idle}d" if u.days_idle is not None else "-"
        size = cleaner.human(u.size_bytes / 1024) if u.size_bytes else "?"
        print(f"  {i:>3}  {last:16} {idle:>7} {u.launches:>8} {size:>10}  {u.kind:8} {u.pkg}")
    total = sum(u.size_bytes for u in apps)
    print(f"\n  {len(apps)} app(s), about {cleaner.human(total / 1024)} in total (app + data).")

    REPORT_DIR.mkdir(exist_ok=True)
    report = REPORT_DIR / f"unused-{adb.serial}-{time.strftime('%Y%m%d-%H%M%S')}.json"
    report.write_text(json.dumps({"window": window, "days": args.days,
                                  "apps": [u.__dict__ for u in apps]}, indent=2), encoding="utf-8")
    print(f"  Report: {report}")

    selection = args.select
    if selection is None:
        if args.yes or not sys.stdin.isatty():
            print("\n  To remove some: unused --select 1,3,5-7   (or run it in a terminal to pick)")
            return 0
        try:
            selection = input("\n  Numbers to remove (e.g. 1,3,5-7 or all; Enter = cancel): ")
        except EOFError:
            return 0
    if not selection.strip():
        print("  Nothing removed.")
        return 0
    try:
        chosen = [apps[i] for i in usage.parse_selection(selection, len(apps))]
    except ValueError as e:
        print(f"  Invalid selection: {e}")
        return 1
    print("\n  Will remove:")
    for u in chosen:
        how = "uninstall (APK backed up)" if u.kind == "user" and not args.no_backup else \
              "uninstall" if u.kind == "user" else "remove for user 0 (restorable)"
        print(f"    {u.pkg:50} {how}")
    if not confirm(args, f"Remove {len(chosen)} app(s)?"):
        print("  Cancelled.")
        return 1
    actions = PackageActions(adb, args.dry_run)
    results = []
    for u in chosen:
        if u.kind == "user":
            ok, out = actions.remove_app(u.pkg, backup=not args.no_backup)
        else:
            ok, out = actions.uninstall_for_user(u.pkg)
        results.append((u.pkg, ok, out))
    report_actions(results)
    print("\n  Undo any of them with: restore <package>")
    return 0 if all(ok for _, ok, _ in results) else 1


def cmd_clean(args) -> int:
    adb = get_adb(args)
    cl = cleaner.Cleaner(adb, args.dry_run)
    before = cleaner.storage(adb)
    header("Storage before")
    if before:
        print(f"  {cleaner.human(before.avail_kb)} free of {cleaner.human(before.size_kb)} ({before.pct:.0f}% used)")

    # Shared-storage caches first: trim-caches also empties them, which would hide their size.
    header("App caches (shared storage)")
    ext = cl.clean_external_caches()
    header("App caches (internal, via pm trim-caches)")
    cl.trim_app_caches()
    header("Junk folders (thumbnails, LOST.DIR, logs, tmp)")
    junk = cl.clean_junk_dirs() + cl.clean_junk_files()

    apks = cleaner.find_apk_installers(adb)
    if apks:
        header("Leftover APK installers")
        for path, kb in apks:
            print(f"    {cleaner.human(kb):>10}  {path}")
        if args.apks and confirm(args, f"Delete {len(apks)} APK file(s)?"):
            junk += cl.clean_apks(apks)
        elif not args.apks:
            print("  Kept. Add --apks to delete them.")
    if args.empty_dirs:
        n = cl.clean_empty_dirs()
        print(f"\n  Removed {n} empty folder(s)")
    if args.kill_background:
        cl.kill_background()
        print("\n  Background processes stopped")

    if not args.dry_run:
        cleaner.mark_cleaned(adb.serial)
    after = cleaner.storage(adb)
    header("Result")
    print(f"  Shared-storage caches: {cleaner.human(ext)}, junk: {cleaner.human(junk)}")
    if before and after and not args.dry_run:
        freed = after.avail_kb - before.avail_kb
        print(f"  Free space: {cleaner.human(before.avail_kb)} -> {cleaner.human(after.avail_kb)} "
              f"({'+' if freed >= 0 else ''}{cleaner.human(freed)})")

    if args.large:
        header(f"Files larger than {args.large} MB (review manually)")
        for path, kb in cleaner.large_files(adb, args.large)[:30]:
            print(f"    {cleaner.human(kb):>10}  {path}")
    return 0


def cmd_ads(args) -> int:
    adb = get_adb(args)
    actions = PackageActions(adb, args.dry_run)
    if args.ads_cmd == "status" or args.ads_cmd is None:
        mode, spec = adblock.dns_status(adb)
        header("Ad blocking status")
        print(f"  Private DNS: {mode} {spec if mode == 'hostname' else ''}")
        blocking = mode == "hostname" and spec in {h for h, _ in adblock.DNS_PROVIDERS.values()}
        print(f"  System-wide DNS ad blocking: {'ON' if blocking else 'off'}")
        if mode == "hostname":
            valid = adblock.dns_validated(adb)
            print(f"  Resolver validated by phone: {', '.join(valid) if valid else 'NO (check network / hostname)'}")
        pkgs = inventory(adb, with_details=False)
        third = {p.name for p in pkgs.values() if not p.system and p.installed and p.enabled}
        ov = adblock.overlay_apps(adb, third)
        print(f"\n  User apps allowed to draw over other apps ({len(ov)}):")
        for p in ov:
            print(f"    {p}")
        print("\n  Providers for `ads dns <name>`:")
        for name, (host, desc) in adblock.DNS_PROVIDERS.items():
            print(f"    {name:20} {host:30} {desc}")
        return 0
    if args.ads_cmd == "dns":
        host = None if args.provider == "off" else \
            adblock.DNS_PROVIDERS.get(args.provider, (args.provider, ""))[0]
        ok, err = adblock.set_private_dns(adb, actions, host)
        if not ok:
            print(f"  FAILED to set Private DNS: {err}")
            return 1
        if host is None:
            print("  Private DNS set back to automatic.")
            return 0
        print(f"  Private DNS -> {host}")
        print("  Ads are now filtered for every app and browser (Wi-Fi and mobile data).")
        print("  If a site or app breaks, run: python -m androidcleaner ads dns off")
        return 0
    if args.ads_cmd == "overlay":
        results = []
        for pkg in args.packages:
            ok, out = actions.set_appop(pkg, "SYSTEM_ALERT_WINDOW", "deny")
            results.append((pkg, ok, out))
        report_actions(results)
        return 0
    return 1


def cmd_restore(args) -> int:
    adb = get_adb(args)
    journal = Journal(adb.serial)
    entries = journal.entries()
    if args.list or (not args.all and not args.targets):
        header("Change journal")
        if not entries:
            print("  No changes recorded for this device.")
        for i, e in enumerate(entries):
            mark = "restored" if e["restored"] else "active"
            print(f"  #{i:<3} {e['ts']}  {e['action']:15} {e['target']:45} {mark}  {e['detail']}")
        if entries:
            print("\n  Undo: restore --all | restore <package or #index> ...")
        return 0
    if args.all:
        idx = None
    else:
        idx = []
        for t in args.targets:
            if t.lstrip("#").isdigit():
                idx.append(int(t.lstrip("#")))
            else:
                idx += [i for i, e in enumerate(entries) if e["target"] == t]
    for line in restore(adb, idx, args.dry_run):
        print(f"  {line}")
    return 0


def cmd_checkup(args) -> int:
    """Read-only check of everything, with a summary and recommended commands."""
    from . import checkup
    adb = get_adb(args)
    print(f"Memeriksa HP {adb.serial} (tidak ada yang diubah)...")
    sections, data = checkup.run(adb, days=args.days)
    print(checkup.render(sections, data))
    print(f"\n  Laporan tersimpan: {checkup.save(sections, data)}")
    return 1 if any(s.status == checkup.BAD for s in sections) else 0


# ---------------------------------------------------------------- parser
def build_parser() -> argparse.ArgumentParser:
    # Global flags are accepted before or after the subcommand. SUPPRESS keeps a subparser from
    # overwriting a flag that was given earlier on the command line.
    common = argparse.ArgumentParser(add_help=False, argument_default=argparse.SUPPRESS)
    common.add_argument("-s", "--serial", help="device serial (when several are connected)")
    common.add_argument("-n", "--dry-run", action="store_true", help="show commands, change nothing")
    common.add_argument("-y", "--yes", action="store_true", help="do not ask for confirmation")
    common.add_argument("-v", "--verbose", action="store_true", help="print every adb command")

    p = argparse.ArgumentParser(prog="androidcleaner", parents=[common],
                                description="Clean Android devices of ads, malware, bloatware and junk over ADB.")
    sub = p.add_subparsers(dest="command", required=True)

    sub.add_parser("devices", parents=[common], help="list connected devices").set_defaults(func=cmd_devices)
    sub.add_parser("info", parents=[common], help="device, storage and app summary").set_defaults(func=cmd_info)
    s = sub.add_parser("checkup", parents=[common],
                       help="check everything (read-only) and recommend commands; saves a Markdown report")
    s.add_argument("--days", type=int, default=30, help="idle threshold for unused apps")
    s.set_defaults(func=cmd_checkup)

    s = sub.add_parser("scan", parents=[common], help="heuristic malware/adware scan (read-only)")
    s.add_argument("--all", action="store_true", help="list every app, including clean and system ones")
    s.add_argument("--virustotal", action="store_true",
                   help="hash flagged APKs and look them up on VirusTotal (needs VT_API_KEY)")
    s.set_defaults(func=cmd_scan)

    s = sub.add_parser("quarantine", parents=[common],
                       help="back up APK, strip accessibility/overlay/admin, uninstall")
    s.add_argument("packages", nargs="+")
    s.add_argument("--no-backup", action="store_true", help="do not pull the APK first")
    s.set_defaults(func=cmd_quarantine)

    s = sub.add_parser("apps", parents=[common], help="list packages")
    s.add_argument("--system", action="store_true", help="include system apps")
    s.add_argument("--all", action="store_true", help="include apps removed for this user")
    s.set_defaults(func=cmd_apps)

    for name, fn, hlp in [("disable", cmd_disable, "disable packages (reversible)"),
                          ("enable", cmd_enable, "re-enable packages"),
                          ("uninstall", cmd_uninstall, "uninstall for user 0, keep system APK (reversible)")]:
        s = sub.add_parser(name, parents=[common], help=hlp)
        s.add_argument("packages", nargs="+")
        s.set_defaults(func=fn)

    s = sub.add_parser("debloat", parents=[common], help="remove preinstalled bloatware by brand list")
    s.add_argument("--level", choices=debloat.LEVELS, default="recommended")
    s.add_argument("--mode", choices=["disable", "uninstall"], default="disable")
    s.add_argument("--brand", help="override detected brand list")
    s.add_argument("--apply", action="store_true", help="actually make the changes")
    s.set_defaults(func=cmd_debloat)

    s = sub.add_parser("unused", parents=[common],
                       help="find apps never/rarely used, pick which to remove")
    s.add_argument("--days", type=int, default=30, help="idle threshold in days (default 30)")
    s.add_argument("--system", action="store_true", help="also list system apps that have an icon")
    s.add_argument("--select", help="remove these list numbers without prompting, e.g. 1,3,5-7")
    s.add_argument("--no-backup", action="store_true", help="do not pull APKs before uninstalling")
    s.set_defaults(func=cmd_unused)

    s = sub.add_parser("clean", parents=[common], help="clear app caches and junk files")
    s.add_argument("--apks", action="store_true", help="also delete downloaded .apk installers")
    s.add_argument("--empty-dirs", action="store_true", help="also remove empty folders")
    s.add_argument("--kill-background", action="store_true", help="stop background processes")
    s.add_argument("--large", type=int, metavar="MB", help="list files bigger than MB")
    s.set_defaults(func=cmd_clean)

    s = sub.add_parser("ads", parents=[common], help="system-wide ad blocking")
    ads = s.add_subparsers(dest="ads_cmd")
    ads.add_parser("status", parents=[common])
    d = ads.add_parser("dns", parents=[common], help="set Private DNS ad-block resolver")
    d.add_argument("provider", help="provider name, a hostname, or 'off'")
    o = ads.add_parser("overlay", parents=[common], help="deny draw-over-apps (pop-up ads)")
    o.add_argument("packages", nargs="+")
    s.set_defaults(func=cmd_ads)

    s = sub.add_parser("restore", parents=[common], help="list or undo recorded changes")
    s.add_argument("targets", nargs="*", help="package names or #index from the journal")
    s.add_argument("--all", action="store_true", help="undo every active change")
    s.add_argument("--list", action="store_true")
    s.set_defaults(func=cmd_restore)
    return p


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    args = build_parser().parse_args(argv)
    # Not via set_defaults: parent-parser actions are shared, so that would undo SUPPRESS.
    for key, value in (("serial", None), ("dry_run", False), ("yes", False), ("verbose", False)):
        if not hasattr(args, key):
            setattr(args, key, value)
    try:
        return args.func(args) or 0
    except AdbError as e:
        print(f"error: {e}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        return 130
