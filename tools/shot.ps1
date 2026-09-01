# Unity 창만 찍는다. 화면 전체가 아니라 그 창만 — 다른 게 안 찍히게.
#   쓰기:  powershell -ExecutionPolicy Bypass -File tools\shot.ps1
#   나오는 곳: tools\shot.png
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

# Unity 편집기 프로세스를 찾는다 (Hub 는 뺀다)
$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "Unity 창을 못 찾음 (편집기가 떠 있는지 보세요)"; exit 1 }

$hwnd = $p.MainWindowHandle
if ([Win]::IsIconic($hwnd)) { [void][Win]::ShowWindow($hwnd, 9) }
[void][Win]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

$r = New-Object Win+RECT
[void][Win]::GetWindowRect($hwnd, [ref]$r)
$w = $r.R - $r.L; $h = $r.B - $r.T
if ($w -le 0 -or $h -le 0) { Write-Output "창 크기를 못 읽음"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$out = Join-Path $PSScriptRoot 'shot.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "$out  ($w x $h)  $($p.MainWindowTitle)"
