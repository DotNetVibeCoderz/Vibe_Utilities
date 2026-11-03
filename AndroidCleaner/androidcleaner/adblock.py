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
    return parse_dns_validation(out)


def parse_dns_validation(dumpsys_connectivity: str) -> list[str]:
    """Android 10+ lists `ValidatedPrivateDnsAddresses: [...]`. Android 9 does not; there a
    network that is VALIDATED while `UsePrivateDns: true PrivateDnsServerName: <host>` means
    strict-mode DNS-over-TLS works (validation would fail otherwise), so the host is returned."""
    addrs: list[str] = []
    for m in re.finditer(r"ValidatedPrivateDnsAddresses: \[([^\]]*)\]", dumpsys_connectivity):
        addrs += [a.strip() for a in m.group(1).split(",") if a.strip()]
    if addrs:
        return sorted(set(addrs))
    if "ValidatedPrivateDnsAddresses" in dumpsys_connectivity:
        return []  # Android 10+ format and nothing validated
    for m in re.finditer(r"UsePrivateDns: true PrivateDnsServerName: (\S+).{0,400}?Capabilities: (\S+)",
                         dumpsys_connectivity, re.S):
        if m.group(1) != "null" and "VALIDATED" in m.group(2).split("&"):
            return [m.group(1)]
    return []


BLOCKED_IPS = {"0.0.0.0", "127.0.0.1", "::", "::1"}


def ad_block_probe(adb: Adb, domain: str = "doubleclick.net") -> str:
    """Resolve a well-known ad domain on the phone. Returns 'blocked', 'not blocked' or 'unknown'."""
    out = adb.shell(f"ping -c 1 -W 2 {domain} 2>&1 | head -1", timeout=30)
    m = re.search(r"\(([0-9a-fA-F:.]+)\)", out)
    if m:
        return "blocked" if m.group(1) in BLOCKED_IPS else "not blocked"
    return "blocked" if "unknown host" in out.lower() else "unknown"


def set_private_dns(adb: Adb, actions: PackageActions, hostname: str | None) -> tuple[bool, str]:
    """hostname=None turns Private DNS back to automatic (opportunistic).
    Returns (ok, error). The values are read back, because a refused write is not always loud."""
    if adb.sdk() < 28:
        return False, "Private DNS needs Android 9 or newer."
    steps = ([("private_dns_mode", "opportunistic")] if hostname is None else
             [("private_dns_specifier", hostname), ("private_dns_mode", "hostname")])
    for key, value in steps:
        ok, out = actions.put_setting("global", key, value)
        if not ok:
            return False, out
    if actions.dry_run:
        return True, ""
    mode, spec = dns_status(adb)
    want_mode = "opportunistic" if hostname is None else "hostname"
    if mode != want_mode or (hostname and spec != hostname):
        return False, f"setting did not stick (mode={mode}, host={spec})"
    return True, ""


def overlay_apps(adb: Adb, third_party: set[str]) -> list[str]:
    """Third-party apps currently allowed to draw over other apps."""
    return sorted(p for p in appop_allowed(adb, "SYSTEM_ALERT_WINDOW")
                  if p in third_party and p not in KNOWN_GOOD)
