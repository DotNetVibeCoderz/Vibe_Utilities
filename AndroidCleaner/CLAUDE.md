# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project purpose

AndroidCleaner is a Python CLI (standard library only, Python 3.10+) that cleans Android phones of adware, malware, bloatware and junk/cache files. It drives the phone over ADB from a Windows PC, without root. User-facing docs (`README.md`, `docs/`) and the `checkup` report are in Indonesian, the user's language. Other CLI output is in English. When behaviour or options change, update the matching `docs/NN-*.md` page.

## Commands

```
python -m androidcleaner <command> [-n] [-y] [-s SERIAL] [-v]   # or: ac <command>
cek-semua.cmd [--days N]                                         # one-click: fetch adb if missing, then checkup (pauses only when run without args)
python -m unittest discover -s tests -v                          # offline tests, no device needed
python -m unittest tests.test_usage.UsageTests.test_select_unused   # single test
powershell -File scripts\install_platform_tools.ps1              # download adb into .tools/
```

Use `-n/--dry-run` to see the exact `adb shell` commands without changing the device. `checkup`, `info`, `scan`, `apps`, `ads status` and `restore --list` are read-only.

## Architecture

- `adb.py`: finds adb (in order: `$ADB`, `.tools/platform-tools`, Android SDK, PATH), picks the single authorized device, and runs commands. `Adb.shell()` does not raise on failure. It returns stdout plus stderr, so callers detect errors by matching `Failure|Exception|Error` in the text.
- `packages.py`: builds the package inventory from `pm list packages` variants, `cmd package query-activities` (launcher icons) and `dumpsys package packages` (granted permissions). `PackageActions` is the only place that changes packages or settings, and every change goes through `journal.py`.
- `journal.py`: an append-only `journal/<serial>.jsonl`. Each entry stores its own `undo` steps (`{"shell": ...}` or `{"install": [apks]}`). `restore()` replays them newest first. Any new mutating action must record an undo step here.
- `scanner.py`: a heuristic risk score per app, with no signature database. Origin decides the weight: removable OEM preloads (APK outside `/data/`), trusted-store installs and untrusted/sideloaded apps are scored differently. This stops the many Xiaomi preloads and Google Play apps from showing up as false positives. Thresholds: HIGH ≥ 8, MEDIUM ≥ 4.
- `debloat.py` + `data/debloat.json`: per-brand lists (brand found from `ro.product.manufacturer`/`brand`) at `recommended` or `advanced` level. The `generic` list always applies.
- `cleaner.py`: clears shared-storage app caches, then runs `pm trim-caches` (which also empties shared-storage caches, so it must run second or the sizes read 0), then clears the fixed `JUNK_DIRS` and temp/log files.
- `usage.py`: the `unused` command. It merges every `dumpsys usagestats` interval per package (latest `lastTimeUsed`, max totals; `1970-01-01` means never) and reads sizes from the `dumpsys diskstats` JSON arrays, which are a periodic snapshot, not live. It only lists launcher apps that are enabled and older than `--days`, and skips `PROTECTED` plus the current home app. The stats window is device-limited; the yearly `timeRange` is shown to the user verbatim because its date format follows the phone's locale.
- `checkup.py`: the `checkup` command (read-only). Runs every check over a single `inventory()` (it is passed into `scanner.scan(pkgs=...)`), builds `Section`s with status OK/PERHATIAN/BAHAYA and recommended commands, prints them, and saves `reports/checkup-*.md`. It exits 1 if any section is BAHAYA. The internal-cache figure comes from the daily `diskstats` snapshot, so it is ignored for 24h after a real `clean` (`journal/<serial>.lastclean`).
- `adblock.py`: ad blocking through Private DNS (`settings global private_dns_*`) and overlay app-op denial. `dns_validated()` reads `ValidatedPrivateDnsAddresses` from `dumpsys connectivity` to confirm the resolver actually works.
- `cli.py`: argparse. Global flags live on a `common` parent parser with `argument_default=SUPPRESS`, and their defaults are filled in `main()`. Do not use `set_defaults` on the root parser: parent actions are shared objects, so it would overwrite the flags again in the subparsers.

## Device behaviour learned on the test phone (Xiaomi 2409FPCC4G, HyperOS, Android 14)

- `pm disable-user` on system packages fails with `SecurityException: Cannot disable system packages`. `PackageActions.disable()` falls back to `pm uninstall -k --user 0`, which `cmd package install-existing` undoes.
- On this build, `dumpsys package packages` does not include `requested permissions:`. Only granted install/runtime permissions are available in bulk, so scoring relies on `granted_perms`.
- Shell can read and delete `/sdcard/Android/data/*/cache` on Android 14.

## Device behaviour learned on a second phone (POCO F3 M2012K11AG, MIUI 12.0.2, Android 11)

- `pm disable-user` works for system packages here (the HyperOS refusal is version-specific).
- Without Developer options > "USB debugging (Security settings)", `settings put global ...` throws `SecurityException ... WRITE_SECURE_SETTINGS`. `PackageActions._exec` appends `SECURE_SETTINGS_HINT`, and `adblock.set_private_dns()` reads the values back and returns `(ok, err)`; never print success without checking it. After the user enables that option, it only takes effect once USB debugging is toggled off/on and the cable replugged; restarting adb from the PC is not enough.
- Old MIUI preloads look like user apps: `/data/app/<name>/<name>.apk` (not `base.apk`), installer `android`, or `firstInstallTime` in 1970. `scanner.is_oem_preload()` covers these. `usage.py` deliberately still treats `/data/app` preloads as user apps, because uninstalling them deletes the APK, so they need a backup.
- `com.xiaomi.discover` silently installs promotional games; they show up with installer `com.xiaomi.discover`.

## Device behaviour learned on a third phone (Galaxy S10 SM-G973F, One UI, Android 9)

- `dumpsys usagestats` has one block per user (`user=0`, `user=150` = Secure Folder); `parse_usagestats` reads only user 0. History can be reset to a few hours; `window_days()` turns the locale-formatted yearly `timeRange` into days (both d/m and m/d tried, shorter wins), and `unused`/`checkup` refuse to list apps when it is shorter than `--days` (`unused --force` overrides).
- No `ValidatedPrivateDnsAddresses` in `dumpsys connectivity`: `parse_dns_validation()` falls back to `UsePrivateDns: true PrivateDnsServerName: <host>` on a `VALIDATED` network. `ad_block_probe()` pings `doubleclick.net` on the phone; AdGuard answers 127.0.0.1 there.
- Old WhatsApp path `/sdcard/WhatsApp/.Shared`; `*.log`/`*.tmp` under Download/Documents/WhatsApp Documents/WhatsApp Media are user files (`cleaner.keep_file`). Files inside `JUNK_DIRS` are not counted again as junk files.
- `DCIM/.thumbnails` held 54,688 files: `rm dir/*` failed with "Argument list too long" while the tool reported success. Deletes now go through `find ... -exec rm -rf {} +`, and `clean_junk_dirs` re-measures each folder and reports what could not be deleted.
- Galaxy Watch plugins come from installer `com.samsung.android.app.watchmanager` (trusted); Xamarin debug runtimes `Mono.Android.*` are skipped by the scanner.

## Generated, git-ignored directories

`.tools/` (adb), `journal/` (undo log, keep it so `restore` works), `quarantine/` (APK backups), `reports/` (scan JSON).
