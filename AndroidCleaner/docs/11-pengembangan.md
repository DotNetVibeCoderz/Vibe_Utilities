# 11 - Pengembangan

Python 3.10+, hanya standard library. Tidak ada build step.

## Struktur

```
androidcleaner/
  adb.py          cari adb, pilih HP, jalankan perintah
  packages.py     inventaris paket (pm list, query-activities, dumpsys package) + PackageActions
  journal.py      catatan perubahan + langkah undo per entri
  scanner.py      skor risiko malware/adware
  adblock.py      Private DNS + overlay
  debloat.py      pemilihan kandidat dari data/debloat.json
  usage.py        aplikasi tidak terpakai (usagestats + diskstats)
  cleaner.py      cache & sampah
  checkup.py      cek semua + laporan Markdown
  cli.py          argparse, satu fungsi cmd_* per perintah
tests/            unit test offline (tanpa HP)
scripts/          install_platform_tools.ps1
ac.cmd            shortcut: python -m androidcleaner
cek-semua.cmd     satu klik: siapkan adb + checkup
```

## Aturan penting

- **Semua perubahan ke HP harus lewat `PackageActions`** (`packages.py`), dan setiap perubahan harus memanggil `journal.record(...)` dengan langkah `undo`. Hanya `cleaner.py` yang menghapus file tanpa journal, karena isinya cache.
- `Adb.shell()` tidak melempar error ketika perintah gagal. Hasilnya berisi stdout dan stderr, jadi deteksi kegagalan dengan mencari `Failure|Exception|Error` (lihat `PackageActions._exec`).
- Hormati `dry_run`: setiap perintah yang mengubah HP harus mencetak `[dry-run] ...` alih-alih menjalankannya.
- Opsi global di `cli.py` memakai parent parser dengan `argument_default=SUPPRESS`, dan nilai default diisi di `main()`. Jangan pakai `set_defaults` di parser utama: objek action dibagi bersama dengan subparser, sehingga flag yang diberikan sebelum nama perintah akan tertimpa.
- Output parser ADB berbeda-beda antar versi Android dan ROM. Saat menambah parser, simpan contoh output asli sebagai fixture di `tests/`.

## Test

```
python -m unittest discover -s tests -v
python -m unittest tests.test_usage.UsageTests.test_select_unused
```

Test berjalan tanpa HP, memakai potongan output ADB asli dari HP uji. Untuk uji di perangkat, gunakan perintah read-only atau `-n` terlebih dulu. Untuk aksi yang mengubah HP, uji bolak-balik: ubah → cek di HP → `ac restore` → cek lagi.

## Perilaku perangkat yang sudah diketahui (Xiaomi HyperOS, Android 14)

- `pm disable-user` ditolak untuk paket sistem → fallback ke `pm uninstall -k --user 0`.
- `dumpsys package packages` tidak memuat `requested permissions:`. Hanya izin yang di-*grant* yang tersedia secara massal.
- ADB shell bisa membaca dan menghapus `/sdcard/Android/data/*/cache`.
- `dumpsys usagestats` hanya menyimpan riwayat beberapa minggu. `timeRange` mengikuti locale HP, jadi ditampilkan apa adanya.
- `dumpsys diskstats` adalah snapshot harian, bukan kondisi saat ini.

## Ide pengembangan

- Daftar debloat untuk merek lain (Motorola, Nokia, ASUS, Sony).
- Ekspor laporan checkup ke HTML.
- Dukungan ADB over Wi-Fi (`adb pair` / `adb connect`), yang sudah bisa dipakai lewat `-s IP:PORT`.
