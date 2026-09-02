# Unity 의 **Play 단추를 직접 누른다.**
#
# 🔴 2026-09-02: Ctrl+P 는 창 초점에 따라 안 먹는다.
#    프로젝트 창에 초점이 있을 때는 그쪽이 가로채서,
#    보낸 키가 폴더 이동이 되어버렸다 (T 를 눌렀더니 Tests 폴더로 갔다).
#    단추 자리를 직접 누르는 쪽이 확실하다.
Add-Type @"
using System; using System.Runtime.InteropServices;
public class PlayWin {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "Unity 창을 못 찾음"; exit 1 }
$hwnd = $p.MainWindowHandle
if ([PlayWin]::IsIconic($hwnd)) { [void][PlayWin]::ShowWindow($hwnd, 9) }
[void][PlayWin]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 700

$r = New-Object PlayWin+RECT
[void][PlayWin]::GetWindowRect($hwnd, [ref]$r)
# Play 단추는 위 띄 가운데에서 약간 왼쪽
$px = [int]($r.L + ($r.R - $r.L) * 0.506)
$py = [int]($r.T + ($r.B - $r.T) * 0.066)
[void][PlayWin]::SetCursorPos($px, $py)
Start-Sleep -Milliseconds 200
[PlayWin]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 90
[PlayWin]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 2000
Write-Output "Play 단추 누름"
