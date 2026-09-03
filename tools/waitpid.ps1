# 🔴 일이 **정말 끝났는지** 기다린다.  powershell -File tools\waitpid.ps1 <pid> [초]
#
# 왜 있나 (2026-09-05):
#   `for i in 1..28; do ...; sleep 20; done; echo "끝"` 을 쓰다가
#   **시간이 다 된 것을 「끝」이라고 세 번** 읽었다. 그 바람에
#   · 아직 도는 일을 죽은 줄 알고 원인을 지어냈고
#   · 같은 파일에 쓰는 놈을 하나 더 띄웠다 (자료가 깨질 뻔했다)
#
#   ⚠️ 「초록불을 의심하기」와 같은 병이다 — 검사가 엉뚱한 걸 재고 있었다.
#      여기서는 **끝났다 / 아직 돈다** 를 구분해서 말한다.
param([int]$Id, [int]$Seconds = 600)
$t0 = Get-Date
while ((Get-Date) - $t0 -lt [TimeSpan]::FromSeconds($Seconds)) {
  if (-not (Get-Process -Id $Id -ErrorAction SilentlyContinue)) {
    Write-Output "DONE  pid $Id 끝남 ($([int]((Get-Date)-$t0).TotalSeconds)초)"
    exit 0
  }
  Start-Sleep -Seconds 10
}
Write-Output "STILL pid $Id 아직 돈다 ($Seconds 초 기다렸다) — 죽은 게 아니다"
exit 3
