<#
    LIMPEZA PERMANENTE - remove bloatware e telemetria de uma vez.
    Tudo que e removido fica registrado em registro-removidos.txt
#>
$ErrorActionPreference = 'SilentlyContinue'
$Log = "$env:LOCALAPPDATA\ModoJogoLimpo\registro-removidos.txt"
New-Item (Split-Path $Log) -ItemType Directory -Force | Out-Null
$removidos = @()

Clear-Host
Write-Host ""
Write-Host "   LIMPEZA PERMANENTE DO PC" -ForegroundColor Cyan
Write-Host "  ============================================================" -ForegroundColor DarkGray

# ---------------------------------------------------------------------
#  APPS QUE SAO REMOVIDOS (prefixo do nome do pacote)
#  NAO inclui: AcerSense, AcerCareCenter (termico!), Realtek (audio),
#              Intel Graphics Software (video), Epic, Steam, Roblox
# ---------------------------------------------------------------------
$AppsRemover = @(
    'Clipchamp.Clipchamp',
    'Evernote.Evernote',
    'C27EB4BA.DropboxOEM',
    '26720RandomSaladGamesLLC.Spades',
    '26720RandomSaladGamesLLC.3899848563C1F',
    'AppUp.IntelOptaneMemoryandStorageManagement',
    'AppUp.IntelTechnologyMDE',
    'AcerIncorporated.AcerRegistration',
    'AcerIncorporated.UserExperienceImprovementProgramV'
)

Write-Host "  [1/4] Removendo apps desnecessarios..." -ForegroundColor Cyan
foreach ($a in $AppsRemover) {
    $pkg = Get-AppxPackage -Name "$a*" | Select-Object -First 1
    if ($pkg) {
        Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue
        $removidos += "APPX: $($pkg.Name)"
        Write-Host "    removido: $($pkg.Name)" -ForegroundColor Green
    }
}

# ---------------------------------------------------------------------
#  PROGRAMAS QUE SAO DESINSTALADOS
# ---------------------------------------------------------------------
Write-Host "  [2/4] Desinstalando programas..." -ForegroundColor Cyan
$Desinstalar = @('Microsoft Copilot','Intel(R) Computing Improvement Program','Intel Driver & Support Assistant','Intel(R) Driver & Support Assistant','Microsoft Edge')
foreach ($nome in $Desinstalar) {
    $reg = Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*, HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\* |
           Where-Object { $_.DisplayName -eq $nome } | Select-Object -First 1
    if ($reg -and $reg.UninstallString) {
        $cmd = $reg.UninstallString -replace '^"/?','' -replace '"$','' -replace ' /(I|S|x|quiet|silent|noreboot)$',''
        if ($cmd -match 'msiexec') {
            $prod = $reg.PSChildName
            Start-Process msiexec -ArgumentList "/x $prod /qn /norestart" -Wait
        } else {
            Start-Process $cmd -ArgumentList '/S','/quiet' -Wait
        }
        $removidos += "PROGRAMA: $nome"
        Write-Host "    removido: $nome" -ForegroundColor Green
    }
}

# OneDrive tem desinstalador proprio
$od = "$env:SystemRoot\SysWOW64\OneDriveSetup.exe"
if (Test-Path $od) {
    Start-Process $od -ArgumentList '/uninstall' -Wait
    $removidos += "PROGRAMA: Microsoft OneDrive"
    Write-Host "    removido: OneDrive" -ForegroundColor Green
}

# ---------------------------------------------------------------------
#  TAREFAS AGENDADAS DE COLETA DE DADOS
# ---------------------------------------------------------------------
Write-Host "  [3/4] Desativando tarefas de telemetria..." -ForegroundColor Cyan
$PadroesTelemetria = @(
    'Microsoft Compatibility Appraiser','CompatTelRunner','ProgramDataUpdater',
    'Consolidator','UsbCeip','KernelCeipTask','Appraiser','Proxy',
    'DiskDiagnostic','SystemInformation','MareBackup','PcaPatchDbTask','SdbinstMergeDbTask',
    'StartupAppTask','CleanupTemporaryState','DsSvcCleanup','appuriverifier',
    'RecoverabilityToastTask','BackgroundTaskHandler','CorporateWorkloadSyndication',
    'Microsoft\Windows\Feedback\Siuf\DmClient','DmClientOnScenarioDownload'
)
$n = 0
foreach ($t in @(Get-ScheduledTask)) {
    $nome = "$($t.TaskPath)$($t.TaskName)"
    foreach ($p in $PadroesTelemetria) {
        if ($nome -like "*$p*" -and $t.State -ne 'Disabled') {
            Disable-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath | Out-Null
            $removidos += "TAREFA: $nome"
            $n++
            break
        }
    }
}
Write-Host "    [$n tarefas desativadas]" -ForegroundColor Green

# ---------------------------------------------------------------------
#  INICIALIZACAO
# ---------------------------------------------------------------------
Write-Host "  [4/4] Limpando inicializacao..." -ForegroundColor Cyan
foreach ($k in 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run','HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run') {
    $p = Get-ItemProperty $k
    foreach ($prop in @($p.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' })) {
        if ($prop.Name -match 'Edge|Intel.R. Graphics Software') {
            $removidos += "INICIALIZACAO: $k -> $($prop.Name)"
            Remove-ItemProperty $k -Name $prop.Name -Force
            Write-Host "    removido: $($prop.Name)" -ForegroundColor Green
        }
    }
}

# Grava registro
$removidos | Out-File $Log -Encoding UTF8
Write-Host ""
Write-Host "  Registro salvo em: $Log" -ForegroundColor Gray
Write-Host "  Total removido: $($removidos.Count) itens" -ForegroundColor Green
Write-Host "  ============================================================" -ForegroundColor DarkGray
Write-Host "  RECOMENDA REINICIAR O PC AGORA" -ForegroundColor Yellow
Write-Host ""
Start-Sleep -Seconds 5