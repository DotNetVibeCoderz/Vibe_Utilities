# 08 - Bersihkan sampah & cache

```
ac clean -n                     # lihat dulu apa yang akan dihapus
ac clean                        # bersihkan
ac clean --apks --empty-dirs    # + file installer APK + folder kosong
ac clean --large 500            # + tampilkan file > 500 MB untuk ditinjau sendiri
```

## Urutan kerja

1. **Cache aplikasi di penyimpanan bersama**: `/sdcard/Android/data/*/cache/*`. Sepuluh folder terbesar ditampilkan beserta ukurannya.
2. **Cache aplikasi internal**: `pm trim-caches`. Sistem diminta mengosongkan cache semua aplikasi. Langkah ini juga ikut mengosongkan cache di penyimpanan bersama, karena itu langkah 1 dijalankan lebih dulu agar ukurannya masih bisa dihitung.
3. **Folder sampah**, hanya isinya:
   - `DCIM/.thumbnails`, `Pictures/.thumbnails`, `Movies/.thumbnails`, `Music/.thumbnails`
   - `LOST.DIR`, `.Trash`
   - `MIUI/debug_log`, `MIUI/.cache`, `log`, `logs`, `tencent/msflogs`
   - `Android/media/com.whatsapp/WhatsApp/.Shared` (salinan sementara saat berbagi media dari WhatsApp)
   - `/data/local/tmp`
4. **File sementara** hingga kedalaman 4 folder: `*.tmp`, `*.temp`, `*.log`, `thumbdata*` (tidak termasuk `Android/obb`).
5. **Opsional:** installer APK (`--apks`), folder kosong (`--empty-dirs`), proses latar belakang (`--kill-background`).

Di akhir ditampilkan ruang kosong sebelum → sesudah. Waktu pembersihan dicatat di `journal\<serial>.lastclean` untuk `checkup`.

## Yang TIDAK dihapus

- Foto, video, musik, dokumen, dan file unduhan selain APK.
- Chat, media WhatsApp/Telegram (selain `.Shared`), dan database aplikasi.
- Data login aplikasi. Cache hanya berisi file yang bisa diunduh atau dibuat ulang.
- `Android/obb` (data game).

## Setelah dibersihkan

- Galeri akan membuat ulang thumbnail, jadi sesaat terasa lebih lambat.
- Aplikasi akan mengunduh ulang sebagian cache (gambar, peta offline sementara). Ruang yang terpakai akan naik lagi sedikit seiring pemakaian. Ini normal.
- Hasil di HP uji (Xiaomi 2409FPCC4G): 179.5 GB → 187.5 GB kosong (**+8.0 GB**), sebagian besar dari cache internal.

## Catatan teknis

- Di Android 11+, folder `Android/data` milik aplikasi lain tidak bisa diakses aplikasi biasa, tetapi masih bisa diakses lewat ADB shell (sudah diuji di Android 14). Jika di HP Anda aksesnya ditolak, langkah 1 dilewati tanpa error dan `pm trim-caches` tetap membersihkan cache tersebut.
- `pm trim-caches` tidak menampilkan ukuran yang dibersihkan. Lihat selisih ruang kosong di akhir.
