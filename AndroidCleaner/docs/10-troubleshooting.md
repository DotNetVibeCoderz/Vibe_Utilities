# 10 - Troubleshooting

### `adb not found`
Jalankan `powershell -File scripts\install_platform_tools.ps1`, atau set variabel `ADB` ke lokasi `adb.exe`.

### Status `unauthorized`
- Buka kunci layar HP. Dialog **Allow USB debugging** hanya muncul saat layar tidak terkunci.
- Dialog tidak muncul? Di Developer options tekan **Revoke USB debugging authorizations**, cabut dan colok ulang kabel, lalu jalankan `ac devices`.
- Masih belum? Matikan lalu nyalakan lagi USB debugging.

### HP tidak muncul sama sekali (`No devices`)
- Pakai kabel data, bukan kabel charge saja, dan coba port USB lain.
- Ubah mode USB di notifikasi HP ke **File transfer**.
- Pasang driver USB (lihat [Persiapan](01-persiapan.md)).

### `Several devices connected; pass --serial`
Lihat serial dengan `ac devices`, lalu tambahkan `-s SERIAL`. Emulator yang sedang berjalan juga terhitung.

### `SecurityException` / `not allowed` saat mengubah setting (Xiaomi)
Aktifkan **Developer options → USB debugging (Security settings)**. Opsi ini butuh SIM card dan login Mi Account.

### `Cannot disable system packages`
Ini normal di HyperOS/MIUI. Tools otomatis memakai uninstall untuk user 0. Lihat [06 - Bloatware](06-bloatware.md#mode).

### Internet tidak jalan setelah `ac ads dns ...`
- `ac ads status`: jika *Resolver validated by phone* = `NO`, server tidak bisa dihubungi dari jaringan Anda.
- Jalankan `ac ads dns off`, atau coba server lain (`ac ads dns mullvad`).
- Jaringan kantor/kampus sering memblokir DNS-over-TLS (port 853).

### Aplikasi/fitur hilang setelah debloat
`ac restore` → cari paket yang relevan → `ac restore <paket>`.

### Device admin menolak dilepas saat `quarantine`
Buka **Settings → Security → Device admin apps** (lokasinya berbeda di tiap merek; cari "device admin" di Settings), nonaktifkan aplikasinya, lalu jalankan `ac quarantine <paket>` lagi.

### `unused` menampilkan aplikasi yang sebenarnya saya pakai
Riwayat pemakaian Android terbatas, dan aplikasi yang hanya bekerja di latar belakang tidak tercatat sebagai "dibuka". Cukup jangan pilih nomornya. Lihat [07](07-aplikasi-tidak-terpakai.md#batas-akurasi).

### Checkup bilang cache internal besar padahal baru dibersihkan
Angka itu snapshot harian dari sistem. Setelah `ac clean`, checkup mengabaikannya selama 24 jam. Jika Anda membersihkan dengan cara lain (dari menu HP), angkanya baru diperbarui sistem keesokan harinya.

### Teks rusak / karakter aneh di terminal
Gunakan Windows Terminal, atau jalankan `chcp 65001` sebelum perintah.

### Melihat perintah ADB yang sebenarnya dijalankan
Tambahkan `-v`. Untuk simulasi tanpa mengubah HP, tambahkan `-n`.
