using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Lega.Core;

public static class Sys
{
    public static readonly string Root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    public static string WindowsDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string Profile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Menjalankan ulang aplikasi dengan UAC. False jika pengguna membatalkan.</summary>
    public static bool RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Menjalankan proses tanpa jendela dan menunggu selesai. Mengembalikan exit code.</summary>
    public static int Run(string file, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Gagal menjalankan {file}");
        p.OutputDataReceived += (_, _) => { };
        p.ErrorDataReceived += (_, _) => { };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        while (!p.WaitForExit(250))
        {
            if (!ct.IsCancellationRequested) continue;
            try { p.Kill(entireProcessTree: true); } catch { /* sudah selesai */ }
            ct.ThrowIfCancellationRequested();
        }

        p.WaitForExit();
        return p.ExitCode;
    }
}

public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes)
    {
        double v = Math.Max(0, bytes);
        int i = 0;
        while (v >= 1024 && i < Units.Length - 1) { v /= 1024; i++; }

        string fmt = i >= 3 ? (v >= 100 ? "0" : "0.0") : "0";
        return $"{v.ToString(fmt, CultureInfo.CurrentCulture)} {Units[i]}";
    }
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint NoConfirmation = 0x1, NoProgressUi = 0x2, NoSound = 0x4;

    public static long RecycleBinSize(string root)
    {
        var info = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(root, ref info) == 0 ? info.i64Size : 0;
    }

    public static void EmptyRecycleBin(string root) =>
        SHEmptyRecycleBin(IntPtr.Zero, root, NoConfirmation | NoProgressUi | NoSound);
}

internal static class FileUtil
{
    private static EnumerationOptions Opts(bool recurse) => new()
    {
        RecurseSubdirectories = recurse,
        IgnoreInaccessible = true,
        // Default-nya melewati Hidden/System; kita butuh keduanya. Reparse point dilewati agar tidak mengikuti symlink.
        AttributesToSkip = FileAttributes.ReparsePoint,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    /// <summary>Pengaman: jangan pernah menyentuh root drive.</summary>
    private static bool IsDangerous(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd('\\');
        return full.Length <= 3 ||
               string.Equals(full, Path.GetPathRoot(full)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    public static long Measure(Target t, CancellationToken ct)
    {
        if (IsDangerous(t.Path)) return 0;

        if (File.Exists(t.Path))
        {
            try { return new FileInfo(t.Path).Length; } catch { return 0; }
        }

        if (!Directory.Exists(t.Path)) return 0;

        long total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(t.Path).EnumerateFiles(t.Pattern, Opts(t.Recurse)))
            {
                ct.ThrowIfCancellationRequested();
                try { total += f.Length; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return total;
    }

    public static void Delete(Target t, CancellationToken ct)
    {
        if (IsDangerous(t.Path)) return;

        if (File.Exists(t.Path))
        {
            TryDelete(new FileInfo(t.Path));
            return;
        }

        if (!Directory.Exists(t.Path)) return;

        var root = new DirectoryInfo(t.Path);
        List<FileInfo> files;
        try { files = root.EnumerateFiles(t.Pattern, Opts(t.Recurse)).ToList(); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            TryDelete(f);
        }

        // Rapikan folder kosong (dari yang terdalam), folder induk target dibiarkan.
        if (t.Recurse && t.Pattern == "*")
        {
            try
            {
                var dirs = root.EnumerateDirectories("*", Opts(true))
                               .OrderByDescending(x => x.FullName.Length)
                               .ToList();
                foreach (var d in dirs)
                {
                    try { if (!d.EnumerateFileSystemInfos().Any()) d.Delete(); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void TryDelete(FileInfo f)
    {
        try
        {
            if (f.IsReadOnly) f.IsReadOnly = false;
            f.Delete();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
