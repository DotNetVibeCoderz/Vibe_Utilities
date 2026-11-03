"""Offline tests for the unused-app scanner."""
import unittest
from datetime import datetime

from androidcleaner.packages import Package
from androidcleaner.usage import (_duration_seconds, parse_diskstats, parse_selection,
                                  parse_usagestats, select_unused)

USAGE = '''\
  In-memory daily stats
    packages
      package=com.whatsapp totalTimeUsed="00.21" lastTimeUsed="2026-11-03 11:59:49" appLaunchCount=1
  In-memory yearly stats
  timeRange="20/9/2026, 11.01 – 3/11/2026, 12.28"
    packages
      package=com.whatsapp totalTimeUsed="55.10.51" lastTimeUsed="2026-10-01 10:00:00" appLaunchCount=713
      package=com.old.game totalTimeUsed="02.25" lastTimeUsed="2026-09-28 05:07:12" appLaunchCount=2
      package=com.never totalTimeUsed="00.00" lastTimeUsed="1970-01-01 07:00:00" appLaunchCount=0
'''
DISK = '''Package Names: ["com.never","com.old.game"]
App Sizes: [1000,2000]
App Data Sizes: [10,20]
Cache Sizes: [5,5]
'''
NOW = datetime(2026, 11, 3, 12, 0, 0)


def app(name, **kw):
    kw.setdefault("path", "/data/app/x/base.apk")
    kw.setdefault("has_launcher", True)
    kw.setdefault("first_install", "2025-01-01 00:00:00")
    return Package(name, **kw)


class UsageTests(unittest.TestCase):
    def test_parse_usagestats_merges_intervals(self):
        stats, window = parse_usagestats(USAGE)
        self.assertEqual(stats["com.whatsapp"]["last_used"], "2026-11-03 11:59:49")
        self.assertEqual(stats["com.whatsapp"]["launches"], 713)
        self.assertEqual(stats["com.never"]["last_used"], "")
        self.assertTrue(window.startswith("20/9/2026"))

    def test_duration(self):
        self.assertEqual(_duration_seconds("02.25"), 145)
        self.assertEqual(_duration_seconds("1.00.00"), 3600)

    def test_diskstats(self):
        self.assertEqual(parse_diskstats(DISK), {"com.never": 1010, "com.old.game": 2020})

    def test_select_unused(self):
        stats, _ = parse_usagestats(USAGE)
        pkgs = {p.name: p for p in [
            app("com.whatsapp"), app("com.old.game"), app("com.never"),
            app("com.fresh", first_install="2026-11-01 00:00:00"),       # installed 2 days ago
            app("com.service", has_launcher=False),                      # not a user-facing app
            app("com.android.settings", system=True),                    # protected
            app("com.sys.tool", system=True),
        ]}
        got = select_unused(pkgs, stats, parse_diskstats(DISK), 30, False, NOW)
        self.assertEqual([u.pkg for u in got], ["com.never", "com.old.game"])
        self.assertIsNone(got[0].days_idle)
        self.assertEqual(got[1].days_idle, 36)
        with_sys = select_unused(pkgs, stats, {}, 30, True, NOW)
        self.assertIn("com.sys.tool", [u.pkg for u in with_sys])
        self.assertNotIn("com.android.settings", [u.pkg for u in with_sys])

    def test_parse_selection(self):
        self.assertEqual(parse_selection("1,3,5-6", 6), [0, 2, 4, 5])
        self.assertEqual(parse_selection("all", 3), [0, 1, 2])
        with self.assertRaises(ValueError):
            parse_selection("7", 6)


if __name__ == "__main__":
    unittest.main()
