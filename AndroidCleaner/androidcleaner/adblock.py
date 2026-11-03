"""System-wide ad blocking without root: Private DNS (DNS-over-TLS) to a filtering resolver,
plus finding and revoking pop-up (overlay) permissions used by adware."""
from __future__ import annotations

import re

from .adb import Adb
from .packages import PackageActions
from .scanner import KNOWN_GOOD, appop_allowed

DNS_PROVIDERS = {
    "adguard": ("dns.adguard-dns.com", "AdGuard DNS - blocks ads, trackers, phishing"),
    "adguard-family": ("family.adguard-dns.com", "AdGuard Family - ads + adult content + safe search"),
    "mullvad": ("adblock.dns.mullvad.net", "Mullvad - blocks ads and trackers"),
    "mullvad-all": ("all.dns.mullvad.net", "Mullvad - ads, trackers, malware, adult, gambling"),
    "controld": ("p2.freedns.controld.com", "Control D free - ads, trackers, malware"),
    "cloudflare-security": ("security.cloudflare-dns.com", "Cloudflare - malware only (no ad blocking)"),
    "quad9": ("dns.quad9.net", "Quad9 - malware only (no ad blocking)"),
}


def dns_status(adb: Adb) -> tuple[str, str]:
    mode = adb.shell("settings get global private_dns_mode").strip()
    spec = adb.shell("settings get global private_dns_specifier").strip()
    return mode, spec


def dns_validated(adb: Adb) -> list[str]:
    """Resolver addresses the phone has actually validated for Private DNS (empty = not working
    or not enabled). Taken from the active networks in `dumpsys connectivity`."""
    out = adb.shell("dumpsys connectivity", timeout=120)
    addrs: list[str] = []
    for m in re.finditer(r"ValidatedPrivateDnsAddresses: \[([^\]]*)\]", out):
        addrs += [a.strip() for a in m.group(1).split(",") if a.strip()]
    return sorted(set(addrs))


def set_private_dns(adb: Adb, actions: PackageActions, hostname: str | None) -> None:
    """hostname=None turns Private DNS back to automatic (opportunistic)."""
    if adb.sdk() < 28:
        raise RuntimeError("Private DNS needs Android 9 or newer.")
    if hostname is None:
        actions.put_setting("global", "private_dns_mode", "opportunistic")
        return
    actions.put_setting("global", "private_dns_specifier", hostname)
    actions.put_setting("global", "private_dns_mode", "hostname")


def overlay_apps(adb: Adb, third_party: set[str]) -> list[str]:
    """Third-party apps currently allowed to draw over other apps."""
    return sorted(p for p in appop_allowed(adb, "SYSTEM_ALERT_WINDOW")
                  if p in third_party and p not in KNOWN_GOOD)
