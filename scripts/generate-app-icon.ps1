[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SourcePath,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$sourcePath = [IO.Path]::GetFullPath($SourcePath)
$outputPath = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Icon source was not found: $sourcePath"
}

$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$sizes = [int[]](16, 24, 32, 48, 64, 128, 256)
$source = [Drawing.Image]::FromFile($sourcePath)
$frames = [Collections.Generic.List[byte[]]]::new()

try {
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.DrawImage($source, [Drawing.Rectangle]::new(0, 0, $size, $size))
            }
            finally {
                $graphics.Dispose()
            }

            $stream = [IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
                $frames.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}

$output = [IO.FileStream]::new($outputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
try {
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)

        $offset = 6 + (16 * $frames.Count)
        for ($index = 0; $index -lt $frames.Count; $index++) {
            $size = $sizes[$index]
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $frame = $frames[$index]
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Length)
            $writer.Write([uint32]$offset)
            $offset += $frame.Length
        }

        foreach ($frame in $frames) {
            $writer.Write($frame)
        }
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $output.Dispose()
}

Write-Output "Icon written to $outputPath"
