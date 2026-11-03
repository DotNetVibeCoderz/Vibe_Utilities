# 02 - Cek semua (satu perintah)

## Cara menjalankan

- **Double-click `cek-semua.cmd`** dari Windows Explorer. Jendela tetap terbuka sampai Anda menekan tombol.
- Atau dari terminal: `cek-semua.cmd`, `ac checkup`, atau `ac checkup --days 60`.

`cek-semua.cmd` secara otomatis:
1. Mengecek Python, dan mengunduh ADB kalau belum ada.
2. Menampilkan HP yang tersambung (`ac devices`).
3. Menjalankan `ac checkup`.

**Checkup hanya membaca. Tidak ada yang diubah di HP.**

## Yang diperiksa

| # | Bagian | Isi | Kapan diberi tanda |
|---|---|---|---|
| 1 | Perangkat | Merek, model, Android, patch keamanan, penyimpanan, RAM, jumlah aplikasi | Patch keamanan > 180 hari, atau penyimpanan ≥ 85% |
| 2 | Malware / adware | Scan heuristik semua aplikasi ([detail](04-malware.md)), status Play Protect, device admin, accessibility | Ada aplikasi risiko tinggi (BAHAYA) atau sedang (PERHATIAN), atau Play Protect mati |
| 3 | Pemblokir iklan | Private DNS dan apakah server sudah tersambung; aplikasi yang boleh tampil di atas aplikasi lain | Private DNS belum aktif atau belum tersambung |
| 4 | Bloatware | Bloatware sesuai merek yang masih aktif | Masih ada bloatware level *recommended* |
| 5 | Aplikasi tidak dipakai | Aplikasi yang tidak dibuka > N hari (default 30), dengan ukurannya | Ada aplikasi yang tidak dipakai |
| 6 | Sampah & cache | Cache aplikasi, thumbnail, log, file sementara, APK sisa | Total > 200 MB |

## Membaca hasil

Setiap bagian diberi status:

- `OK`: tidak perlu tindakan.
- `!! PERHATIAN`: ada yang bisa dirapikan.
- `XX BAHAYA`: ada aplikasi berisiko tinggi, atau Play Protect mati. Dalam kondisi ini exit code perintah = 1.

Di akhir ada **REKOMENDASI** bernomor. Setiap baris berisi perintah siap pakai beserta alasannya, misalnya:

```
== REKOMENDASI ===============================================
  1. ac unused                              # pilih aplikasi yang mau dihapus dari daftar bernomor
  2. ac clean                               # bersihkan cache & sampah
```

## Laporan tersimpan

Setiap checkup menyimpan laporan Markdown di `reports\checkup-<serial>-<waktu>.md`, berisi tabel status, detail per bagian, dan rekomendasi. Simpan laporan ini kalau ingin membandingkan kondisi HP dari waktu ke waktu.

## Catatan

- **Cache internal** diambil dari snapshot `dumpsys diskstats`, yang diperbarui sistem sekitar sekali sehari. Jika `ac clean` dijalankan kurang dari 24 jam sebelumnya, checkup menampilkan "sudah dibersihkan" dan tidak memakai angka snapshot yang sudah usang.
- **Aplikasi tidak dipakai** bergantung pada riwayat pemakaian yang disimpan Android, biasanya hanya beberapa minggu sampai beberapa bulan. Rentang waktunya ditampilkan di laporan.
