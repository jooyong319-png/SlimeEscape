# 웹 빌드를 **진짜 브라우저로 열어서** 화면을 찍는다.
#
# 🔴 왜 있나 (2026-09-08): 웹 빌드가 회색 화면만 나온다고 하셨는데,
#    내가 두 번이나 "이래서 그럴 겁니다" 하고 짐작만 했다. 짐작 말고 **보고** 말한다.
#
#   powershell -ExecutionPolicy Bypass -File tools\webshot.ps1 -Url http://127.0.0.1:8777
param([string]$Url = "http://127.0.0.1:8777", [int]$Wait = 25)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class Shot {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

# 브라우저를 찾는다 (엣지 → 크롬 순)
$browsers = @(
  "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
  "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
  "C:\Program Files\Google\Chrome\Application\chrome.exe",
  "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)
$exe = $browsers | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) { Write-Output "브라우저를 못 찾았다"; exit 1 }
Write-Output "브라우저: $exe"

#  🔴 새 창으로 띄운다 — 이미 열린 창의 탭으로 들어가면 어느 창을 찍을지 알 수 없다
$p = Start-Process -PassThru -FilePath $exe -ArgumentList @(
  "--new-window", "--window-size=1280,900", "--window-position=0,0", $Url)
Start-Sleep -Seconds $Wait

#  🔴 창이 어디에 열릴지 모른다 (엣지가 --window-position 을 무시하고 옛 자리를 쓴다).
#     **맨 앞 창의 자리를 재서** 거기를 찍는다.
$h = [Shot]::GetForegroundWindow()
$r = New-Object Shot+RECT
[void][Shot]::GetWindowRect($h, [ref]$r)
$w = $r.R - $r.L; $ht = $r.B - $r.T
if ($w -le 0 -or $ht -le 0) { $r.L = 0; $r.T = 0; $w = 1280; $ht = 900 }
Write-Output ("창 자리: {0},{1}  {2}x{3}" -f $r.L, $r.T, $w, $ht)
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size $w, $ht))
$g.Dispose()
$out = "D:\Make_game\SlimeEscape\tools\webshot.png"
$bmp.Save($out)
$bmp.Dispose()
Write-Output "찍었다: $out"
