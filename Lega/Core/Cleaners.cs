namespace Lega.Core;

public enum Risk { Safe, Review, Careful }

/// <summary>Satu lokasi yang dibersihkan: folder (opsional dengan pola nama) atau satu berkas.</summary>
public readonly record struct Target(string Path, string Pattern = "*", bool Recurse = true);

public abstract class CleanCategory
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public Risk Risk { get; init; } = Risk.Safe;
    public bool RequiresAdmin { get; init; }

    /// <summary>Dampak yang perlu diketahui pengguna. Ditampilkan di dialog konfirmasi.</summary>
    public string? Consequence { get; init; }

    /// <summary>Null berarti ikuti risiko: hanya item aman yang dicentang otomatis.</summary>
    public bool? PreSelect { get; init; }

    public bool DefaultChecked => PreSelect ?? Risk == Risk.Safe;

    public virtual bool SizeKnown => true;
    public virtual bool IsApplicable => true;

    public abstract long Scan(CancellationToken ct);
    public abstract void Clean(CancellationToken ct);
}

public sealed class FolderCategory : CleanCategory
{
    public required Func<IEnumerable<Target>> Targets { get; init; }
    public Action<CancellationToken>? Before { get; init; }
    public Action? After { get; init; }

    public override bool IsApplicable =>
        Targets().Any(t => Directory.Exists(t.Path) || File.Exists(t.Path));

    public override long Scan(CancellationToken ct) =>
        Targets().Sum(t => FileUtil.Measure(t, ct));

    public override void Clean(CancellationToken ct)
    {
        try
        {
            Before?.Invoke(ct);
            foreach (var t in Targets().ToList())
                FileUtil.Delete(t, ct);
        }
        finally
        {
            After?.Invoke();
        }
    }
}

public sealed class RecycleBinCategory : CleanCategory
{
    public override long Scan(CancellationToken ct) => Native.RecycleBinSize(Sys.Root);
    public override void Clean(CancellationToken ct) => Native.EmptyRecycleBin(Sys.Root);
}

public sealed class HibernationCategory : CleanCategory
{
    private static string FilePath => Path.Combine(Sys.Root, "hiberfil.sys");

    public override bool IsApplicable => File.Exists(FilePath);

    public override long Scan(CancellationToken ct)
    {
        try { return new FileInfo(FilePath).Length; } catch { return 0; }
    }

    public override void Clean(CancellationToken ct) =>
        Sys.Run("powercfg.exe", "/hibernate off", ct);
}

public sealed class ComponentStoreCategory : CleanCategory
{
    public override bool SizeKnown => false;
    public override long Scan(CancellationToken ct) => 0;

    public override void Clean(CancellationToken ct) =>
        Sys.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", ct);
}

public sealed class WindowsOldCategory : CleanCategory
{
    private static string Dir => Path.Combine(Sys.Root, "Windows.old");

    public override bool IsApplicable => Directory.Exists(Dir);

    public override long Scan(CancellationToken ct) =>
        FileUtil.Measure(new Target(Dir), ct);

    public override void Clean(CancellationToken ct)
    {
        // Windows.old dimiliki TrustedInstaller; ambil alih dulu, lalu hapus.
        Sys.Run("takeown.exe", $"/F \"{Dir}\" /R /A /D Y", ct);
        Sys.Run("icacls.exe", $"\"{Dir}\" /grant *S-1-5-32-544:F /T /C /Q", ct);
        Sys.Run("cmd.exe", $"/c rd /s /q \"{Dir}\"", ct);
    }
}

public static class Catalog
{
    public static List<CleanCategory> Build()
    {
        var all = new List<CleanCategory>
        {
            new WindowsOldCategory
            {
                Title = "Instalasi Windows sebelumnya",
                Description = "Folder Windows.old yang tertinggal setelah pembaruan besar. Biasanya yang terbesar, 10 sampai 30 GB.",
                Risk = Risk.Careful,
                RequiresAdmin = true,
                Consequence = "Anda tidak bisa lagi kembali ke versi Windows sebelumnya.",
            },
            new HibernationCategory
            {
                Title = "File hibernasi",
                Description = "hiberfil.sys menyimpan isi RAM untuk Hibernate. Ukurannya sekitar 40% dari RAM.",
                Risk = Risk.Review,
                RequiresAdmin = true,
                Consequence = "Hibernate dan Fast Startup dimatikan. Aktifkan lagi dengan perintah powercfg /hibernate on.",
            },
            WindowsUpdateCache(),
            new ComponentStoreCategory
            {
                Title = "Sisa komponen Windows (WinSxS)",
                Description = "Versi lama komponen sistem yang sudah digantikan pembaruan. Bisa makan waktu beberapa menit.",
                RequiresAdmin = true,
                PreSelect = false,
            },
            new RecycleBinCategory
            {
                Title = "Recycle Bin",
                Description = "Berkas yang sudah Anda hapus tetapi belum dibuang permanen.",
                Risk = Risk.Review,
                Consequence = "Isi Recycle Bin tidak bisa dipulihkan lagi.",
            },
            new FolderCategory
            {
                Title = "Cache browser",
                Description = "Chrome, Edge, Brave, dan Firefox. Situs dimuat sedikit lebih lambat sekali saja. Tutup browser dulu.",
                Targets = BrowserCaches,
            },
            new FolderCategory
            {
                Title = "Berkas sementara pengguna",
                Description = "Sisa instalasi dan aplikasi di folder Temp Anda. Aplikasi membuatnya lagi saat perlu.",
                Targets = () => [new Target(Path.GetTempPath())],
            },
            new FolderCategory
            {
                Title = "Berkas sementara Windows",
                Description = "Folder Temp milik sistem, umumnya sisa instalasi dan pembaruan.",
                RequiresAdmin = true,
                Targets = () => [new Target(Path.Combine(Sys.WindowsDir, "Temp"))],
            },
            new FolderCategory
            {
                Title = "Cache Delivery Optimization",
                Description = "Salinan pembaruan yang Windows simpan untuk dibagikan ke PC lain.",
                RequiresAdmin = true,
                Targets = () => [new Target(Path.Combine(Sys.WindowsDir,
                    @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"))],
            },
            new FolderCategory
            {
                Title = "Laporan error dan crash dump",
                Description = "Rekaman aplikasi atau sistem yang crash. Hanya berguna untuk analisis masalah.",
                Targets = () =>
                [
                    new Target(Path.Combine(Sys.LocalAppData, "CrashDumps")),
                    new Target(Path.Combine(Sys.WindowsDir, "Minidump")),
                    new Target(Path.Combine(Sys.WindowsDir, "MEMORY.DMP")),
                    new Target(Path.Combine(Sys.ProgramData, @"Microsoft\Windows\WER\ReportArchive")),
                    new Target(Path.Combine(Sys.ProgramData, @"Microsoft\Windows\WER\ReportQueue")),
                    new Target(Path.Combine(Sys.LocalAppData, @"Microsoft\Windows\WER")),
                ],
            },
            new FolderCategory
            {
                Title = "Cache thumbnail dan ikon",
                Description = "Gambar mini di File Explorer. Dibuat ulang otomatis saat Anda membuka folder.",
                Targets = () =>
                {
                    string dir = Path.Combine(Sys.LocalAppData, @"Microsoft\Windows\Explorer");
                    return [new Target(dir, "thumbcache_*.db", false), new Target(dir, "iconcache_*.db", false)];
                },
            },
            new FolderCategory
            {
                Title = "Cache NuGet dan npm",
                Description = "Paket pengembang yang pernah diunduh, termasuk ~\\.nuget\\packages.",
                Risk = Risk.Review,
                Consequence = "Paket NuGet dan npm diunduh ulang saat build berikutnya.",
                Targets = () =>
                [
                    new Target(Path.Combine(Sys.LocalAppData, @"NuGet\v3-cache")),
                    new Target(Path.Combine(Sys.LocalAppData, @"NuGet\plugins-cache")),
                    new Target(Path.Combine(Sys.LocalAppData, "npm-cache")),
                    new Target(Path.Combine(Sys.Profile, @".nuget\packages")),
                ],
            },
        };

        return all.Where(c => c.IsApplicable).ToList();
    }

    private static CleanCategory WindowsUpdateCache()
    {
        bool stopped = false;
        return new FolderCategory
        {
            Title = "Cache Windows Update",
            Description = "Unduhan pembaruan yang sudah terpasang. Windows mengunduh ulang hanya jika perlu.",
            RequiresAdmin = true,
            Targets = () => [new Target(Path.Combine(Sys.WindowsDir, @"SoftwareDistribution\Download"))],
            Before = ct => stopped = Sys.Run("net.exe", "stop wuauserv", ct) == 0,
            After = () =>
            {
                if (stopped) Sys.Run("net.exe", "start wuauserv", CancellationToken.None);
            },
        };
    }

    private static IEnumerable<Target> BrowserCaches()
    {
        string local = Sys.LocalAppData;
        string[] chromium = [@"Google\Chrome", @"Microsoft\Edge", @"BraveSoftware\Brave-Browser"];
        string[] cacheDirs = ["Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage"];

        foreach (var browser in chromium)
        {
            string userData = Path.Combine(local, browser, "User Data");
            if (!Directory.Exists(userData)) continue;

            foreach (var profile in Directory.EnumerateDirectories(userData))
            {
                string name = Path.GetFileName(profile);
                if (name != "Default" && name != "Guest Profile" && !name.StartsWith("Profile ")) continue;
                foreach (var sub in cacheDirs)
                    yield return new Target(Path.Combine(profile, sub));
            }
        }

        string firefox = Path.Combine(local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefox))
        {
            foreach (var profile in Directory.EnumerateDirectories(firefox))
                yield return new Target(Path.Combine(profile, "cache2"));
        }
    }
}
