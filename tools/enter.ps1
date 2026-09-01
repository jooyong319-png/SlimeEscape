# 컴파일을 기다렸다가 Play 를 **확실히 켜고**, 개발자 키로 판까지 들어간다.
#   powershell -File tools\enter.ps1 5     (앞으로 5판)
#
# 🔴 사장님이 주무시는 동안 혼자 확인하려고 만든 것 (2026-09-02).
#    눈으로 화면을 봐야만 다음 단계를 정할 수 있으면 확인 한 번에 여러 차례가 든다.
#    코드가 판을 열 때 "[판] 1-5 열림" 을 찍으니, **로그로 상태를 읽어** 기계적으로 간다.
#
# ⚠️ Ctrl+P 는 **토글**이다. 이미 켜져 있는데 누르면 꺼진다 —
#    그래서 누른 뒤 로그에 새 "[판]" 이 났는지 보고, 없으면 한 번 더 누른다.
param([int]$advance = 0)

$log = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"

# 🔴 Editor.log 는 수십만 줄까지 커진다. 통째로 읽으면 몇 초씩 걸려서
# 그 사이에 난 줄을 놓친다. **꼬리만** 읽는다.
function Tail() {
  return (Get-Content $log -Tail 400 -ErrorAction SilentlyContinue) -join "`n"
}

$mark = (Get-Item $log).Length

# 1) 초점을 줘서 다시 들여오기/컴파일을 시킨다
& "$PSScriptRoot\shot.ps1" | Out-Null

# 2) 로그가 8초간 안 늘면 컴파일이 끝난 것으로 본다
$last = -1; $still = 0
for ($i = 0; $i -lt 90; $i++) {
  Start-Sleep -Seconds 2
  $now = (Get-Item $log).Length
  if ($now -eq $last) { $still += 2 } else { $still = 0 }
  $last = $now
  if ($still -ge 8) { break }
}

# 3) 이번 컴파일에서 난 오류만 본다
$err = (Tail) -split "`n" | Select-String -Pattern "error CS"
if ($err) { Write-Output "🔴 컴파일 오류:"; $err | Select-Object -First 4; exit 1 }

# 4) Play 를 켜다. Ctrl+P 는 **토글**이라 이미 켜져 있으면 꺼진다 —
#    새 [LV] 로그가 날 때까지 번갈아 누른다.
$ok = $false
for ($try = 1; $try -le 4; $try++) {
  & "$PSScriptRoot\play.ps1" | Out-Null
  for ($i = 0; $i -lt 8; $i++) {
    Start-Sleep -Seconds 2
    if ((Tail) -match "\[LV\] ") { $ok = $true; break }
  }
  if ($ok) { break }
}
if (-not $ok) { Write-Output "⚠️ Play 가 안 켜졌다 — 화면을 직접 보세요"; exit 2 }

# 5) 개발자 모드로 판을 넘긴다 (T = F1 과 같은 뜻. 에디터가 F1 을 먹는다)
if ($advance -gt 0) {
  & "$PSScriptRoot\keys.ps1" "T" 500 | Out-Null
  $seq = (1..$advance | ForEach-Object { "N" }) -join " "
  & "$PSScriptRoot\keys.ps1" $seq 380 | Out-Null
  Start-Sleep -Seconds 1
  $opened = (Tail) -split "`n" | Select-String -Pattern "\[LV\] " | Select-Object -Last 1
  if ($opened) { Write-Output ($opened -replace '.*\[LV\]', '열림:') }
  else { Write-Output "⚠️ 판이 안 넘어갔다 (개발자 키가 안 먹었다)" }
} else {
  Write-Output "Play 켜짐"
}
