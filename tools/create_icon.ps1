# Generates the mod's toolbar icon: an arrow coming down and stopped by the ground, white on
# transparent.
#
# Style follows the sibling mods (Bulldozer's dozer, VesselBookmark's bookmark): a single flat white
# silhouette, no disc, no second colour, no outline. KSP tints the toolbar button itself, so any
# colour of ours would fight it.
#
# Why an arrow and a hatched line: what the mod does is stop a part going through the ground, and at
# 38 px the only two things that read reliably are a hatched horizontal band (a ground section, the
# way a drawing shows soil) and a thick arrow. The arrow TOUCHES the band rather than crossing it -
# an arrow that pierces the line would say the opposite of what the mod does.
#
# Nothing here is a scaled-down version of a bigger drawing: every feature is sized so that it still
# measures at least 2 px after the reduction. Multiply any fraction below by 38 to check - the hatch
# strokes are the ones that are right on the limit.
#
# Why it is drawn 8x and reduced: GDI+ has no analytic antialiasing worth the name on filled paths.
# The reduction also blends colour with alpha, which fringes a white-on-transparent shape grey, so
# the RGB is forced back to pure white afterwards and only the alpha is kept from the resampling.
#
# Run from anywhere: powershell -ExecutionPolicy Bypass -File tools\create_icon.ps1

Add-Type -AssemblyName System.Drawing

# Output size (px). 38 is the ApplicationLauncher's native button size; handing it anything else only
# buys a resample it does not need.
$size = 38

# Supersampling factor used before the reduction.
$scale = 8

# Geometry, in fractions of the canvas: x to the right, y DOWNWARDS (GDI+ convention).

# --- The arrow, i.e. the part being pushed down. Deliberately the heaviest mass of the drawing: it
# is the moving thing, and the ground below it only has to read as a surface.
$shaft = @(0.440, 0.140, 0.560, 0.360)         # left, top, right, bottom

# The head. Its tip sits exactly ON the top of the ground band: they abut, they do not overlap - the
# arrow is stopped BY the surface, it does not enter it.
$headLeft   = @(0.300, 0.335)
$headRight  = @(0.700, 0.335)
$headTip    = @(0.500, 0.600)

# --- The ground: a solid band, and the hatching that turns it from "a line" into "soil seen in
# section". Without the hatching the band reads as a shelf and the whole icon as a download arrow.
$ground = @(0.100, 0.600, 0.900, 0.672)        # left, top, right, bottom

# Hatch strokes, hung under the band and leaning the way a section hatch does. Their x is where each
# one meets the band; each runs down-left by $hatchRun.
$hatchXs        = @(0.225, 0.385, 0.545, 0.705, 0.865)
$hatchTop       = 0.672
$hatchBottom    = 0.850
$hatchRun       = 0.130
$hatchThickness = 0.060

# Resolve the output relative to this script, independent of the CWD.
$iconPath = Join-Path $PSScriptRoot '..\GameData\EvaCMGroundMod\icon.png'
$iconPath = [System.IO.Path]::GetFullPath($iconPath)

$work = $size * $scale

# Turns a fraction-of-canvas pair into a PointF of the supersampled bitmap.
function New-Point($xy) {
    return New-Object System.Drawing.PointF(([float]($xy[0] * $work)), ([float]($xy[1] * $work)))
}

# Turns a list of fraction pairs into the PointF[] the GDI+ polygon calls want.
function New-Polygon($points) {
    return [System.Drawing.PointF[]]@($points | ForEach-Object { New-Point $_ })
}

# Fills a @(left, top, right, bottom) box given in fractions of the canvas.
function Add-Box($graphics, $brush, $box) {
    $graphics.FillRectangle(
        $brush,
        [float]($box[0] * $work),
        [float]($box[1] * $work),
        [float](($box[2] - $box[0]) * $work),
        [float](($box[3] - $box[1]) * $work))
}

$bitmap = New-Object System.Drawing.Bitmap($work, $work, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

# --- The arrow.
Add-Box $graphics $brush $shaft
$graphics.FillPolygon($brush, (New-Polygon @($headLeft, $headRight, $headTip)))

# --- The ground band.
Add-Box $graphics $brush $ground

# --- The hatching, stroked rather than filled: a pen gets the constant thickness for free, where
# offsetting each stroke by hand does not.
$hatchPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float]($hatchThickness * $work))
$hatchPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
$hatchPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Flat
foreach ($x in $hatchXs) {
    $graphics.DrawLine($hatchPen, (New-Point @($x, $hatchTop)), (New-Point @(($x - $hatchRun), $hatchBottom)))
}
$hatchPen.Dispose()

$brush.Dispose()
$graphics.Dispose()

# --- Reduce to the output size.
$icon = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$iconGraphics = [System.Drawing.Graphics]::FromImage($icon)
$iconGraphics.Clear([System.Drawing.Color]::Transparent)
$iconGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$iconGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$iconGraphics.DrawImage($bitmap, (New-Object System.Drawing.Rectangle(0, 0, $size, $size)))
$iconGraphics.Dispose()
$bitmap.Dispose()

# --- Force every pixel to pure white and keep only the resampled alpha.
#
# The reduction interpolates the colour channels along with alpha, so a pixel on the edge comes out
# as white-blended-with-transparent-black, i.e. grey. Unnoticeable over a dark toolbar, visible as a
# dirty fringe anywhere else - and it also makes the icon unusable as a tint source.
$rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
$data = $icon.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $bytes = New-Object byte[] ($data.Stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    for ($y = 0; $y -lt $size; $y++) {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $size; $x++) {
            # BGRA: leave the 4th byte (alpha) alone.
            $i = $row + $x * 4
            $bytes[$i] = 255
            $bytes[$i + 1] = 255
            $bytes[$i + 2] = 255
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
} finally {
    $icon.UnlockBits($data)
}

New-Item -ItemType Directory -Force -Path (Split-Path $iconPath) | Out-Null
$icon.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)
$icon.Dispose()

Write-Host "create_icon: wrote $iconPath ($size x $size)"
