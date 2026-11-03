"""Thin wrapper around the adb executable."""
from __future__ import annotations

import os
import shlex
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent


class AdbError(RuntimeError):
    pass


def find_adb() -> str:
    """Locate adb: $ADB, bundled .tools/platform-tools, Android SDK, then PATH."""
    exe = "adb.exe" if os.name == "nt" else "adb"
    candidates = []
    if os.environ.get("ADB"):
        candidates.append(Path(os.environ["ADB"]))
    candidates.append(PROJECT_ROOT / ".tools" / "platform-tools" / exe)
    for var in ("ANDROID_HOME", "ANDROID_SDK_ROOT"):
        if os.environ.get(var):
            candidates.append(Path(os.environ[var]) / "platform-tools" / exe)
    if os.environ.get("LOCALAPPDATA"):
        candidates.append(Path(os.environ["LOCALAPPDATA"]) / "Android" / "Sdk" / "platform-tools" / exe)
    for c in candidates:
        if c.is_file():
            return str(c)
    found = shutil.which("adb")
    if found:
        return found
    raise AdbError(
        "adb not found. Run scripts/install_platform_tools.ps1 or set the ADB environment variable."
    )


@dataclass
class DeviceEntry:
    serial: str
    state: str
    props: dict


def list_devices(adb_path: str | None = None) -> list[DeviceEntry]:
    adb_path = adb_path or find_adb()
    out = subprocess.run([adb_path, "devices", "-l"], capture_output=True, text=True,
                         encoding="utf-8", errors="replace").stdout
    return parse_devices(out)


def parse_devices(output: str) -> list[DeviceEntry]:
    devices = []
    for line in output.splitlines():
        line = line.strip()
        if not line or line.startswith("List of devices") or line.startswith("*"):
            continue
        parts = line.split()
        if len(parts) < 2:
            continue
        props = dict(p.split(":", 1) for p in parts[2:] if ":" in p)
        devices.append(DeviceEntry(parts[0], parts[1], props))
    return devices


class Adb:
    """Runs commands against one device."""

    def __init__(self, serial: str | None = None, adb_path: str | None = None, verbose: bool = False):
        self.adb_path = adb_path or find_adb()
        self.verbose = verbose
        self.serial = serial or self._pick_device()

    def _pick_device(self) -> str:
        devices = list_devices(self.adb_path)
        ready = [d for d in devices if d.state == "device"]
        if len(ready) == 1:
            return ready[0].serial
        if not devices:
            raise AdbError("No device connected. Enable USB debugging and plug the phone in.")
        if not ready:
            states = ", ".join(f"{d.serial}={d.state}" for d in devices)
            hint = ""
            if any(d.state == "unauthorized" for d in devices):
                hint = " Unlock the phone and accept the 'Allow USB debugging' prompt."
            raise AdbError(f"No usable device ({states}).{hint}")
        raise AdbError("Several devices connected; pass --serial. "
                       + ", ".join(d.serial for d in ready))

    def run(self, *args: str, check: bool = True, timeout: int = 300) -> str:
        cmd = [self.adb_path, "-s", self.serial, *args]
        if self.verbose:
            print("  $", " ".join(cmd[3:]))
        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                                  errors="replace", timeout=timeout)
        except subprocess.TimeoutExpired as e:
            raise AdbError(f"Timed out: {' '.join(args)}") from e
        if check and proc.returncode != 0:
            raise AdbError(f"adb {' '.join(args)} failed: {(proc.stderr or proc.stdout).strip()}")
        return proc.stdout

    def shell(self, command: str, check: bool = False, timeout: int = 300) -> str:
        """Run a shell command on the device. Returns stdout (stderr merged on failure)."""
        cmd = [self.adb_path, "-s", self.serial, "shell", command]
        if self.verbose:
            print("  $ adb shell", command)
        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                                  errors="replace", timeout=timeout)
        except subprocess.TimeoutExpired as e:
            raise AdbError(f"Timed out: {command}") from e
        if check and proc.returncode != 0:
            raise AdbError(f"'{command}' failed: {(proc.stderr or proc.stdout).strip()}")
        return proc.stdout + (proc.stderr if proc.returncode != 0 else "")

    def getprop(self, key: str) -> str:
        return self.shell(f"getprop {key}").strip()

    def sdk(self) -> int:
        try:
            return int(self.getprop("ro.build.version.sdk"))
        except ValueError:
            return 0


def q(s: str) -> str:
    """Quote an argument for the device shell."""
    return shlex.quote(s)
