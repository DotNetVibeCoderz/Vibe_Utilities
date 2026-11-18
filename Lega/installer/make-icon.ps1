# Membuat Assets\lega.ico dari geometri yang sama dengan UI\Logo.cs.
# Jalankan dari folder Lega:  powershell -File installer\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\Assets\lega.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

$turmeric = [System.Drawing.ColorTranslator]::FromHtml('#E8B23F')
$pine = [System.Drawing.ColorTranslator]::FromHtml('#16302A')

function New-Mark([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Kotak bulat
    $r = $s * 0.24; $d = [Math]::Min($r * 2, $s - 1)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $d, $d, 180, 90)
    $p.AddArc($s - 1 - $d, 0, $d, $d, 270, 90)
    $p.AddArc($s - 1 - $d, $s - 1 - $d, $d, $d, 0, 90)
    $p.AddArc(0, $s - 1 - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $turmeric), $p)

    # Busur skala
    $cx = $s * 0.5; $cy = $s * 0.64; $rad = $s * 0.30
    $arc = New-Object System.Drawing.Pen $pine, ($s * 0.095)
    $arc.StartCap = 'Round'; $arc.EndCap = 'Round'
    $g.DrawArc($arc, $cx - $rad, $cy - $rad, $rad * 2, $rad * 2, 180, 180)

    # Jarum rendah + poros
    $a = 208 * [Math]::PI / 180; $len = $s * 0.26
    $needle = New-Object System.Drawing.Pen $pine, ($s * 0.085)
    $needle.StartCap = 'Round'; $needle.EndCap = 'Round'
    $g.DrawLine($needle, [float]$cx, [float]$cy, [float]($cx + [Math]::Cos($a) * $len), [float]($cy + [Math]::Sin($a) * $len))
    $hub = $s * 0.15
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $pine), $cx - $hub / 2, $cy - $hub / 2, $hub, $hub)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @($sizes | ForEach-Object { ,(New-Mark $_) })

# Berkas ICO dengan entri PNG (didukung sejak Windows Vista)
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$len); $w.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()

# Pratinjau besar untuk README
$png = Join-Path $PSScriptRoot '..\Assets\lega-256.png'
[System.IO.File]::WriteAllBytes($png, $images[-1])
"wrote $out"
