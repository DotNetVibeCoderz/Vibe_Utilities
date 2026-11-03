# 09 - Restore & keamanan

## Journal perubahan

Setiap perubahan aplikasi atau setting dicatat di `journal\<serial>.jsonl`, satu baris per perubahan. Setiap baris menyimpan langkah pembatalannya sendiri.

```
ac restore            # atau: ac restore --list
```
```
== Change journal ==============================================
  #0   2026-11-03 12:25:11  setting         global/private_dns_specifier   restored  null -> dns.adguard-dns.com
  #2   2026-11-03 12:25:27  uninstall-user  com.miui.bugreport             active
```

`active` berarti perubahannya masih berlaku. `restored` berarti sudah dibatalkan.

## Membatalkan

```
ac restore --all                     # semua yang masih active, dari yang terbaru
ac restore com.miui.msa.global       # semua perubahan untuk paket ini
ac restore #5 #6                     # berdasarkan nomor
ac restore -n --all                  # simulasi
```

| Aksi di journal | Dibuat oleh | Cara dibatalkan |
|---|---|---|
| `disable` | `disable`, `debloat` | `pm enable --user 0` |
| `uninstall-user` | `uninstall`, `debloat`, `unused` (preload/sistem), fallback disable di HyperOS | `cmd package install-existing --user 0` |
| `uninstall` | `quarantine`, `unused` (aplikasi user) | `adb install` dari backup APK di `quarantine\` (data tidak kembali) |
| `appop` | `ads overlay`, `quarantine` | Mode app-op dikembalikan ke nilai sebelumnya |
| `setting` | `ads dns`, `quarantine` | Nilai setting sebelumnya dipasang lagi (atau dihapus jika sebelumnya kosong) |
| `revoke` | (API internal) | `pm grant` |

## Hal yang tidak bisa dibatalkan

- **Data aplikasi user yang di-uninstall.** Backup hanya berisi APK.
- **File yang dihapus `clean`.** Isinya hanya cache dan file sementara yang dibuat ulang otomatis.
- Aplikasi user yang di-uninstall dengan `--no-backup`.

## Jangan hapus folder ini

| Folder | Isi |
|---|---|
| `journal\` | Catatan pembatalan. Tanpa folder ini, `restore` tidak tahu apa yang harus dikembalikan |
| `quarantine\` | Backup APK. Boleh dihapus jika Anda yakin tidak perlu menginstal ulang |
| `reports\` | Laporan scan/checkup. Aman dihapus |

Journal disimpan per serial HP, sehingga beberapa HP bisa dikelola dari satu PC.

## Jika HP bermasalah setelah debloat

1. Colok ke PC, lalu jalankan `ac restore --all`.
2. Jika HP tidak bisa masuk ke layar utama (sangat jarang dengan level recommended), ADB tetap bisa dipakai selama USB debugging aktif. Jalankan perintah yang sama.
3. Pilihan terakhir: factory reset. Semua aplikasi bawaan kembali karena tidak ada yang dihapus dari partisi sistem.
