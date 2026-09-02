# 게임 화면에 키를 보낸다.
#   powershell -File tools\keys.ps1 "RIGHT RIGHT UP LEFT"
#
# 🔴 사장님이 주무시는 동안 **조작까지** 확인하려고 만든 것 (2026-09-02).
#    화면만 봐서는 움직임이 죽었는지, 마디 사이 이음매가 어색한지 알 수 없다.
#
# 쓸 수 있는 이름: LEFT RIGHT UP DOWN Z R ESC N P H  (사이는 빈칸)
param([string]$seq = "RIGHT", [int]$gap = 420)

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KeyWin {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@

$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "Unity 창을 못 찾음"; exit 1 }
[void][KeyWin]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 500

$map = @{
  'LEFT' = '{LEFT}'; 'RIGHT' = '{RIGHT}'; 'UP' = '{UP}'; 'DOWN' = '{DOWN}'
  'ESC' = '{ESC}'; 'Z' = 'z'; 'R' = 'r'; 'N' = 'n'; 'P' = 'p'; 'H' = 'h'
  'F1' = '{F1}'; 'F2' = '{F2}'; 'F3' = '{F3}'; 'SPACE' = ' '; 'T' = 't'
  'F' = 'f'; 'G' = 'g'; 'K' = 'k'; 'X' = 'x'
}
foreach ($k in $seq.Split(' ')) {
  if (-not $k) { continue }
  $send = $map[$k.ToUpper()]
  if (-not $send) { Write-Output "모르는 키: $k"; continue }
  [System.Windows.Forms.SendKeys]::SendWait($send)
  Start-Sleep -Milliseconds $gap
}
Write-Output "보냄: $seq"
