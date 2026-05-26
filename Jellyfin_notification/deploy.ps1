# deploy.ps1 - Build & deploy Jellyfin Notification plugin to Jellyfin
# Supporte installation service (ProgramData) et tray (AppData\Local)

param(
    [string]$Version = "2.1.1.0"
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$DllSrc     = Join-Path $ProjectDir "bin\Release\net9.0\Jellyfin_notification.dll"
$MetaSrc    = Join-Path $ProjectDir "meta.json"
$FolderName = "Jellyfin_notification_$Version"

# Detection automatique du repertoire plugins Jellyfin
$PossibleRoots = @(
    "C:\ProgramData\Jellyfin\Server\plugins",
    "$env:LOCALAPPDATA\jellyfin\plugins"
)
$PluginsRoot = $PossibleRoots | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $PluginsRoot) {
    Write-Error "Aucun repertoire plugins Jellyfin trouve. Verifiez votre installation."
    exit 1
}

$Dest = Join-Path $PluginsRoot $FolderName
Write-Host "  -> Repertoire cible : $Dest" -ForegroundColor DarkGray

# 1. Build
Write-Host ""
Write-Host "[1/4] Build Release net9.0..." -ForegroundColor Cyan
dotnet build "$ProjectDir\Jellyfin_notification.csproj" -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) { Write-Error "Build echoue."; exit 1 }
Write-Host "      OK - 0 erreur." -ForegroundColor Green

# 2. Stop Jellyfin
Write-Host ""
Write-Host "[2/4] Arret de Jellyfin..." -ForegroundColor Cyan
$jf = Get-Process -Name "jellyfin" -ErrorAction SilentlyContinue
if ($jf) {
    try {
        $jf | Stop-Process -Force
        Start-Sleep -Seconds 3
        Write-Host "      Jellyfin arrete." -ForegroundColor Green
    } catch {
        Write-Host "      ATTENTION : impossible d'arreter Jellyfin (acces refuse)." -ForegroundColor Red
        Write-Host "      La DLL est peut-etre verrouilee - arretez Jellyfin manuellement puis relancez deploy.ps1." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "      Jellyfin non actif, on continue." -ForegroundColor Yellow
}

# 3. Nettoyage des anciennes versions
Write-Host ""
Write-Host "[3/4] Nettoyage des anciennes versions..." -ForegroundColor Cyan
Get-ChildItem $PluginsRoot -Directory -Filter "Jellyfin_notification_*" | ForEach-Object {
    if ($_.FullName -ne $Dest) {
        Remove-Item $_.FullName -Recurse -Force
        Write-Host "      Supprime : $($_.Name)" -ForegroundColor DarkGray
    }
}

# 4. Copie des fichiers
Write-Host ""
Write-Host "[4/4] Deploiement vers $Dest..." -ForegroundColor Cyan
if (-not (Test-Path $Dest)) { New-Item -ItemType Directory -Path $Dest | Out-Null }

Copy-Item $DllSrc  -Destination $Dest -Force
Copy-Item $MetaSrc -Destination $Dest -Force

# Permissions NetworkService (service Windows uniquement)
try {
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "NT AUTHORITY\NetworkService", "FullControl", "Allow"
    )
    foreach ($file in Get-ChildItem $Dest) {
        $acl = Get-Acl $file.FullName
        $acl.AddAccessRule($rule)
        Set-Acl -Path $file.FullName -AclObject $acl
    }
    Write-Host "      Permissions NetworkService appliquees." -ForegroundColor DarkGray
} catch {
    Write-Host "      (NetworkService ignore - mode tray)" -ForegroundColor DarkGray
}

# 5. Injection du script dans index.html (contourne le pb de permissions a l'execution)
Write-Host ""
Write-Host "[5/5] Injection dans index.html..." -ForegroundColor Cyan
$IndexHtmlPaths = @(
    "C:\Program Files\Jellyfin\Server\jellyfin-web\index.html",
    "C:\ProgramData\Jellyfin\Server\jellyfin-web\index.html"
)
$IndexHtml = $IndexHtmlPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $IndexHtml) {
    Write-Host "      index.html introuvable - injection ignoree." -ForegroundColor Yellow
} else {
    $PluginTag = "JellyfinNotification"
    $ScriptUrl = "/JellyNotif/client?v=$Version"
    $ScriptTag = "<script plugin=""$PluginTag"" version=""$Version"" src=""$ScriptUrl"" defer></script>"

    $html = [System.IO.File]::ReadAllText($IndexHtml, [System.Text.Encoding]::UTF8)

    # Supprimer ancienne balise si presente
    $html = [System.Text.RegularExpressions.Regex]::Replace(
        $html,
        '<script[^>]*plugin=[''"]' + $PluginTag + '[''"][^>]*>\s*</script>',
        ''
    )

    # Injecter avant </body>
    if ($html.Contains("</body>")) {
        $html = $html.Replace("</body>", "$ScriptTag`n</body>")
        [System.IO.File]::WriteAllText($IndexHtml, $html, [System.Text.Encoding]::UTF8)
        Write-Host "      OK - Script injecte dans $IndexHtml" -ForegroundColor Green
    } else {
        Write-Host "      ERREUR : </body> introuvable dans index.html" -ForegroundColor Red
    }
}

# Recapitulatif
Write-Host ""
Write-Host "Fichiers deployes :" -ForegroundColor Cyan
Get-ChildItem $Dest | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

Write-Host ""
Write-Host "OK - Deploiement v$Version termine." -ForegroundColor Green
Write-Host "   Relancez Jellyfin manuellement (tray ou raccourci)." -ForegroundColor Green
