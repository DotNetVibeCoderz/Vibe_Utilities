# Dokumentasi AndroidCleaner

AndroidCleaner membersihkan HP Android dari iklan, malware, bloatware, aplikasi yang tidak dipakai, dan file sampah/cache. Semuanya dijalankan dari PC Windows lewat kabel USB (ADB), tanpa root dan tanpa memasang aplikasi apa pun di HP.

## Mulai cepat

1. Siapkan HP dan PC: [Persiapan](01-persiapan.md).
2. Double-click **`cek-semua.cmd`** di folder proyek, atau jalankan `ac checkup`.
3. Ikuti daftar **REKOMENDASI** di akhir laporan. Setiap baris berisi perintah yang tinggal dijalankan.
4. Kalau ada yang tidak beres, batalkan dengan `ac restore`.

## Daftar isi

| Dokumen | Isi |
|---|---|
| [01 - Persiapan](01-persiapan.md) | Python, ADB, USB debugging, izin di HP (termasuk Xiaomi/Samsung/OPPO/vivo) |
| [02 - Cek semua](02-cek-semua.md) | `cek-semua.cmd` / `ac checkup`: apa yang diperiksa dan cara membaca laporannya |
| [03 - Referensi perintah](03-perintah.md) | Semua perintah, opsi, dan contoh |
| [04 - Malware & adware](04-malware.md) | Cara kerja scan, arti skor, quarantine, VirusTotal |
| [05 - Blokir iklan](05-iklan.md) | Private DNS, pilihan server, iklan pop-up (overlay) |
| [06 - Bloatware](06-bloatware.md) | Daftar per merek, level, mode disable/uninstall, menambah daftar |
| [07 - Aplikasi tidak terpakai](07-aplikasi-tidak-terpakai.md) | Cara mencari dan memilih aplikasi yang jarang dipakai |
| [08 - Bersihkan sampah & cache](08-pembersihan.md) | Apa saja yang dihapus `clean` dan apa yang tidak |
| [09 - Restore & keamanan](09-restore.md) | Journal perubahan, cara membatalkan, backup APK |
| [10 - Troubleshooting](10-troubleshooting.md) | Masalah umum dan solusinya |
| [11 - Pengembangan](11-pengembangan.md) | Arsitektur kode, test, cara menambah fitur |

## Prinsip keamanan

- **Default aman.** `checkup`, `scan`, `info`, `apps`, `ads status`, dan `restore --list` hanya membaca. `debloat` cuma menampilkan preview sampai diberi `--apply`.
- **Semua bisa dibatalkan.** Setiap perubahan aplikasi atau setting dicatat di `journal\` dan bisa dikembalikan dengan `ac restore`.
- **Dry-run.** Tambahkan `-n` ke perintah apa pun untuk melihat perintah ADB yang akan dijalankan tanpa mengubah HP.
- **Data pribadi tidak disentuh.** Foto, video, dokumen, chat, dan kontak tidak pernah dihapus oleh `clean`.
