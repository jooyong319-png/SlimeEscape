# Unity 창에 Ctrl+P 를 보내 Play 를 켠다/끈다.
#   쓰기:  powershell -ExecutionPolicy Bypass -File tools\play.ps1
#
# 🔴 사장님이 주무시는 동안 화면을 확인하려고 만든 것 (2026-09-02 허락).
#    코드를 고치면 Unity 가 다시 컴파일하며 Play 가 꺼진다 — 그래서 매번 다시 켜야 한다.
#
# ⚠️ 컴파일 중에 누르면 안 켜진다. 부르기 전에 컴파일이 끝났는지 확인할 것.
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PlayWin {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
}
"@

$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "Unity 창을 못 찾음"; exit 1 }

$hwnd = $p.MainWindowHandle
if ([PlayWin]::IsIconic($hwnd)) { [void][PlayWin]::ShowWindow($hwnd, 9) }
[void][PlayWin]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 700

[System.Windows.Forms.SendKeys]::SendWait("^p")
Start-Sleep -Milliseconds 2500
Write-Output "Ctrl+P 보냄 -> $($p.MainWindowTitle)"
