# Lega - pembersih drive C untuk Windows 11

WinForms, .NET 10. Memindai lalu membersihkan berkas yang aman dihapus supaya ruang bebas di drive C naik.

## Menjalankan

```powershell
cd Lega
dotnet run
```

Build rilis (satu file exe):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Hasilnya ada di `bin\Release\net10.0-windows\win-x64\publish\Lega.exe`. Butuh .NET 10 Desktop Runtime di PC target.
Tambahkan `--self-contained true` jika ingin exe yang berdiri sendiri (ukuran lebih besar).

## Yang dibersihkan

| Kategori | Perkiraan | Risiko | Admin |
|---|---|---|---|
| Instalasi Windows sebelumnya (Windows.old) | 10-30 GB | Hati-hati | ya |
| File hibernasi (hiberfil.sys) | 40% RAM | Tinjau dulu | ya |
| Cache Windows Update | 1-10 GB | Aman | ya |
| Sisa komponen Windows (WinSxS, via DISM) | 1-5 GB | Aman | ya |
| Recycle Bin | bervariasi | Tinjau dulu | tidak |
| Cache browser (Chrome, Edge, Brave, Firefox) | 0.5-5 GB | Aman | tidak |
| Berkas sementara pengguna dan Windows | 0.2-10 GB | Aman | sebagian |
| Cache Delivery Optimization | 0.1-5 GB | Aman | ya |
| Laporan error dan crash dump | 0.1-5 GB | Aman | sebagian |
| Cache thumbnail dan ikon | kecil | Aman | tidak |
| Cache NuGet dan npm | 1-10 GB | Tinjau dulu | tidak |

Kategori yang tidak ada di PC (misalnya tidak ada Windows.old) otomatis disembunyikan.

## Cara kerja

- Saat dibuka, Lega memindai semua kategori secara paralel (maksimal 3 sekaligus). Belum ada yang dihapus.
- Hanya item berisiko "Aman" yang dicentang otomatis. Item "Tinjau dulu" dan "Hati-hati" harus Anda centang sendiri,
  dan dialog konfirmasi menjelaskan dampaknya.
- Item yang butuh administrator tetap tampil dengan ukurannya. Tombol di kanan atas menjalankan ulang aplikasi
  dengan UAC dan menunjukkan berapa GB tambahan yang terbuka.
- Setelah selesai, tiap kategori dipindai ulang dan angka "kembali ke drive C" diambil dari selisih ruang bebas
  yang sebenarnya, bukan dari perkiraan.

## Pengaman

- Tidak pernah menyentuh root drive, dan tidak mengikuti symlink atau junction.
- Berkas yang sedang dipakai dilewati, bukan dipaksa.
- Cache browser hanya folder cache. Password, cookie, dan riwayat tidak disentuh.
- Layanan Windows Update dihentikan sementara saat membersihkan cache-nya, lalu dinyalakan lagi.

## Struktur

```
Core/Sys.cs        admin, proses, P/Invoke Recycle Bin, util berkas, format ukuran
Core/Cleaners.cs   kategori dan katalognya (tambah kategori baru di Catalog.Build)
UI/Theme.cs        palet dan font (Segoe UI Variable, fallback Segoe UI)
UI/StorageBar.cs   bar penyimpanan beranimasi
UI/CategoryRow.cs  baris kategori (custom paint, keyboard, dan akses Narrator)
UI/ActionButton.cs tombol dan garis aktivitas
UI/MainForm.cs     layout, alur pindai dan bersihkan
```
