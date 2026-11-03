"""Offline tests: parsers and scoring run against captured adb output, no device needed."""
import unittest

from androidcleaner import debloat
from androidcleaner.adb import parse_devices
from androidcleaner.cleaner import parse_df
from androidcleaner.cli import build_parser
from androidcleaner.packages import (Package, parse_component_packages, parse_dumpsys_packages,
                                     parse_pm_list)
from androidcleaner.scanner import score_package

PM_LIST = """\
package:/system_ext/framework/framework-ext-res/framework-ext-res.apk=com.miui.rom  installer=null uid:10086
package:/data/app/~~W6p==/com.google.android.apps.tachyon-Lh==/base.apk=com.google.android.apps.tachyon  installer=com.android.vending uid:10192
package:/data/app/~~x==/com.evil.cleaner-y==/base.apk=com.evil.cleaner  installer=com.google.android.packageinstaller uid:10300
"""

DUMPSYS = """\
Packages:
  Package [com.whatsapp] (edd2b36):
    versionName=2.26.42.74
    declared permissions:
      com.whatsapp.permission.BROADCAST: prot=signature
    requested permissions:
      android.permission.RECEIVE_SMS
      android.permission.READ_SMS: restricted=true
    install permissions:
      android.permission.RECEIVE_BOOT_COMPLETED: granted=true
      android.permission.INTERNET: granted=true
    User 0: ceDataInode=34258 installed=true hidden=false enabled=0
      firstInstallTime=2025-05-08 14:48:28
      gids=[3003]
      runtime permissions:
        android.permission.READ_SMS: granted=false, flags=[ USER_SET ]
        android.permission.CAMERA: granted=true, flags=[ USER_SET ]
  Package [com.other] (abc):
    versionName=1.0
"""

NO_ACCESS = {k: set() for k in ("device_admin", "accessibility", "notification_listener",
                                "input_method", "overlay", "install_unknown", "usage_stats",
                                "default_sms", "home")}


class ParserTests(unittest.TestCase):
    def test_devices(self):
        out = "List of devices attached\nABC123   device product:x model:2409FPCC4G\nXYZ unauthorized transport_id:1\n"
        devs = parse_devices(out)
        self.assertEqual([(d.serial, d.state) for d in devs], [("ABC123", "device"), ("XYZ", "unauthorized")])
        self.assertEqual(devs[0].props["model"], "2409FPCC4G")

    def test_pm_list(self):
        pkgs = parse_pm_list(PM_LIST)
        self.assertEqual(pkgs["com.google.android.apps.tachyon"]["installer"], "com.android.vending")
        self.assertEqual(pkgs["com.miui.rom"]["uid"], "10086")
        self.assertTrue(pkgs["com.evil.cleaner"]["path"].endswith("base.apk"))

    def test_dumpsys(self):
        info = parse_dumpsys_packages(DUMPSYS)
        wa = info["com.whatsapp"]
        self.assertEqual(wa["requested"], {"android.permission.RECEIVE_SMS", "android.permission.READ_SMS"})
        self.assertEqual(wa["granted"], {"android.permission.RECEIVE_BOOT_COMPLETED",
                                         "android.permission.INTERNET", "android.permission.CAMERA"})
        self.assertEqual(wa["first_install"], "2025-05-08 14:48:28")
        self.assertEqual(info["com.other"]["version"], "1.0")

    def test_components(self):
        self.assertEqual(parse_component_packages(
            "com.a.b/.Svc:com.c.d/com.c.d.Listener$Inner"), {"com.a.b", "com.c.d"})

    def test_df(self):
        rows = parse_df("Filesystem 1K-blocks Used Available Use% Mounted on\n"
                        "/dev/block/dm-50 238769112 50000000 188000000 21% /data\n")
        self.assertEqual(rows[0].mount, "/data")
        self.assertAlmostEqual(rows[0].pct, 20.9, places=1)


class ScoringTests(unittest.TestCase):
    def test_sideloaded_hidden_adware_is_high(self):
        p = Package("com.evil.cleaner", path="/data/app/x/base.apk",
                    installer="com.google.android.packageinstaller", has_launcher=False,
                    granted_perms={"android.permission.READ_SMS"})
        access = dict(NO_ACCESS, overlay={"com.evil.cleaner"}, accessibility={"com.evil.cleaner"})
        self.assertEqual(score_package(p, access).verdict, "HIGH")

    def test_disguised_system_name(self):
        p = Package("com.android.systemupdate", path="/data/app/x/base.apk", installer="")
        f = score_package(p, NO_ACCESS)
        self.assertIn("disguised", " ".join(f.reasons))

    def test_play_store_and_preload_apps_are_clean(self):
        play = Package("com.google.android.safetycore", path="/data/app/x/base.apk",
                       installer="com.android.vending", has_launcher=False)
        preload = Package("com.miui.calculator", path="/product/data-app/Calc/Calc.apk",
                          installer="null", has_launcher=True)
        access = dict(NO_ACCESS, overlay={"com.miui.calculator"})
        self.assertEqual(score_package(play, access).verdict, "OK")
        self.assertEqual(score_package(preload, access).verdict, "OK")


class DebloatTests(unittest.TestCase):
    def test_list_is_well_formed(self):
        data = debloat.load()
        seen = set()
        for items in data["lists"].values():
            for item in items:
                self.assertIn(item["level"], debloat.LEVELS)
                self.assertNotIn(item["pkg"], seen, "duplicate package")
                seen.add(item["pkg"])

    def test_candidates_respect_level_and_state(self):
        pkgs = {"com.miui.msa.global": Package("com.miui.msa.global", system=True),
                "com.miui.player": Package("com.miui.player", system=True, enabled=False)}
        rec = debloat.candidates(pkgs, "xiaomi", "recommended")
        self.assertEqual([c.pkg for c in rec], ["com.miui.msa.global"])
        adv = {c.pkg: c.state for c in debloat.candidates(pkgs, "xiaomi", "advanced")}
        self.assertEqual(adv["com.miui.player"], "disabled")


class CliTests(unittest.TestCase):
    def test_global_flags_before_or_after_subcommand(self):
        p = build_parser()
        self.assertTrue(p.parse_args(["-n", "clean"]).dry_run)
        self.assertTrue(p.parse_args(["clean", "-n"]).dry_run)
        self.assertTrue(p.parse_args(["-y", "ads", "dns", "adguard"]).yes)


if __name__ == "__main__":
    unittest.main()
