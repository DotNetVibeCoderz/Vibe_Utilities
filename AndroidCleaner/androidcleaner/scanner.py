"""Heuristic malware / adware audit.

There is no signature engine here: each app gets a risk score from the special access it holds
(device admin, accessibility, overlay, notification access...), how it was installed, whether it
hides its launcher icon, and dangerous permission combinations. Optional VirusTotal hash lookups
add a reputation verdict.
"""
from __future__ import annotations

import json
import os
import re
import urllib.error
import urllib.request
from dataclasses import dataclass, field

from .adb import Adb, q
from .packages import Package, inventory, parse_component_packages

TRUSTED_INSTALLERS = {
    "com.android.vending",                 # Google Play
    "com.sec.android.app.samsungapps",     # Galaxy Store
    "com.xiaomi.mipicks", "com.xiaomi.market",
    "com.huawei.appmarket",
    "com.heytap.market", "com.oppo.market",
    "com.bbk.appstore",
    "com.amazon.venezia",
    "com.facebook.system",                 # preinstalled Facebook installer
    "com.google.android.feedback",         # Play on some older builds
}
SIDELOAD_INSTALLERS = {"", "null", "com.google.android.packageinstaller",
                       "com.android.packageinstaller", "com.android.shell", "adb"}

# Apps that legitimately hold powerful access; they are still listed, just not scored.
KNOWN_GOOD = {
    "com.google.android.gms", "com.google.android.gsf", "com.android.vending",
    "com.google.android.apps.wellbeing", "com.google.android.inputmethod.latin",
    "com.google.android.marvin.talkback", "com.samsung.android.honeyboard",
    "com.samsung.accessibility", "com.google.android.as", "com.android.systemui",
    "com.google.android.apps.messaging", "com.google.android.dialer",
    "com.android.settings", "com.android.phone", "com.whatsapp", "com.whatsapp.w4b",
    "com.microsoft.launcher", "com.google.android.apps.nexuslauncher",
}

ADWARE_NAME_HINTS = re.compile(
    r"(clean(er)?|boost(er)?|speed|battery|cool(er)?|junk|antivirus|virus|security|"
    r"flashlight|torch|wallpaper|launcher|qrcode|qr\.?scan|vpn|free\.?fire|diamond|"
    r"fast\.?charg|phone\.?master|optimi[sz]er)", re.I)

DANGEROUS = {
    "android.permission.READ_SMS": "read SMS",
    "android.permission.RECEIVE_SMS": "receive SMS",
    "android.permission.SEND_SMS": "send SMS",
    "android.permission.READ_CALL_LOG": "read call log",
    "android.permission.PROCESS_OUTGOING_CALLS": "intercept calls",
    "android.permission.READ_CONTACTS": "read contacts",
    "android.permission.RECORD_AUDIO": "microphone",
    "android.permission.CAMERA": "camera",
    "android.permission.ACCESS_FINE_LOCATION": "precise location",
    "android.permission.ACCESS_BACKGROUND_LOCATION": "background location",
    "android.permission.REQUEST_INSTALL_PACKAGES": "install other apps",
    "android.permission.REQUEST_DELETE_PACKAGES": "delete apps",
    "android.permission.QUERY_ALL_PACKAGES": "see all installed apps",
    "android.permission.BIND_DEVICE_ADMIN": "device admin",
    "android.permission.SYSTEM_ALERT_WINDOW": "draw over other apps",
    "android.permission.PACKAGE_USAGE_STATS": "usage stats",
    "android.permission.READ_PHONE_STATE": "phone identity",
    "android.permission.MANAGE_EXTERNAL_STORAGE": "all files access",
}


@dataclass
class Finding:
    pkg: str
    score: int = 0
    reasons: list[str] = field(default_factory=list)
    system: bool = False
    installer: str = ""
    has_launcher: bool = True
    path: str = ""
    sha256: str = ""
    vt: str = ""

    def add(self, points: int, reason: str) -> None:
        if points <= 0:
            return
        self.score += points
        self.reasons.append(reason)

    @property
    def verdict(self) -> str:
        if self.score >= 8:
            return "HIGH"
        if self.score >= 4:
            return "MEDIUM"
        if self.score > 0:
            return "LOW"
        return "OK"


def _setting_components(adb: Adb, key: str) -> set[str]:
    val = adb.shell(f"settings get secure {key}").strip()
    if val in ("", "null"):
        return set()
    return parse_component_packages(val)


def appop_allowed(adb: Adb, op: str) -> set[str]:
    out = adb.shell(f"appops query-op --user 0 {op} allow")
    if "Unknown" in out or "Error" in out or "Exception" in out:
        return set()
    return {line.strip() for line in out.splitlines() if re.match(r"^[\w.]+$", line.strip())}


def special_access(adb: Adb) -> dict[str, set[str]]:
    admins = set(re.findall(r"ComponentInfo\{([\w.]+)/", adb.shell("dumpsys device_policy")))
    default_sms = adb.shell("settings get secure sms_default_application").strip()
    home = parse_component_packages(adb.shell(
        "cmd package resolve-activity --brief -a android.intent.action.MAIN "
        "-c android.intent.category.HOME"))
    return {
        "device_admin": admins,
        "accessibility": _setting_components(adb, "enabled_accessibility_services"),
        "notification_listener": _setting_components(adb, "enabled_notification_listeners"),
        "input_method": _setting_components(adb, "enabled_input_methods"),
        "overlay": appop_allowed(adb, "SYSTEM_ALERT_WINDOW"),
        "install_unknown": appop_allowed(adb, "REQUEST_INSTALL_PACKAGES"),
        "usage_stats": appop_allowed(adb, "GET_USAGE_STATS"),
        "default_sms": {default_sms} if default_sms not in ("", "null") else set(),
        "home": home,
    }


def is_oem_preload(p: Package) -> bool:
    """Apps the manufacturer put on the phone, as opposed to ones the user installed.

    - APK on a read-only partition (/product/data-app, /system/...): removable preload.
    - installer "android": installed by the system itself (MIUI preinstall provisioning).
    - Older MIUI copies preloads into /data/app/<name>/<name>.apk at first boot; user installs
      always get .../base.apk.
    - firstInstallTime in 1970: installed at first boot, before the clock was set.
    """
    if p.path and not p.path.startswith("/data/"):
        return True
    if p.installer == "android":
        return True
    if p.path.startswith("/data/app/") and not p.path.endswith("/base.apk"):
        return True
    return p.first_install.startswith("1970-")


def score_package(p: Package, access: dict[str, set[str]]) -> Finding:
    f = Finding(p.name, system=p.system, installer=p.installer,
                has_launcher=p.has_launcher, path=p.path)
    if p.name in KNOWN_GOOD:
        return f
    third_party = not p.system
    preload = third_party and is_oem_preload(p)
    store = p.installer in TRUSTED_INSTALLERS
    sideloaded = third_party and not preload and not store and p.installer in SIDELOAD_INSTALLERS
    # Untrusted origin = user-installed APK or an unknown installer: full weight for risky traits.
    untrusted = third_party and not preload and not store
    w = (lambda full, trusted: full if untrusted else (trusted if third_party else 0))

    if sideloaded:
        f.add(2, "sideloaded APK (not from an app store)")
    elif untrusted and p.installer:
        f.add(1, f"installed by {p.installer}")

    if untrusted and p.name.startswith(("com.android.", "android.", "com.google.android.")):
        f.add(5, "user-installed app disguised with a system package name")

    if untrusted and not p.has_launcher and p.installed and p.enabled:
        f.add(3, "no launcher icon (hidden app)")

    if p.name in access["device_admin"]:
        f.add(w(5, 2) or 1, "device administrator (blocks uninstall)")
    if p.name in access["accessibility"]:
        f.add(w(5, 2) or 1, "accessibility service enabled (can read/control screen)")
    if p.name in access["notification_listener"]:
        f.add(w(2, 0), "reads all notifications")
    if p.name in access["overlay"]:
        f.add(w(2, 0), "can draw over other apps (pop-up ads)")
    if p.name in access["install_unknown"]:
        f.add(w(2, 1), "allowed to install other apps")
    if p.name in access["usage_stats"]:
        f.add(w(1, 0), "usage access")
    if p.name in access["input_method"] and untrusted:
        f.add(2, "third-party keyboard (sees everything typed)")

    perms = p.granted_perms or p.requested_perms
    sms = {"android.permission.READ_SMS", "android.permission.RECEIVE_SMS",
           "android.permission.SEND_SMS"} & p.granted_perms
    if sms and p.name not in access["default_sms"]:
        f.add(w(3, 1), "SMS access while not the default SMS app (OTP theft risk)")
    if "android.permission.READ_CALL_LOG" in p.granted_perms:
        f.add(w(1, 0), "reads call log")
    spy = {"android.permission.RECORD_AUDIO", "android.permission.CAMERA",
           "android.permission.ACCESS_FINE_LOCATION"} & p.granted_perms
    if len(spy) == 3:
        f.add(w(1, 0), "microphone + camera + precise location granted")
    if ("android.permission.RECEIVE_BOOT_COMPLETED" in perms and p.name in access["overlay"]):
        f.add(w(1, 0), "starts at boot and shows overlays")

    if untrusted and ADWARE_NAME_HINTS.search(p.name) and (
            p.name in access["overlay"] or not p.has_launcher or f.score >= 2):
        f.add(2, "cleaner/booster/utility-type app with aggressive access (common adware pattern)")

    if f.score and preload:
        f.reasons.append(f"OEM preload ({p.path})")
    return f


def apk_sha256(adb: Adb, path: str) -> str:
    out = adb.shell(f"sha256sum {q(path)}").strip()
    m = re.match(r"^([0-9a-f]{64})", out)
    return m.group(1) if m else ""


def virustotal_lookup(sha256: str, api_key: str) -> str:
    req = urllib.request.Request(f"https://www.virustotal.com/api/v3/files/{sha256}",
                                 headers={"x-apikey": api_key})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            stats = json.load(r)["data"]["attributes"]["last_analysis_stats"]
        mal, sus = stats.get("malicious", 0), stats.get("suspicious", 0)
        total = sum(stats.values())
        return f"{mal} malicious / {sus} suspicious of {total} engines"
    except urllib.error.HTTPError as e:
        return "unknown to VirusTotal" if e.code == 404 else f"VT error {e.code}"
    except Exception as e:  # network errors should not abort the scan
        return f"VT error: {e}"


def scan(adb: Adb, include_system: bool = False, virustotal: bool = False,
         progress=print, pkgs: dict[str, Package] | None = None
         ) -> tuple[list[Finding], dict[str, set[str]], dict]:
    progress("  reading package list and permissions...")
    pkgs = pkgs if pkgs is not None else inventory(adb)
    progress(f"  {len(pkgs)} packages ({sum(1 for p in pkgs.values() if not p.system)} third-party)")
    progress("  reading special access (admin, accessibility, overlay, notifications)...")
    access = special_access(adb)

    findings = []
    for p in pkgs.values():
        if not p.installed:
            continue
        if p.system and not include_system:
            # system apps are only reported when they hold risky access
            if not ({p.name} & (access["device_admin"] | access["accessibility"])):
                continue
        findings.append(score_package(p, access))
    findings.sort(key=lambda f: (-f.score, f.pkg))

    api_key = os.environ.get("VT_API_KEY", "")
    if virustotal:
        flagged = [f for f in findings if f.score >= 4 and not f.system]
        progress(f"  hashing {len(flagged)} flagged APKs...")
        for f in flagged:
            base = f.path or ""
            if base:
                f.sha256 = apk_sha256(adb, base)
            if f.sha256 and api_key:
                f.vt = virustotal_lookup(f.sha256, api_key)
                m = re.match(r"(\d+) malicious", f.vt)
                if m and int(m.group(1)) >= 3:
                    f.add(10, f"VirusTotal: {f.vt}")

    protect = {
        "play_protect_verifier": adb.shell("settings get global package_verifier_enable").strip(),
        "play_protect_user_consent": adb.shell(
            "settings get global package_verifier_user_consent").strip(),
        "install_non_market_apps": adb.shell("settings get secure install_non_market_apps").strip(),
        "private_dns_mode": adb.shell("settings get global private_dns_mode").strip(),
        "private_dns_specifier": adb.shell("settings get global private_dns_specifier").strip(),
        "always_on_vpn_app": adb.shell("settings get secure always_on_vpn_app").strip(),
        "default_home": ", ".join(sorted(access["home"])),
    }
    findings.sort(key=lambda f: (-f.score, f.pkg))
    return findings, access, protect
