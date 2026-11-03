# 03 - Referensi perintah

Format: `ac [opsi global] <perintah> [opsi]`. Opsi global boleh diletakkan sebelum atau sesudah nama perintah.

## Opsi global

| Opsi | Fungsi |
|---|---|
| `-s SERIAL`, `--serial` | Pilih HP jika lebih dari satu tersambung |
| `-n`, `--dry-run` | Tampilkan perintah ADB yang akan dijalankan, tanpa mengubah apa pun |
| `-y`, `--yes` | Jangan minta konfirmasi |
| `-v`, `--verbose` | Cetak setiap perintah ADB; di `scan`, tampilkan juga temuan LOW |

## Ringkasan

| Perintah | Mengubah HP? | Fungsi |
|---|---|---|
| `devices` | Tidak | Daftar HP tersambung dan statusnya |
| `info` | Tidak | Info perangkat, penyimpanan, RAM, jumlah aplikasi |
| `checkup` | Tidak | Cek semua + rekomendasi + laporan Markdown |
| `scan` | Tidak | Scan malware/adware |
| `quarantine` | Ya | Cabut akses aplikasi berbahaya, backup, uninstall |
| `apps` | Tidak | Daftar paket |
| `disable` / `enable` / `uninstall` | Ya | Kelola paket manual |
| `debloat` | Hanya dengan `--apply` | Hapus bloatware sesuai merek |
| `unused` | Hanya jika Anda memilih | Aplikasi yang jarang dipakai |
| `clean` | Ya | Hapus cache & sampah |
| `ads` | `dns`/`overlay`: ya | Blokir iklan |
| `restore` | Ya (kecuali `--list`) | Batalkan perubahan |

---

### `devices`
```
ac devices
```
Status yang mungkin muncul: `device` (siap), `unauthorized` (terima dialog izin di HP), `offline` (cabut lalu colok ulang kabel).

### `info`
```
ac info
```

### `checkup`
```
ac checkup [--days N]
```
`--days`: batas hari untuk dianggap "tidak dipakai" (default 30). Lihat [02 - Cek semua](02-cek-semua.md).

### `scan`
```
ac scan [--all] [--virustotal] [-v]
```
| Opsi | Fungsi |
|---|---|
| `--all` | Tampilkan semua aplikasi, termasuk yang bersih dan aplikasi sistem |
| `--virustotal` | Hitung SHA-256 APK yang mencurigakan dan cek ke VirusTotal (butuh variabel `VT_API_KEY`) |
| `-v` | Tampilkan juga temuan risiko rendah |

Laporan JSON tersimpan di `reports\scan-*.json`. Lihat [04 - Malware](04-malware.md).

### `quarantine`
```
ac quarantine <paket> [<paket> ...] [--no-backup]
```
Untuk setiap paket: hentikan aplikasi → cabut accessibility & akses notifikasi → tolak izin overlay → lepas device admin → backup APK ke `quarantine\` → uninstall. Jika uninstall gagal, aplikasi di-disable. Untuk aplikasi preload, APK tidak di-backup karena tetap ada di partisi sistem.

### `apps`
```
ac apps [--system] [--all]
```
Kolom tanda: `U` aplikasi user, `S` sistem, `disabled`, `hidden` (tanpa ikon launcher), `removed` (dihapus untuk user ini). Kolom terakhir menunjukkan installer.

### `disable`, `enable`, `uninstall`
```
ac disable <paket> ...      # pm disable-user; di HyperOS/MIUI otomatis jadi uninstall --user 0
ac enable <paket> ...
ac uninstall <paket> ...    # pm uninstall -k --user 0 (APK sistem tetap ada, bisa di-restore)
```

### `debloat`
```
ac debloat [--level recommended|advanced] [--mode disable|uninstall] [--brand MEREK] [--apply]
```
Tanpa `--apply`, perintah ini hanya menampilkan preview. Lihat [06 - Bloatware](06-bloatware.md).

### `unused`
```
ac unused [--days N] [--system] [--select 1,3,5-7] [--no-backup] [--force]
```
Lihat [07 - Aplikasi tidak terpakai](07-aplikasi-tidak-terpakai.md).

### `clean`
```
ac clean [--apks] [--empty-dirs] [--kill-background] [--large MB]
```
| Opsi | Fungsi |
|---|---|
| `--apks` | Hapus juga file installer `.apk`/`.xapk`/`.apks` di luar folder `Android\` |
| `--empty-dirs` | Hapus folder kosong di penyimpanan bersama |
| `--kill-background` | Hentikan proses latar belakang (`am kill-all`) |
| `--large MB` | Tampilkan file yang lebih besar dari MB, untuk ditinjau sendiri (tidak dihapus) |

Lihat [08 - Pembersihan](08-pembersihan.md).

### `ads`
```
ac ads status
ac ads dns <adguard|adguard-family|mullvad|mullvad-all|controld|cloudflare-security|quad9|hostname-lain|off>
ac ads overlay <paket> ...
```
Lihat [05 - Blokir iklan](05-iklan.md).

### `restore`
```
ac restore                 # sama dengan --list
ac restore --all           # batalkan semua perubahan yang masih aktif
ac restore com.miui.msa.global
ac restore #3 #4           # berdasarkan nomor di journal
```
Lihat [09 - Restore](09-restore.md).

## Exit code

`0` sukses · `1` ada aksi yang gagal, dibatalkan, atau checkup menemukan BAHAYA · `2` masalah ADB atau HP tidak tersambung · `130` dihentikan dengan Ctrl+C.
