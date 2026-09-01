# 유니티에 Ctrl+R 을 보내 **강제로 다시 읽게** 한다.
#
# 🔴 2026-09-02 밤에 한 시간을 먹은 것:
#    창에 초점만 줘도 유니티가 안 읽어들였다 (Auto Refresh 가 꺼져 있는 듯).
#    소스는 03:07인데 컴파일된 것은 02:05이었다 — 그 사이 고친 게
#    하나도 게임에 안 들어있었다. 화면을 아무리 봐도 안 바뀌니 계속 헛짚었다.
#
#    🔴 바뀌었는지 확인하려면 **어셈블리 시각**을 본다:
#       game/Library/ScriptAssemblies/Assembly-CSharp.dll
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class RefWin { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); }
"@
$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "Unity 창을 못 찾음"; exit 1 }
[void][RefWin]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 700
[System.Windows.Forms.SendKeys]::SendWait("^r")
Write-Output "Ctrl+R 보냄"
