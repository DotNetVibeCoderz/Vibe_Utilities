# 07 - Aplikasi yang tidak / jarang dipakai

```
ac unused                       # tidak dibuka > 30 hari, lalu pilih yang mau dihapus
ac unused --days 60
ac unused --system              # termasuk aplikasi sistem yang punya ikon
ac unused --select 1,3,5-7      # langsung hapus nomor tertentu, tanpa prompt
ac unused -n --select all       # simulasi: lihat apa yang akan terjadi
```

## Contoh tampilan

```
  Usage history available on the phone: 20/9/2026, 11.01 – 3/11/2026, 12.29
  ('never' = not opened anywhere in that period)

    #  last used           idle launches       size  type     package
    1  never                  -        0     2.0 GB  user     com.axlebolt.standoff2.xiaomi
    2  never                  -        0   384.1 MB  user     com.telkomsel.telkomselcm
    ...
   22  2026-09-25 08:27     39d        1   462.6 MB  user     com.microsoft.emmx

  23 app(s), about 4.3 GB in total (app + data).

  Numbers to remove (e.g. 1,3,5-7 or all; Enter = cancel):
```

| Kolom | Arti |
|---|---|
| last used | Terakhir dibuka di layar depan (`never` = tidak pernah dalam rentang riwayat) |
| idle | Jumlah hari sejak terakhir dibuka |
| launches | Berapa kali dibuka dalam rentang riwayat |
| size | Ukuran APK + data (dari snapshot sistem harian) |
| type | `user` diinstal sendiri, `preload` bawaan pabrik yang bisa dihapus, `system` aplikasi sistem |

Urutan daftar: yang tidak pernah dibuka lebih dulu, lalu yang paling lama idle, lalu yang paling besar.

## Memilih

Ketik nomor dipisah koma, rentang dengan tanda minus, atau `all`. Tekan Enter tanpa isi untuk batal. Tools akan menampilkan daftar yang akan dihapus beserta caranya, lalu meminta konfirmasi `y`.

| Jenis | Cara hapus | Bisa dikembalikan? |
|---|---|---|
| `user` | APK di-backup ke `quarantine\`, lalu uninstall | APK ya (`ac restore <paket>`); **data aplikasi hilang** |
| `preload` / `system` | `pm uninstall -k --user 0` | Ya, lengkap (`ac restore <paket>`) |

Pakai `--no-backup` kalau tidak perlu backup APK, misalnya karena aplikasinya bisa diunduh ulang dari Play Store.

> **Penting:** sebelum menghapus aplikasi `user` seperti game, e-wallet, atau aplikasi bank, pastikan akun/datanya sudah tersimpan di cloud. Uninstall menghapus data lokal aplikasi.

## Apa yang tidak pernah masuk daftar

- Aplikasi tanpa ikon launcher (layanan latar belakang, plugin).
- Aplikasi yang diinstal kurang dari `--days` hari yang lalu.
- Aplikasi yang disabled.
- Aplikasi inti: Settings, Play Store, Play Services, Telepon, Kontak, SMS, Kamera, SystemUI, Security Center, Files, launcher yang sedang aktif, dan lainnya (lihat `PROTECTED` di `androidcleaner/usage.py`).
- Aplikasi sistem, kecuali memakai `--system`.

## Batas akurasi

Data diambil dari `dumpsys usagestats`. Android hanya menyimpan riwayat terbatas: harian beberapa hari, mingguan beberapa minggu, bulanan beberapa bulan, tahunan sejak data tertua yang masih ada. Riwayat juga bisa terhapus setelah update sistem atau reset. **Rentang riwayat yang sebenarnya selalu ditampilkan di bagian atas.** "never" berarti tidak dibuka dalam rentang itu, bukan tidak pernah sama sekali.

Aplikasi yang bekerja di latar belakang tanpa pernah dibuka (misalnya widget, aplikasi smartwatch, atau aplikasi yang hanya dipakai lewat share sheet) bisa tampil sebagai "never" padahal masih berguna.
