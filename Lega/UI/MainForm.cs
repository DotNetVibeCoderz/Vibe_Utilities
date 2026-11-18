using Lega.Core;

namespace Lega.UI;

public sealed class MainForm : Form
{
    private enum Phase { Scanning, Ready, Cleaning, Done }

    private readonly List<CategoryRow> _rows = [];
    private readonly bool _isAdmin = Sys.IsAdmin();
    private readonly DriveInfo _drive = new(Sys.Root);
    private readonly CancellationTokenSource _cts = new();

    private readonly Band _header = new(Theme.Mist);
    private readonly Band _hero = new(Theme.Mist);
    private readonly Band _listHost = new(Theme.Surface);
    private readonly Band _strip = new(Theme.Surface);
    private readonly Band _footer = new(Theme.Surface);

    private readonly Label _brand = MakeLabel("Lega", Theme.Display(17f), Theme.Ink, Theme.Mist);
    private readonly Label _headline = MakeLabel("", Theme.Display(26f), Theme.Ink, Theme.Mist);
    private readonly Label _sub = MakeLabel("", Theme.Text(10.5f), Theme.Muted, Theme.Mist);
    private readonly Label _stripTitle = MakeLabel("Yang bisa dibersihkan", Theme.Semi(11f), Theme.Ink, Theme.Surface);
    private readonly Label _status = MakeLabel("", Theme.Text(9.5f), Theme.Muted, Theme.Surface);

    private readonly StorageBar _bar = new();
    private readonly RowList _list = new();
    private readonly SlimProgress _progress = new();

    private readonly ActionButton _btnAdmin = new("Jalankan sebagai administrator", ButtonKind.Secondary, 9.5f);
    private readonly ActionButton _btnSafe = new("Pilih yang aman", ButtonKind.Quiet, 9.5f);
    private readonly ActionButton _btnAll = new("Pilih semua", ButtonKind.Quiet, 9.5f);
    private readonly ActionButton _btnNone = new("Kosongkan pilihan", ButtonKind.Quiet, 9.5f);
    private readonly ActionButton _btnRescan = new("Pindai ulang", ButtonKind.Secondary, 10f);
    private readonly ActionButton _btnClean = new("Bersihkan", ButtonKind.Primary, 10.5f);

    private Phase _phase = Phase.Scanning;
    private long _total, _free, _gained;

    public MainForm()
    {
        Text = "Lega - pembersih drive C";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Mist;
        Font = Theme.Text(10f);
        MinimumSize = new Size(900, 640);
        ClientSize = new Size(1060, 780);

        _brand.TextAlign = ContentAlignment.MiddleLeft;
        _headline.TextAlign = ContentAlignment.BottomLeft;
        _stripTitle.TextAlign = ContentAlignment.MiddleLeft;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _bar.BackColor = Theme.Mist;
        _btnAdmin.BackColor = Theme.Mist;
        _btnSafe.BackColor = _btnAll.BackColor = _btnNone.BackColor = Theme.Surface;
        _btnRescan.BackColor = _btnClean.BackColor = Theme.Surface;

        _header.Controls.AddRange([_brand, _btnAdmin]);
        _hero.Controls.AddRange([_headline, _sub, _bar]);
        _strip.Controls.AddRange([_stripTitle, _btnSafe, _btnAll, _btnNone]);
        _listHost.Controls.AddRange([_strip, _list]);
        _footer.Controls.AddRange([_progress, _status, _btnRescan, _btnClean]);
        Controls.AddRange([_header, _hero, _listHost, _footer]);

        foreach (var category in Catalog.Build())
        {
            var row = new CategoryRow(category, category.RequiresAdmin && !_isAdmin);
            row.CheckedChanged += (_, _) => OnSelectionChanged();
            _rows.Add(row);
            _list.Controls.Add(row);
        }

        _list.ClientSizeChanged += (_, _) => FitRows();
        _btnAdmin.Visible = !_isAdmin;
        _btnAdmin.Click += (_, _) => { if (Sys.RestartAsAdmin()) Close(); };
        _btnSafe.Click += (_, _) => SelectWhere(r => r.Category.Risk == Risk.Safe);
        _btnAll.Click += (_, _) => SelectWhere(_ => true);
        _btnNone.Click += (_, _) => SelectWhere(_ => false);
        _btnRescan.Click += async (_, _) => await RunGuarded(ScanAllAsync);
        _btnClean.Click += async (_, _) => await RunGuarded(CleanAsync);

        FormClosing += (_, _) => _cts.Cancel();
    }

    private static Label MakeLabel(string text, Font font, Color fore, Color back) => new()
    {
        AutoSize = false,
        Text = text,
        Font = font,
        ForeColor = fore,
        BackColor = back,
        AutoEllipsis = true,
        UseMnemonic = false,
    };

    private int P(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    // ------------------------------------------------------------------ layout

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = new Size(P(900), P(640));
        ClientSize = new Size(P(1060), P(780));
        CenterToScreen();
        LayoutAll();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RunGuarded(ScanAllAsync);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutAll();
    }

    private void LayoutAll()
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        int hHeader = P(60), hHero = P(190), hFooter = P(80);
        int listTop = hHeader + hHero;

        SuspendLayout();
        _header.SetBounds(0, 0, w, hHeader);
        _hero.SetBounds(0, hHeader, w, hHero);
        _listHost.SetBounds(0, listTop, w, Math.Max(0, h - listTop - hFooter));
        _footer.SetBounds(0, h - hFooter, w, hFooter);

        LayoutHeader();
        LayoutHero();
        LayoutList();
        LayoutFooter();
        ResumeLayout();
    }

    private void LayoutHeader()
    {
        int w = _header.Width, pad = P(32);
        _brand.SetBounds(pad, 0, P(240), _header.Height);

        int bw = _btnAdmin.MeasureWidth(), bh = P(36);
        _btnAdmin.SetBounds(w - pad - bw, (_header.Height - bh) / 2, bw, bh);
    }

    private void LayoutHero()
    {
        int pad = P(32), inner = Math.Max(0, _hero.Width - pad * 2);
        _headline.SetBounds(pad, P(4), inner, P(52));
        _sub.SetBounds(pad, P(58), inner, P(26));
        _bar.SetBounds(pad, P(104), inner, _bar.PreferredHeight);
    }

    private void LayoutList()
    {
        int w = _listHost.Width, pad = P(32), hStrip = P(56);
        _strip.SetBounds(0, 0, w, hStrip);
        _stripTitle.SetBounds(pad, 0, P(300), hStrip);

        int x = w - pad, bh = P(32);
        foreach (var b in new[] { _btnNone, _btnAll, _btnSafe })
        {
            int bw = b.MeasureWidth();
            x -= bw;
            b.SetBounds(x, (hStrip - bh) / 2, bw, bh);
            x -= P(4);
        }

        _list.SetBounds(0, hStrip, w, Math.Max(0, _listHost.Height - hStrip));
        FitRows();
    }

    private void LayoutFooter()
    {
        int w = _footer.Width, pad = P(32), bh = P(44);
        _progress.SetBounds(0, 0, w, P(2));

        int cw = Math.Max(P(200), _btnClean.MeasureWidth());
        int top = (_footer.Height - bh) / 2 + P(1);
        _btnClean.SetBounds(w - pad - cw, top, cw, bh);

        int rw = _btnRescan.MeasureWidth();
        _btnRescan.SetBounds(_btnClean.Left - P(12) - rw, top, rw, bh);

        _status.SetBounds(pad, P(2), Math.Max(0, _btnRescan.Left - pad - P(16)), _footer.Height - P(2));
    }

    private void FitRows()
    {
        int w = _list.ClientSize.Width, h = P(76);
        foreach (var r in _rows)
            if (r.Width != w || r.Height != h) r.Size = new Size(w, h);
    }

    // ------------------------------------------------------------------ state

    private void RefreshDrive()
    {
        try
        {
            _total = _drive.TotalSize;
            _free = _drive.AvailableFreeSpace;
        }
        catch (IOException) { }
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        bool idle = phase is Phase.Ready or Phase.Done;

        _progress.Active = phase is Phase.Scanning or Phase.Cleaning;
        foreach (var r in _rows) r.Frozen = phase == Phase.Cleaning;
        _btnSafe.Enabled = _btnAll.Enabled = _btnNone.Enabled = _btnRescan.Enabled = _btnAdmin.Enabled = idle;

        if (idle && !_isAdmin)
        {
            long locked = _rows.Where(r => r.Locked).Sum(r => r.Bytes);
            _btnAdmin.Text = locked > 0
                ? $"Buka {Format.Bytes(locked)} lagi sebagai administrator"
                : "Jalankan sebagai administrator";
            LayoutHeader();
        }

        UpdateSummary();
    }

    private void OnSelectionChanged()
    {
        if (_phase == Phase.Done) _phase = Phase.Ready;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var picked = _rows.Where(r => r.Checked).ToList();
        long reclaim = picked.Sum(r => r.Bytes);
        bool idle = _phase is Phase.Ready or Phase.Done;

        _bar.SetValues(_total, _total - _free, reclaim);

        string free = Format.Bytes(_free), total = Format.Bytes(_total);
        switch (_phase)
        {
            case Phase.Scanning:
                _headline.Text = "Memindai drive C…";
                _sub.Text = "Menghitung ukuran tiap kategori. Belum ada yang dihapus.";
                break;

            case Phase.Cleaning:
                _headline.Text = "Membersihkan drive C…";
                _sub.Text = "Biarkan Lega terbuka sampai selesai. Berkas yang sedang dipakai dilewati.";
                break;

            case Phase.Done when _gained >= 1 << 20:
                _headline.Text = $"{Format.Bytes(_gained)} kembali ke drive C";
                _sub.Text = $"{free} bebas dari {total}.";
                break;

            case Phase.Done:
                _headline.Text = "Tidak ada ruang baru yang terlepas";
                _sub.Text = "Berkas mungkin sedang dipakai. Tutup aplikasi lain, lalu pindai ulang.";
                break;

            default:
                if (picked.Count == 0)
                {
                    _headline.Text = "Pilih apa yang ingin dibersihkan";
                    _sub.Text = "Centang kategori di bawah untuk melihat berapa ruang yang kembali.";
                }
                else if (reclaim == 0)
                {
                    _headline.Text = "Windows yang akan menghitung hasilnya";
                    _sub.Text = "Ruang yang kembali dihitung setelah pembersihan selesai.";
                }
                else
                {
                    _headline.Text = $"{Format.Bytes(reclaim)} bisa dikosongkan di drive C";
                    _sub.Text = $"{free} bebas dari {total} sekarang. Setelah dibersihkan: {Format.Bytes(_free + reclaim)} bebas.";
                }
                break;
        }

        _btnClean.Text = reclaim > 0 ? $"Bersihkan {Format.Bytes(reclaim)}" : "Bersihkan";
        _btnClean.Enabled = idle && picked.Count > 0;

        if (_phase != Phase.Cleaning)
            _status.Text = _phase == Phase.Scanning
                ? "Menunggu pemindaian selesai"
                : $"{picked.Count} dari {_rows.Count} kategori dipilih";

        LayoutFooter();
    }

    private void ApplyShares()
    {
        long max = _rows.Where(r => r.Category.SizeKnown).Select(r => r.Bytes).DefaultIfEmpty(0).Max();
        foreach (var r in _rows) r.Share = max > 0 ? (double)r.Bytes / max : 0;
    }

    private void SelectWhere(Func<CategoryRow, bool> pick)
    {
        foreach (var r in _rows) r.Checked = r.CanToggle && pick(r);
    }

    // ------------------------------------------------------------------ scan

    private async Task ScanAllAsync()
    {
        var ct = _cts.Token;
        RefreshDrive();
        SetPhase(Phase.Scanning);

        foreach (var r in _rows)
        {
            r.Checked = false;
            r.Bytes = 0;
            r.SetNote(null);
            r.State = RowState.Scanning;
        }

        using var gate = new SemaphoreSlim(3);
        var tasks = _rows.Select(async row =>
        {
            await gate.WaitAsync(ct);
            try
            {
                long bytes;
                try { bytes = await Task.Run(() => row.Category.Scan(ct), ct); }
                catch (OperationCanceledException) { throw; }
                catch { bytes = 0; }

                row.Bytes = bytes;
                row.State = RowState.Ready;
                row.Checked = row.Category.DefaultChecked && row.CanToggle;
                ApplyShares();
                UpdateSummary();
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
        if (IsDisposed) return;

        SortRowsBySize();
        SetPhase(Phase.Ready);
    }

    private void SortRowsBySize()
    {
        // Yang ukurannya tidak diketahui (WinSxS) diurutkan di atas yang kosong.
        var ordered = _rows.OrderByDescending(r => r.Bytes + (r.Category.SizeKnown ? 0 : 1)).ToList();
        for (int i = 0; i < ordered.Count; i++)
            _list.Controls.SetChildIndex(ordered[i], i);
        _rows.Clear();
        _rows.AddRange(ordered);
    }

    // ------------------------------------------------------------------ clean

    private async Task CleanAsync()
    {
        var chosen = _rows.Where(r => r.Checked).ToList();
        if (chosen.Count == 0 || !Confirm(chosen)) return;

        var ct = _cts.Token;
        RefreshDrive();
        long freeBefore = _free;
        SetPhase(Phase.Cleaning);

        for (int i = 0; i < chosen.Count; i++)
        {
            var row = chosen[i];
            row.State = RowState.Cleaning;
            _status.Text = $"Membersihkan {row.Category.Title} ({i + 1} dari {chosen.Count})";

            bool failed = false;
            try { await Task.Run(() => row.Category.Clean(ct), ct); }
            catch (OperationCanceledException) { throw; }
            catch { failed = true; }

            long left = 0;
            try { left = await Task.Run(() => row.Category.Scan(ct), ct); }
            catch (OperationCanceledException) { throw; }
            catch { /* ukuran sisa tidak diketahui */ }

            row.Bytes = left;
            row.Checked = false;
            row.State = RowState.Ready;
            if (failed) row.SetNote("Gagal dibersihkan", Theme.Danger);
            else if (left > 0) row.SetNote("Sisa sedang dipakai", Theme.Muted);
            else row.SetNote("Dibersihkan", Theme.Accent);
        }

        if (IsDisposed) return;

        ApplyShares();
        RefreshDrive();
        _gained = Math.Max(0, _free - freeBefore);
        SetPhase(Phase.Done);
    }

    private bool Confirm(List<CategoryRow> chosen)
    {
        var notes = chosen
            .Where(r => r.Category.Consequence is not null)
            .Select(r => $"• {r.Category.Title}: {r.Category.Consequence}")
            .ToList();
        bool careful = chosen.Any(r => r.Category.Risk == Risk.Careful);
        long size = chosen.Sum(r => r.Bytes);

        var go = new TaskDialogButton("Bersihkan");
        var cancel = new TaskDialogButton("Batal");
        var page = new TaskDialogPage
        {
            Caption = "Lega",
            Heading = size > 0
                ? $"Bersihkan sekitar {Format.Bytes(size)} dari drive C?"
                : "Bersihkan kategori yang dipilih?",
            Text = "Berkas dihapus permanen dan tidak masuk Recycle Bin. Berkas yang sedang dipakai Windows akan dilewati." +
                   (notes.Count > 0 ? "\n\nPerhatikan:\n" + string.Join("\n", notes) : ""),
            Icon = careful ? TaskDialogIcon.Warning : TaskDialogIcon.None,
            AllowCancel = true,
            Buttons = { go, cancel },
            DefaultButton = careful ? cancel : go,
        };

        return TaskDialog.ShowDialog(this, page) == go;
    }

    private async Task RunGuarded(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Jendela ditutup saat proses berjalan.
        }
        catch (Exception ex) when (!IsDisposed)
        {
            SetPhase(Phase.Ready);
            TaskDialog.ShowDialog(this, new TaskDialogPage
            {
                Caption = "Lega",
                Heading = "Terjadi kesalahan",
                Text = ex.Message,
                Icon = TaskDialogIcon.Error,
                Buttons = { TaskDialogButton.OK },
            });
        }
    }
}
