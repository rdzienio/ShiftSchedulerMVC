# Redeploy ShiftSchedulerMVC na VPS mikr.us.
# Wymaga: skonfigurowanego aliasu SSH "mikrus" (~/.ssh/config) z kluczem.
# Użycie:  ./redeploy.ps1
#
# Uwaga: appsettings.json NIE jest nadpisywany na serwerze (trzyma sekrety,
# np. hasło admina). Jeśli dodasz nowy klucz konfiguracji, zaktualizuj go ręcznie
# na serwerze w ~/shiftscheduler/appsettings.json.

$ErrorActionPreference = "Stop"

$proj      = "ShiftSchedulerMVC/ShiftSchedulerMVC.csproj"
$pub       = Join-Path $env:TEMP "ssmvc_publish"
$remote    = "mikrus"
$remoteDir = "shiftscheduler"
$service   = "shiftscheduler"
$healthUrl = "https://grafik.bieda.it/"

Write-Host "1/5 Publikacja (Release)..." -ForegroundColor Cyan
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish $proj -c Release -o $pub --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish nie powiodlo sie" }

Write-Host "2/5 Pomijam appsettings.json (zachowuje konfiguracje serwera)..." -ForegroundColor Cyan
Remove-Item (Join-Path $pub "appsettings.json") -ErrorAction SilentlyContinue

Write-Host "3/5 Wysylka na serwer..." -ForegroundColor Cyan
ssh -o BatchMode=yes $remote "rm -rf ~/ssmvc_publish_new"
scp -r -o BatchMode=yes "$pub" "${remote}:ssmvc_publish_new"
if ($LASTEXITCODE -ne 0) { throw "scp nie powiodlo sie" }
ssh -o BatchMode=yes $remote "cp -rf ~/ssmvc_publish_new/. ~/$remoteDir/ && rm -rf ~/ssmvc_publish_new"

Write-Host "4/5 Restart uslugi (poda haslo sudo)..." -ForegroundColor Cyan
ssh -t $remote "sudo systemctl restart $service"

Write-Host "5/5 Weryfikacja..." -ForegroundColor Cyan
Start-Sleep -Seconds 6
$code = (curl.exe -s -o NUL -w "%{http_code}" --max-time 20 $healthUrl)
if ($code -eq "200") {
    Write-Host "OK - $healthUrl zwrocil HTTP $code" -ForegroundColor Green
} else {
    Write-Host "UWAGA - $healthUrl zwrocil HTTP $code (sprawdz: journalctl -u $service -n 50)" -ForegroundColor Yellow
}
