# 05 - Blokir iklan

Iklan di HP Android datang dari tiga sumber, dan masing-masing ditangani dengan cara berbeda:

| Sumber | Contoh | Penanganan |
|---|---|---|
| Jaringan iklan di aplikasi & browser | Banner, video iklan, tracker | **Private DNS** pemblokir iklan (`ac ads dns`) |
| Sistem/aplikasi bawaan pabrik | MSA Xiaomi, lock screen magazine, "rekomendasi aplikasi" | **Debloat** ([06](06-bloatware.md)) |
| Adware yang terinstal | Iklan pop-up penuh layar di atas aplikasi lain | **Scan + quarantine** ([04](04-malware.md)) dan **cabut izin overlay** |

## Private DNS

Android 9+ mendukung DNS-over-TLS untuk seluruh sistem, baik di Wi-Fi maupun data seluler. Dengan memakai server DNS yang memfilter domain iklan, iklan terblokir di semua aplikasi tanpa root dan tanpa VPN.

```
ac ads status                 # status sekarang + daftar server
ac ads dns adguard            # aktifkan
ac ads dns off                # kembalikan ke otomatis
ac ads dns dns.nextdns.io     # atau hostname DNS-over-TLS lain
```

| Nama | Hostname | Memblokir |
|---|---|---|
| `adguard` (disarankan) | dns.adguard-dns.com | Iklan, tracker, phishing |
| `adguard-family` | family.adguard-dns.com | + konten dewasa, SafeSearch |
| `mullvad` | adblock.dns.mullvad.net | Iklan, tracker |
| `mullvad-all` | all.dns.mullvad.net | Iklan, tracker, malware, dewasa, judi |
| `controld` | p2.freedns.controld.com | Iklan, tracker, malware |
| `cloudflare-security` | security.cloudflare-dns.com | Hanya malware (tidak memblokir iklan) |
| `quad9` | dns.quad9.net | Hanya malware (tidak memblokir iklan) |

`ac ads status` juga menampilkan **"Test lookup of doubleclick.net"**: domain iklan diuji langsung dari HP, dan harus bernilai `blocked`. Selain itu ada **"Resolver validated by phone"**, yaitu alamat server yang benar-benar berhasil dihubungi HP. Di Android 10+ yang ditampilkan adalah alamat IP server; di Android 9 yang ditampilkan nama host-nya, karena Android 9 tidak mencatat alamat IP-nya. Jika tertulis `NO`, HP tidak bisa memakai server tersebut dan internet bisa terganggu. Ganti server lain atau jalankan `ac ads dns off`.

**Keterbatasan:**
- Iklan YouTube dan iklan yang disajikan dari domain yang sama dengan kontennya tidak bisa diblokir lewat DNS.
- Beberapa aplikasi (misalnya link iklan di aplikasi belanja, atau tracking link email) bisa rusak. Matikan sementara dengan `ac ads dns off`.
- Jika jaringan kantor/sekolah memblokir port 853 (DNS-over-TLS), HP akan kehilangan koneksi internet di jaringan itu. Gunakan `ac ads dns off`.

## Iklan pop-up (overlay)

Adware sering memakai izin **"Display over other apps"** (`SYSTEM_ALERT_WINDOW`) untuk menampilkan iklan di atas aplikasi lain atau di layar utama.

```
ac ads status                         # daftar aplikasi user yang punya izin overlay
ac ads overlay com.contoh.aplikasi    # cabut izinnya
```

Pencabutan dicatat di journal. Aplikasi seperti chat head Messenger, bubble, atau perekam layar memang butuh overlay; kembalikan izinnya dengan `ac restore <paket>` jika fiturnya berhenti bekerja.

## Iklan sistem dari pabrik

Untuk Xiaomi/Redmi/POCO, debloat level recommended menghapus `com.miui.msa.global` (layanan iklan MIUI), `com.miui.analytics`, `com.miui.daemon`, GetApps, App Vault, dan Wallpaper Carousel. Merek lain punya daftar masing-masing. Lihat [06 - Bloatware](06-bloatware.md).

Untuk Xiaomi, matikan juga secara manual di HP: **Settings → Passwords & security → Authorization & revocation → msa** (jika masih ada), serta opsi *Receive recommendations* di aplikasi File Manager, Music, Security, dan Downloads.
