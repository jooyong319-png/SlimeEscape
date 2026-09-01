# Unity 게임 화면의 한 점을 눌러본다. (창 안 비율 좌표 0~1)
#   powershell -File tools\click.ps1 0.28 0.25
#
# 🔴 사장님이 주무시는 동안 "눌리는지"를 실제로 확인하려고 만든 것 (2026-09-02).
#    눈으로 화면만 봐서는 버튼이 죽었는지 알 수 없다.
param([double]$x = 0.5, [double]$y = 0.5)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ClickWin {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "Unity 창을 못 찾음"; exit 1 }
[void][ClickWin]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 500

$r = New-Object ClickWin+RECT
[void][ClickWin]::GetWindowRect($p.MainWindowHandle, [ref]$r)
$px = [int]($r.L + ($r.R - $r.L) * $x)
$py = [int]($r.T + ($r.B - $r.T) * $y)

[void][ClickWin]::SetCursorPos($px, $py)
Start-Sleep -Milliseconds 250
[ClickWin]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)   # 왼쪽 누름
Start-Sleep -Milliseconds 90
[ClickWin]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)   # 뗌
Start-Sleep -Milliseconds 900
Write-Output "눌렀다: 창 안 ($x, $y) -> 화면 ($px, $py)"
