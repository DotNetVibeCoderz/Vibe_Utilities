"""Package inventory and reversible package actions."""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

from .adb import PROJECT_ROOT, Adb, AdbError, q
from .journal import Journal

QUARANTINE_DIR = PROJECT_ROOT / "quarantine"


@dataclass
class Package:
    name: str
    path: str = ""
    installer: str = ""
    uid: str = ""
    system: bool = False
    enabled: bool = True
    installed: bool = True          # installed for user 0
    has_launcher: bool = False
    requested_perms: set[str] = field(default_factory=set)
    granted_perms: set[str] = field(default_factory=set)
    first_install: str = ""
    version: str = ""


_PKG_LINE = re.compile(r"^package:(?:(?P<path>.+?)=)?(?P<name>[\w.]+)(?P<rest>.*)$")


def parse_pm_list(output: str) -> dict[str, dict]:
    """Parse `pm list packages [-f] [-i] [-U]` output into {name: {path, installer, uid}}."""
    result = {}
    for line in output.splitlines():
        m = _PKG_LINE.match(line.strip())
        if not m:
            continue
        rest = m.group("rest")
        installer = re.search(r"installer=(\S+)", rest)
        uid = re.search(r"uid:(\d+)", rest)
        result[m.group("name")] = {
            "path": m.group("path") or "",
            "installer": (installer.group(1) if installer else ""),
            "uid": uid.group(1) if uid else "",
        }
    return result


def parse_dumpsys_packages(output: str) -> dict[str, dict]:
    """Parse `dumpsys package packages` into per-package permission and install info."""
    info: dict[str, dict] = {}
    current = None
    section = None
    for raw in output.splitlines():
        m = re.match(r"^\s{2}Package \[([\w.]+)\]", raw)
        if m:
            current = info.setdefault(m.group(1), {
                "requested": set(), "granted": set(), "first_install": "", "version": ""})
            section = None
            continue
        if current is None:
            continue
        line = raw.strip()
        if line.startswith("requested permissions:"):
            section = "requested"
            continue
        if line.startswith(("install permissions:", "runtime permissions:")):
            section = "granted"
            continue
        if line.endswith(":") and not line.startswith("android.") and section:
            # e.g. "User 0:" or "declared permissions:" closes the list
            if not line.startswith("User "):
                section = None
            continue
        if line.startswith("firstInstallTime="):
            current["first_install"] = line.split("=", 1)[1]
        elif line.startswith("versionName="):
            current["version"] = line.split("=", 1)[1]
        elif section == "requested" and re.match(r"^[\w.]+(:|$)", line):
            current["requested"].add(line.split(":")[0])
        elif section == "granted" and "granted=true" in line:
            current["granted"].add(line.split(":")[0])
        elif section and "=" in line and not line.startswith(("android.", "com.")):
            section = None
    return info


def parse_component_packages(output: str) -> set[str]:
    """Extract package names from lines like `com.foo/.Main` or `ComponentInfo{com.foo/...}`."""
    pkgs = set()
    for m in re.finditer(r"([A-Za-z][\w]*(?:\.[\w]+)+)/[\w.$]+", output):
        pkgs.add(m.group(1))
    return pkgs


def launcher_packages(adb: Adb) -> set[str]:
    out = adb.shell("cmd package query-activities --brief -a android.intent.action.MAIN "
                    "-c android.intent.category.LAUNCHER")
    return parse_component_packages(out)


def inventory(adb: Adb, with_details: bool = True) -> dict[str, Package]:
    all_pkgs = parse_pm_list(adb.shell("pm list packages -u -f -i -U"))
    if not all_pkgs:  # very old pm without -U
        all_pkgs = parse_pm_list(adb.shell("pm list packages -u -f -i"))
    installed = set(parse_pm_list(adb.shell("pm list packages")))
    system = set(parse_pm_list(adb.shell("pm list packages -s -u")))
    disabled = set(parse_pm_list(adb.shell("pm list packages -d")))
    launchers = launcher_packages(adb)

    pkgs: dict[str, Package] = {}
    for name, meta in all_pkgs.items():
        pkgs[name] = Package(
            name=name, path=meta["path"], installer=meta["installer"], uid=meta["uid"],
            system=name in system, enabled=name not in disabled,
            installed=name in installed, has_launcher=name in launchers,
        )
    if with_details:
        details = parse_dumpsys_packages(adb.shell("dumpsys package packages", timeout=600))
        for name, d in details.items():
            p = pkgs.get(name)
            if p:
                p.requested_perms = d["requested"]
                p.granted_perms = d["granted"]
                p.first_install = d["first_install"]
                p.version = d["version"]
    return pkgs


class PackageActions:
    """Package changes that are journaled for `restore`."""

    def __init__(self, adb: Adb, dry_run: bool = False):
        self.adb = adb
        self.dry_run = dry_run
        self.journal = Journal(adb.serial)

    def _exec(self, cmd: str) -> tuple[bool, str]:
        if self.dry_run:
            print(f"  [dry-run] adb shell {cmd}")
            return True, ""
        out = self.adb.shell(cmd).strip()
        ok = not re.search(r"(Failure|Exception|Error|not allowed|Unknown package)", out, re.I)
        if not ok:
            # Keep the first meaningful line, not a Java stack trace.
            msg = next((l for l in out.splitlines() if "Exception:" in l or "Failure" in l),
                       out.splitlines()[0] if out else "")
            out = msg.split("Exception:", 1)[-1].strip()
        return ok, out

    def disable(self, pkg: str) -> tuple[bool, str]:
        """Disable for user 0. Some ROMs (HyperOS/MIUI) refuse to disable system packages;
        those fall back to a reversible per-user uninstall."""
        self._exec(f"am force-stop {q(pkg)}")
        ok, out = self._exec(f"pm disable-user --user 0 {q(pkg)}")
        if ok and not self.dry_run:
            self.journal.record("disable", pkg, [{"shell": f"pm enable --user 0 {q(pkg)}"}])
        elif not ok and "Cannot disable" in out:
            ok, out2 = self.uninstall_for_user(pkg)
            out = "ROM blocks disable; removed for user 0 instead" if ok else out2
        return ok, out

    def enable(self, pkg: str) -> tuple[bool, str]:
        return self._exec(f"pm enable --user 0 {q(pkg)}")

    def uninstall_for_user(self, pkg: str) -> tuple[bool, str]:
        """Remove the package for user 0 but keep the APK on the system partition (restorable)."""
        ok, out = self._exec(f"pm uninstall -k --user 0 {q(pkg)}")
        if ok and not self.dry_run:
            self.journal.record("uninstall-user", pkg,
                                [{"shell": f"cmd package install-existing --user 0 {q(pkg)}"}])
        return ok, out

    def backup_apk(self, pkg: str) -> list[str]:
        """Pull all APK splits of a package into quarantine/<serial>/<pkg>/."""
        paths = [line.split(":", 1)[1].strip() for line in
                 self.adb.shell(f"pm path {q(pkg)}").splitlines() if line.startswith("package:")]
        dest = QUARANTINE_DIR / self.adb.serial / pkg
        saved = []
        if self.dry_run:
            for p in paths:
                print(f"  [dry-run] adb pull {p} {dest}")
            return paths
        dest.mkdir(parents=True, exist_ok=True)
        for p in paths:
            local = dest / Path(p).name
            self.adb.run("pull", p, str(local))
            saved.append(str(local))
        return saved

    def remove_app(self, pkg: str, backup: bool = True) -> tuple[bool, str]:
        """Fully uninstall a third-party app, backing its APK up first so it can be reinstalled."""
        apk_paths = [l.split(":", 1)[1].strip() for l in
                     self.adb.shell(f"pm path {q(pkg)}").splitlines() if l.startswith("package:")]
        # A preload whose APK lives on a read-only partition stays there after a per-user
        # uninstall, so install-existing brings it back without a backup.
        on_system = bool(apk_paths) and not any(p.startswith("/data/") for p in apk_paths)
        saved = self.backup_apk(pkg) if backup and not on_system else []
        self._exec(f"am force-stop {q(pkg)}")
        ok, out = self._exec(f"pm uninstall --user 0 {q(pkg)}")
        if ok and not self.dry_run:
            if on_system:
                undo = [{"shell": f"cmd package install-existing --user 0 {q(pkg)}"}]
            else:
                undo = [{"install": saved}] if saved else []
            self.journal.record("uninstall", pkg, undo,
                                detail="APK backed up" if saved else "no backup")
        return ok, out

    def set_appop(self, pkg: str, op: str, mode: str) -> tuple[bool, str]:
        prev = self.adb.shell(f"appops get {q(pkg)} {op}").strip()
        m = re.search(rf"{op}: (\w+)", prev)
        prev_mode = m.group(1) if m else "default"
        ok, out = self._exec(f"appops set {q(pkg)} {op} {mode}")
        if ok and not self.dry_run:
            self.journal.record("appop", pkg, [{"shell": f"appops set {q(pkg)} {op} {prev_mode}"}],
                                detail=f"{op}: {prev_mode} -> {mode}")
        return ok, out

    def revoke_permission(self, pkg: str, perm: str) -> tuple[bool, str]:
        ok, out = self._exec(f"pm revoke {q(pkg)} {perm}")
        if ok and not self.dry_run:
            self.journal.record("revoke", pkg, [{"shell": f"pm grant {q(pkg)} {perm}"}], detail=perm)
        return ok, out

    def put_setting(self, namespace: str, key: str, value: str | None) -> tuple[bool, str]:
        prev = self.adb.shell(f"settings get {namespace} {key}").strip()
        if value is None:
            cmd = f"settings delete {namespace} {key}"
        else:
            cmd = f"settings put {namespace} {key} {q(value)}"
        ok, out = self._exec(cmd)
        if ok and not self.dry_run:
            undo = (f"settings delete {namespace} {key}" if prev in ("", "null")
                    else f"settings put {namespace} {key} {q(prev)}")
            self.journal.record("setting", f"{namespace}/{key}", [{"shell": undo}],
                                detail=f"{prev} -> {value}")
        return ok, out


def restore(adb: Adb, indexes: list[int] | None = None, dry_run: bool = False) -> list[str]:
    """Undo journaled actions (newest first). Returns human-readable results."""
    journal = Journal(adb.serial)
    entries = journal.entries()
    targets = indexes if indexes is not None else [
        i for i, e in enumerate(entries) if not e["restored"]]
    results, done = [], set()
    for i in sorted(targets, reverse=True):
        e = entries[i]
        if e["restored"]:
            continue
        ok = True
        for step in e["undo"]:
            if "shell" in step:
                if dry_run:
                    print(f"  [dry-run] adb shell {step['shell']}")
                    continue
                out = adb.shell(step["shell"])
                if re.search(r"(Failure|Exception|Error)", out):
                    ok = False
                    results.append(f"FAIL {e['action']} {e['target']}: {out.strip()}")
            elif "install" in step:
                apks = step["install"]
                if dry_run:
                    print(f"  [dry-run] adb install-multiple -r {' '.join(apks)}")
                    continue
                try:
                    args = ["install-multiple", "-r", *apks] if len(apks) > 1 else ["install", "-r", *apks]
                    adb.run(*args, timeout=600)
                except AdbError as ex:
                    ok = False
                    results.append(f"FAIL reinstall {e['target']}: {ex}")
        if not e["undo"]:
            ok = False
            results.append(f"SKIP {e['action']} {e['target']}: nothing recorded to undo")
        if ok:
            done.add(i)
            results.append(f"OK   undo {e['action']} {e['target']}")
    if done and not dry_run:
        journal.mark_restored(done)
    return results
