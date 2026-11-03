# AndroidCleaner

Tools untuk membersihkan HP Android dari **iklan, malware, bloatware, aplikasi yang tidak dipakai, dan file sampah/cache**. Dijalankan dari PC Windows lewat USB (ADB). Tidak perlu root, tidak perlu memasang aplikasi di HP, dan hanya butuh Python 3.10+ (standard library).

## Mulai cepat

1. Aktifkan **USB debugging** di HP. Untuk Xiaomi, aktifkan juga **USB debugging (Security settings)**. Colok ke PC, buka kunci layar, lalu tekan **Allow**.
2. **Double-click `cek-semua.cmd`.** ADB akan diunduh otomatis jika belum ada, lalu HP diperiksa menyeluruh *tanpa mengubah apa pun*.
3. Jalankan perintah di daftar **REKOMENDASI** pada akhir laporan, misalnya:
   ```
   ac debloat --apply        # hapus iklan & telemetry bawaan pabrik
   ac ads dns adguard        # blokir iklan di semua aplikasi
   ac unused                 # pilih aplikasi jarang dipakai untuk dihapus
   ac clean                  # bersihkan cache & sampah
   ```
4. Ada yang tidak beres? `ac restore --all` membatalkan semua perubahan.

## Fitur

| Perintah | Fungsi |
|---|---|
| `ac checkup` / `cek-semua.cmd` | Cek semua (read-only), beri status per bagian dan rekomendasi perintah, simpan laporan Markdown |
| `ac scan` | Scan heuristik malware/adware: device admin, accessibility, overlay, SMS, aplikasi tersembunyi/menyamar, sideload. Opsional VirusTotal |
| `ac quarantine <pkg>` | Cabut akses berbahaya, backup APK, uninstall |
| `ac debloat` | Hapus bloatware per merek (Xiaomi, Samsung, OPPO/realme, vivo, Infinix/TECNO, Huawei + generic) |
| `ac ads dns adguard` | Blokir iklan seluruh sistem lewat Private DNS (AdGuard, Mullvad, Control D, dll.) |
| `ac ads overlay <pkg>` | Cabut izin iklan pop-up |
| `ac unused` | Daftar aplikasi yang tidak/jarang dipakai beserta ukurannya; pilih nomor untuk dihapus |
| `ac clean` | Hapus cache aplikasi, thumbnail, log, file sementara, dan (opsional) APK sisa |
| `ac restore` | Lihat atau batalkan setiap perubahan |
| `ac info`, `ac apps`, `ac disable/enable/uninstall` | Info dan kelola paket manual |

Opsi global: `-n` dry-run (simulasi), `-y` tanpa konfirmasi, `-s SERIAL`, `-v` verbose.

## Dokumentasi

Dokumentasi lengkap ada di **[docs/](docs/README.md)**: persiapan per merek HP, cara membaca hasil checkup, referensi semua perintah, cara kerja scan malware, pilihan server DNS, daftar bloatware, aplikasi tidak terpakai, apa yang dihapus `clean`, restore, troubleshooting, dan panduan pengembangan.

## Keamanan

- Perintah pemeriksaan hanya membaca. `debloat` cuma menampilkan preview tanpa `--apply`.
- Setiap perubahan dicatat di `journal\` dan bisa dibatalkan dengan `ac restore`.
- Bloatware dinonaktifkan atau dihapus untuk user utama saja. APK di partisi sistem tidak disentuh, jadi factory reset selalu mengembalikannya.
- `clean` tidak menyentuh foto, video, dokumen, maupun chat.
- Uninstall aplikasi user menghapus datanya. Hanya APK-nya yang di-backup ke `quarantine\`.

## Test

```
python -m unittest discover -s tests -v
```
