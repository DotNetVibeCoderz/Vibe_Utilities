# 06 - Bloatware

```
ac debloat                                  # preview level recommended
ac debloat --level advanced                 # preview recommended + advanced
ac debloat --apply                          # terapkan level recommended
ac debloat --level advanced --apply -y      # terapkan semua tanpa konfirmasi
```

## Level

| Level | Isi | Risiko |
|---|---|---|
| `recommended` | Layanan iklan, telemetry/analytics, toko promo, feed promosi, installer Facebook/Amazon bawaan | Aman untuk hampir semua orang |
| `advanced` | Aplikasi bawaan yang mungkin masih Anda pakai: YouTube, Mi Video, Mi Music, cuaca, Game Launcher, Bixby, dll. | Fitur yang bersangkutan hilang |

`--level advanced` mencakup recommended + advanced. Daftar preview menandai paket yang akan diproses dengan `*`. Paket yang sudah disabled atau dihapus tidak diproses lagi.

## Merek yang didukung

Merek dideteksi otomatis dari `ro.product.manufacturer` dan `ro.product.brand`. Daftar `generic` (Facebook preinstall, Netflix activation, aplikasi Google/Microsoft bawaan, dll.) selalu ikut diperiksa.

| Daftar | Merek |
|---|---|
| `xiaomi` | Xiaomi, Redmi, POCO |
| `samsung` | Samsung |
| `oppo` | OPPO, realme, OnePlus |
| `vivo` | vivo, iQOO |
| `transsion` | Infinix, TECNO, itel |
| `huawei` | Huawei, Honor |

Pakai `--brand xiaomi` (dan seterusnya) untuk memaksa daftar tertentu.

## Mode

| Mode | Perintah ADB | Efek | Cara restore |
|---|---|---|---|
| `disable` (default) | `pm disable-user --user 0` | Aplikasi mati, ikon hilang, APK dan data tetap ada | `pm enable` |
| `uninstall` | `pm uninstall -k --user 0` | Dihapus untuk user utama; APK tetap di partisi sistem | `cmd package install-existing` |

**Xiaomi HyperOS/MIUI** menolak `disable-user` untuk aplikasi sistem (`SecurityException: Cannot disable system packages`). Tools otomatis beralih ke mode uninstall untuk paket tersebut dan menampilkan `ROM blocks disable; removed for user 0 instead`. Hasilnya tetap bisa di-restore.

Tidak ada mode yang menghapus aplikasi dari partisi sistem, karena itu butuh root. Factory reset akan mengembalikan semua aplikasi bawaan.

## Efek samping yang perlu diketahui (Xiaomi)

- Menghapus **GetApps** (`com.xiaomi.mipicks`) menghentikan update otomatis aplikasi Xiaomi yang diperbarui lewat GetApps, seperti Clock, Notes, dan Cleaner. Aplikasi tersebut tetap bisa dipakai.
- Menghapus **App Vault** (`com.mi.globalminusscreen`) menghilangkan layar kiri di launcher MIUI.
- Menghapus **Facebook App Manager/Installer** tidak mengganggu aplikasi Facebook/Instagram yang diinstal dari Play Store.

## Menambah atau mengubah daftar

Edit `androidcleaner/data/debloat.json`:

```json
{"pkg": "com.contoh.bloat", "level": "recommended", "desc": "Penjelasan singkat"}
```

- Tambahkan ke `lists.<merek>`. Merek baru juga perlu didaftarkan di `brands` beserta kata kunci manufacturer/brand-nya.
- Satu paket hanya boleh muncul satu kali di seluruh file. Test `DebloatTests.test_list_is_well_formed` akan gagal jika ada duplikat atau level yang salah.
- Masukkan paket ke `recommended` hanya jika menghapusnya tidak merusak fungsi inti HP. Jika ragu, gunakan `advanced`.
- Jangan pernah memasukkan launcher, SystemUI, Settings, telepon/SMS, keyboard satu-satunya, Play Services, atau komponen `android`/`com.android.providers.*`.
