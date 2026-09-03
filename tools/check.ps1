# 배치모드로 컴파일해서 **정말 컴파일됐는지까지** 확인한다.
#
#   powershell -File tools\check.ps1
#
# 🔴 왜 이게 있나 (2026-09-04):
#    `grep "error CS"` 만 보다가 **세 번** 속았다.
#    · 유니티가 이미 떠 있어 배치모드가 20줄 찍고 죽었는데 "오류 없음"
#    · 빈 씬이라 게임이 안 돌았는데 "예외 0건"
#    · 못 잰 판을 "쉬운 판" 이라고
#
#    **없는 것을 못 찾은 것과 있는데 안 본 것은 다르다.**
#    그래서 여기서는 "컴파일까지 갔다"는 **증거**를 먼저 확인한다.

$ErrorActionPreference = 'Stop'
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
$proj  = "d:\Make_game\SlimeEscape\game"
$log   = "d:\Make_game\SlimeEscape\compile.log"

# ── 남은 유니티가 있으면 배치모드가 못 들어간다 ────────────────
$live = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue
if ($live) {
  Write-Output "SKIP  유니티가 떠 있다 (pid $($live.Id -join ',')) — 배치모드를 못 쓴다"
  Write-Output "      에디터를 닫지 말 것. 사장님이 켜 두신 것이다."
  exit 2
}

Remove-Item $log -ErrorAction SilentlyContinue
$t0 = Get-Date
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
  '-batchmode','-quit','-nographics','-projectPath',$proj,'-logFile',$log)
$sec = [int]((Get-Date) - $t0).TotalSeconds

if (-not (Test-Path $log)) { Write-Output "FAIL  로그가 안 생겼다"; exit 1 }

$text = Get-Content $log -Raw
$errs = Select-String -Path $log -Pattern 'error CS'

# 🔴 **증거 먼저.** 이 줄이 없으면 컴파일까지 못 간 것이다 —
#    그때의 "error CS 없음" 은 아무 뜻이 없다.
$ran = $text -match 'Refresh completed|CompileScripts|Reloading assemblies|Application\.AssetDatabase'

Write-Output "종료코드 $($p.ExitCode) · $sec 초 · 로그 $((Get-Content $log).Count) 줄"

if (-not $ran) {
  Write-Output "FAIL  컴파일까지 못 갔다 — 아래는 로그 끝"
  Get-Content $log -Tail 6
  exit 1
}
if ($errs) {
  Write-Output "FAIL  컴파일 오류 $($errs.Count) 건"
  $errs | Select-Object -First 15
  exit 1
}
Write-Output "OK    컴파일 통과 (증거: 에셋 갱신까지 돌았다)"
exit 0
