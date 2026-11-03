# 01 - Persiapan

## Di PC (Windows)

1. **Python 3.10 atau lebih baru.** Unduh dari <https://www.python.org/downloads/> dan centang *Add python.exe to PATH* saat instalasi. Cek dengan `python --version`. Tidak ada paket tambahan (`pip install`) yang dibutuhkan.
2. **ADB (Android platform-tools).** Cukup jalankan:
   ```
   powershell -File scripts\install_platform_tools.ps1
   ```
   ADB resmi dari Google akan diunduh ke `.tools\platform-tools\`. `cek-semua.cmd` juga mengunduhnya otomatis kalau belum ada.

   Urutan pencarian ADB: variabel `ADB` → `.tools\platform-tools` → `ANDROID_HOME`/`ANDROID_SDK_ROOT` → `%LOCALAPPDATA%\Android\Sdk` → `PATH`.
3. **Driver USB.** Windows 10/11 biasanya sudah punya. Kalau HP tidak muncul di `ac devices`, pasang [Google USB Driver](https://developer.android.com/studio/run/win-usb) atau driver dari pabrikan HP.

## Di HP

### 1. Aktifkan Developer options
Buka **Settings → About phone**, lalu ketuk **Build number** 7 kali sampai muncul "You are now a developer".

| Merek | Lokasi Build number |
|---|---|
| Xiaomi / Redmi / POCO | Settings → About phone → **OS version** (MIUI/HyperOS version) |
| Samsung | Settings → About phone → Software information → Build number |
| OPPO / realme | Settings → About device → Version → Build number |
| vivo | Settings → System management → About phone → Software version |

### 2. Aktifkan USB debugging
**Settings → Additional settings / System → Developer options → USB debugging: ON**.

**Khusus Xiaomi/Redmi/POCO:** aktifkan juga **USB debugging (Security settings)**. Opsi ini butuh SIM card dan login Mi Account. Tanpa opsi ini, perubahan setting (misalnya Private DNS) dan izin aplikasi bisa ditolak.

### 3. Hubungkan dan beri izin
1. Colok HP ke PC dengan kabel data (bukan kabel charge saja).
2. Jika HP bertanya mode USB, pilih **File transfer** atau **No data transfer** (keduanya bisa).
3. Buka kunci layar HP. Akan muncul dialog **"Allow USB debugging?"**. Centang **Always allow from this computer**, lalu tekan **Allow**.
4. Cek di PC:
   ```
   ac devices
   ```
   Status harus `device`. Kalau `unauthorized`, dialog izin belum diterima. Lihat [Troubleshooting](10-troubleshooting.md).

## Cara menjalankan perintah

Dari folder proyek (`AndroidCleaner\`), ketiga bentuk berikut sama:

```
ac checkup
python -m androidcleaner checkup
.\ac.cmd checkup          # dari PowerShell
```

Kalau ada beberapa HP tersambung, pilih salah satu dengan `-s SERIAL`. Serial bisa dilihat di `ac devices`.
