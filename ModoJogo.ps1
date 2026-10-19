<#
    MODO JOGO LIMPO (v4)
    Estilo GeForce Now: deixa SO o jogo, os launchers e o minimo do sistema.

    -Acao Ativar     [-Wallpaper <caminho>] [-Perfil nuvem|leve|personalizado]
                    [-Categorias "a,b,c"] [-BaseDir <pasta>] [-DryRun]
    -Acao Desativar  [-BaseDir <pasta>] [-DryRun]
    -Acao Busca      [-EstadoBusca on|off] [-BaseDir <pasta>]
    -Acao Estado     [-BaseDir <pasta>]

    REGRAS DE OURO
      1) Tudo que for alterado e gravado em snapshot.json ANTES de alterar.
      2) A restauracao le o snapshot e desfaz item a item.
      3) Se o snapshot nao puder ser lido, NADA e alterado (falha segura).
      4) -DryRun imprime o plano sem tocar em nada.
      5) O WebView2 NUNCA e bloqueado nem morto: e o motor da Epic, do
         Roblox e da propria pesquisa do Windows. Bloquear = janela preta.
#>
param(
    [ValidateSet('Ativar','Desativar','Estado','Busca')]
    [string]$Acao = 'Ativar',
    [ValidateSet('on','off')]
    [string]$EstadoBusca = 'on',
    [string]$Wallpaper = '',
    [ValidateSet('nuvem','leve','personalizado')]
    [string]$Perfil = 'nuvem',
    [string]$Categorias = '',
    [string]$BaseDir = '',
    [string]$Manter = '',
    [switch]$DryRun
)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference    = 'SilentlyContinue'

# =====================================================================
#  SETUP
# =====================================================================
$Estado = "$env:LOCALAPPDATA\ModoJogoLimpo"
if ((Test-Path $Estado) -and -not (Get-Item $Estado -Force).PSIsContainer) { Remove-Item $Estado -Force }
New-Item $Estado -ItemType Directory -Force | Out-Null

$ArquivoSnapshot  = "$Estado\snapshot.json"
$ArquivoResultado = "$Estado\resultado.txt"
$MarcadorAtivo    = "$Estado\ativo.txt"
$ArquivoWall      = "$Estado\wallpaper-original.txt"
$GuardDesktop     = "$Estado\desktop-guard"

if (-not $BaseDir) { $BaseDir = "$env:USERPROFILE\JogoLimpo" }
$BaseDir = $BaseDir.TrimEnd('\')

# ---- Interop: wallpaper e barra de tarefas -------------------------------
if (-not ('Wallpaper' -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Wallpaper {
    [DllImport("user32.dll", CharSet=CharSet.Auto, SetLastError=true)]
    public static extern int SystemParametersInfo(int a, int b, string c, int d);
    public const int SET = 20, UPDATE = 0x01, SEND = 0x02;
}
"@ -ErrorAction SilentlyContinue
}
if (-not ('Taskbar' -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Taskbar {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr FindWindow(string classe, string titulo);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
"@ -ErrorAction SilentlyContinue
}

# =====================================================================
#  LOG
# =====================================================================
$script:Falhas = @()
$script:Feitos = 0

function Log([string]$m, [string]$cor = 'Gray') {
    Write-Host "    $m" -ForegroundColor $cor
}
function Fez([string]$m) {
    $script:Feitos++
    Log $m 'Green'
}
function Falhou([string]$m) {
    $script:Falhas += $m
    Log "[FALHOU] $m" 'Red'
}
function Aviso([string]$m) { Log $m 'Yellow' }
function Titulo([string]$m) { Write-Host "  $m" -ForegroundColor Cyan }

function Set-Power([string[]]$cmd) {
    # O powercfg devolve codigo diferente de zero quando um alias nao existe.
    # Sem esta verificacao o erro aparecia cru no registo do app.
    $o = & powercfg @cmd 2>&1
    if ($LASTEXITCODE -ne 0) { Aviso "powercfg $($cmd[1]) falhou: $o" }
}
# =====================================================================
#  CATEGORIAS
# =====================================================================
$TodasCategorias = @(
    'interface','telemetria','update','indexacao','gravacao',
    'desempenho','apps','inicializacao','defender','desktop','processos'
)

function Get-Cats {
    switch ($Perfil) {
        'nuvem' { return @($TodasCategorias) }
        'leve'  { return @('interface','telemetria','indexacao','gravacao','apps','inicializacao','defender','desktop','processos') }
        default {
            if (-not $Categorias) { return @($TodasCategorias) }
            return @($Categorias -split ',' | ForEach-Object { $_.Trim().ToLower() } | Where-Object { $_ -in $TodasCategorias })
        }
    }
}
$Cats = Get-Cats
function CAtiva([string]$c) { return ($Cats -contains $c) }

# =====================================================================
#  LISTAS
# =====================================================================

# --- Servicos de telemetria / coleta / peso ---
$ServicosTelemetria = @(
    'DiagTrack','dmwappushservice','diagnosticshub.standardcollector.service',
    'WerSvc','WSearch','DoSvc','DusmSvc','CDPSvc','TabletInputService',
    'UevAgentService','WbioSrvc','SensorService',
    'IntelCollectorService','IntelTelemetryAgent','DSAService','DSAUpdateService',
    'SystemUsageReportSvc_QUEENCREEK','ESRV_SVC_QUEENCREEK',
    'cbdhsvc_*','whesvc','PcaSvc','RasMan','lfsvc','MapsBroker','RetailDemo',
    'AcerQAAgentSvis','AcerDIAgentSvis','AcerGAICameraService','AcerPixyService',
    'AcerServiceSvc','ASMSvc','AcerARTAIMMXService','AcerARTAIMMXDriverService',
    'AcerDeviceEnablingServiceV2',
    # NAO entram: AcerCCAgentSvis (Acer Care Center) e AcerEZSvc (Experience Zone)
    'AAADSvc','ADESv2Svc','ADESv2BW','ipfsvc','jhi_service','identity_helper'
)

# --- Servicos de Windows Update (PAUSAR, nao desabilitar de vez) ---
$ServicosUpdate = @('wuauserv','UsoSvc','WaaSMedicSvc')

# --- Servicos que so pesam e nao servem para o jogo ---
$ServicosPeso = @(
    'AppXSvc','InstallService','StateRepository','SysMain','Spooler',
    'PrintWorkflowUserSvc_*','WpnService','WpnUserService_*','CDPUserSvc_*',
    'WpcMonSvc','camsvc','DPS','GameInputSvc','GamingServices','GamingServicesNet',
    'PresentMonService','PresentMonSharedService','NPSMSvc_*'
)

# --- Processos que NUNCA podem ser mortos ---
$Essenciais = @(
    'System','Idle','Registry','Memory Compression','Secure System','smss','csrss',
    'wininit','winlogon','services','lsass','lsm','svchost','dwm','fontdrvhost',
    'ctfmon','sihost','LogonUI','conhost','WmiPrvSE','mpssvc','RuntimeBroker',
    'backgroundTaskHost','audiodg','AudioSrv','AudioEndpointBuilder','RtkAudUService',
    'RtkAudUService64','NahimicService','IntelAudioService','TextInputHost','InputApp',
    'RdpClient','WlanSvc','WlanPreApp','NlaSvc','dns','dhcp','Dhcp','Dnscache',
    'netprofm','SystemEventsBroker','SecurityHealth','SecurityHealthSystray',
    'LockApp','explorer','ShellHost','ShellExperienceHost','StartMenuExperienceHost',
    'ApplicationFrameHost','Widgets','WidgetBoard',
    'nvcontainer','nvdisplaycontainer','NVDisplay.ContainerLocalSystem',
    'IntelGraphicsSoftware.Service',
    # O WebView2 e o motor de interface da Epic, do Roblox, do Windows 11 e
    # de todas as apps modernas. Se for morto ou bloqueado, a janela fica
    # PRETA e a app nao funciona. NUNCA tocar nestes.
    'msedgewebview2','msedgewebview2.exe',
    'ModoJogo','FortniteClient-Win64-Shipping','powershell','pwsh',
    'EasyAntiCheat','EasyAntiCheat_EOS','BEService','BEServiceLauncher','GameGuard',
    'GameMon.des','XignCode','nProt','GameGuard.des','EasyHook','ACGameService',
    'EpicGamesLauncher','EpicWebHelper','EpicOnlineServices','EpicAppLauncher',
    'EpicGames','CrashReportClient','steam','steamwebhelper','steamservice',
    'steamerrorreporter','Battle.net','Agent.exe','BlizzardUpdate','Wow',
    'UbisoftConnect','upc','UbisoftExtension','EA','EAApp','EADesktop',
    'EABackgroundService','Origin','OriginWebHelper','GOGGalaxy','GalaxyClient',
    'GOGGame','itch','ItchSetup','RiotClientServices','RiotClient','RiotClientUx',
    'LeagueClient','LeagueClientUx','Valorant','RiotGames','Garena','xlive',
    'RockstarService','RockstarLauncher','RobloxPlayerBeta','RobloxPlayerLauncher',
    'RobloxPlayer','Fortnite','FortniteLauncher','Nebula','NebulaClient',
    'obs64','obs32','obs','GameBar','GameBarPresenceWriter','GameInputRedistService',
    'GameInputSvc','Windows10UniversalSer','MpCmdRun','MpDefenderCoreService'
)

# --- Programas bloqueados via IFEO (nao voltam sozinhos) ---
$Bloqueados = @(
    'msedge.exe',
    'SearchHost.exe','SearchApp.exe','StartMenuExperienceHost.exe',
    'ShellExperienceHost.exe','ApplicationFrameHost.exe',
    'OneDrive.exe','OneDrive.Sync.Service.exe','Dropbox.exe',
    'SearchIndexer.exe','SearchProtocolHost.exe','SearchFilterHost.exe',
    'Widgets.exe','WidgetService.exe','WidgetBoard.exe','Copilot.exe','mscopilot.exe',
    'AADPluginHost.exe',
    'PhoneExperienceHost.exe','YourPhone.exe','MicrosoftTeams.exe','ms-teams.exe',
    'Teams.exe','MicrosoftMaps.exe','Weather.exe','FeedbackHub.exe',
    'GetHelp.exe','MicrosoftSolitaireCollection.exe','StickyNotes.exe',
    'MixedReality.Portal.exe','Microsoft.XboxApp.exe','XboxPcApp.exe',
    'Clipchamp.exe','MicrosoftClipchamp.exe','GamingApp.exe','XboxGip',
    'IntelGraphicsSoftware.Service.exe','IntelGraphicsSoftware.Overlay.exe',
    'IntelGraphicsSoftware.exe','IntelGraphicsSoftware.Service.Graphics.exe',
    'AcerQAAgent.exe','AcerDIAgent.exe','AcerGAICameraService.exe','AcerPixyService.exe',
    'AcerService.exe','AcerServiceWrapper.exe','AcerSysHardwareService.exe',
    'AcerSystemCentralService.exe','AAADSvc.exe','ADESv2Svc.exe','ADESv2BW.exe',
    'ipfsvc.exe','jhi_service.exe','identity_helper.exe','MicrosoftEdgeElevationService.exe'
)

# --- Tarefas agendadas de telemetria ---
$TarefasTelemetria = @(
    'Microsoft Compatibility Appraiser','CompatTelRunner','ProgramDataUpdater',
    'Consolidator','UsbCeip','KernelCeipTask','Appraiser','Proxy','DiskDiagnostic',
    'SystemInformation','MareBackup','PcaPatchDbTask','SdbinstMergeDbTask',
    'StartupAppTask','CleanupTemporaryState','DsSvcCleanup','appuriverifier',
    'RecoverabilityToastTask','BackgroundTaskHandler','CorporateWorkloadSyndication',
    'DmClient','DmClientOnScenarioDownload','Scheduled Defrag','Consolidator'
)

# --- Atalhos da area de trabalho que DEVEM CONTINUAR visiveis ---
$DesktopManter = @(
    'modo jogo','epic','steam','roblox','battle','ubisoft','origin','ea app','gog',
    'xbox','rockstar','riot','garena','itch','minecraft','fortnite','valorant',
    'league of legends',' lol','cs2','counter-strike','apex','overwatch','gta',
    'grand theft','fifa','efootball','rocket league','genshin','honkai','zenless',
    'wuthering','naruto','one piece','jujutsu','dragon ball','yu-gi-oh','hades',
    'balatro','stardew','terraria','palworld','rust','dayz','ark','tekken',
    'street fighter','mortal kombat','forza','assassin','watch dogs','cyberpunk',
    'death stranding','ghostrunner','celeste','hollow knight','dead cells',
    'sea of thieves','subnautica','v rising','nebula','geforce','game','jogo'
)

# =====================================================================
#  SNAPSHOT - o que sera alterado, com o valor ANTIGO de cada coisa
# =====================================================================
$script:Snap = [ordered]@{
    Versao          = 4
    Data            = (Get-Date).ToString('o')
    Perfil          = $Perfil
    Categorias      = ($Cats -join ',')
    Restaurado      = $false
    Servicos        = @()
    Tarefas         = @()
    Registro        = @()
    ChavesCriadas   = @()
    PlanoEnergia    = ''
    PlanoCriado     = ''
    Defender        = [ordered]@{ ExclusoesCaminho = @(); ExclusoesProcesso = @() }
    Inicializacao   = @()
    AreaTrabalho    = @()
    Ifeo            = @()
    WallpaperOriginal = ''
    ProcessosMortos = @()
}

function Salvar-Snapshot {
    try {
        $json = $script:Snap | ConvertTo-Json -Depth 8
        Set-Content -Path $ArquivoSnapshot -Value $json -Encoding UTF8
        return $true
    } catch {
        Falhou "nao consegui gravar o snapshot: $($_.Exception.Message)"
        return $false
    }
}

function Add-Reg([string]$caminho, [string]$nome) {
    $existe = $false; $valor = $null; $tipo = 'DWord'
    $item = Get-Item -Path $caminho -ErrorAction SilentlyContinue
    if ($item) {
        $v = $item.GetValue($nome, $null)
        if ($null -ne $v) {
            $existe = $true
            $valor  = $v
            try { $tipo = $item.GetValueKind($nome).ToString() } catch { $tipo = 'String' }
        }
    }
    $script:Snap.Registro += [pscustomobject]@{
        Caminho = $caminho; Nome = $nome; Existia = $existe; Valor = $valor; Tipo = $tipo
    }
}

function Add-ChaveCriada([string]$caminho) {
    if (-not (Test-Path $caminho)) {
        $script:Snap.ChavesCriadas += $caminho
    }
}

function Add-Servico([string]$nome) {
    $w = Get-CimInstance Win32_Service -Filter "Name='$nome'" -ErrorAction SilentlyContinue
    if (-not $w) { return $false }
    $script:Snap.Servicos += [pscustomobject]@{
        Nome     = $w.Name
        StartMode= $w.StartMode
        Running  = ($w.State -eq 'Running')
        Estado   = $w.State
    }
    return $true
}

function Add-Inic([string]$caminho, [string]$nome, $valor) {
    $script:Snap.Inicializacao += [pscustomobject]@{
        Caminho = $caminho; Nome = $nome; Valor = $valor
    }
}

# =====================================================================
#  ESCRITAS (todas registam o valor antigo no snapshot antes de mexer)
# =====================================================================
function Set-Reg([string]$caminho, [string]$nome, $valor, [string]$tipo = 'DWord') {
    Add-Reg $caminho $nome
    if ($DryRun) { Log "registo: $caminho\$nome = $valor  (seria alterado)" 'DarkCyan'; return }
    if (-not (Test-Path $caminho)) {
        Add-ChaveCriada $caminho
        New-Item -Path $caminho -Force | Out-Null
    }
    # -Type e um parametro dinamico do provider de registo: so funciona com caminho de registo
    Set-ItemProperty -Path $caminho -Name $nome -Value $valor -Type $tipo -Force
}

function Remover-Reg([string]$caminho, [string]$nome) {
    Add-Reg $caminho $nome
    if ($DryRun) { Log "registo: removeria $caminho\$nome" 'DarkCyan'; return }
    Remove-ItemProperty -Path $caminho -Name $nome -Force -ErrorAction SilentlyContinue
}

function Parar-Servico([string]$nome, [string]$startMode = 'Disabled') {
    if (-not (Add-Servico $nome)) { return }
    if ($DryRun) { Log "servico: $nome -> pararia ($startMode)" 'DarkCyan'; return }
    Stop-Service $nome -Force -ErrorAction SilentlyContinue
    Set-Service $nome -StartupType $startMode -ErrorAction SilentlyContinue
}

function Desativar-Tarefa($t) {
    $script:Snap.Tarefas += [pscustomobject]@{ Nome = $t.TaskName; Caminho = $t.TaskPath }
    if ($DryRun) { Log "tarefa: $($t.TaskPath)$($t.TaskName) -> desativaria" 'DarkCyan'; return }
    Disable-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath -ErrorAction SilentlyContinue | Out-Null
}

function Set-Ifeo([string]$exe) {
    $script:Snap.Ifeo += $exe
    $ifeo = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'
    if ($DryRun) { Log "bloqueio: $exe -> nao iniciaria" 'DarkCyan'; return }
    $chave = "$ifeo\$exe"
    if (-not (Test-Path $chave)) { New-Item -Path $chave -Force | Out-Null }
    Set-ItemProperty -Path $chave -Name 'Debugger' -Value 'x:\bloqueado.exe' -Type String
}

# =====================================================================
#  WALLPAPER
# =====================================================================
function Get-WallpaperAtual {
    try { return (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name WallPaper -ErrorAction SilentlyContinue).WallPaper } catch { return $null }
}
function Set-Wallpaper([string]$caminho) {
    if (-not $caminho -or -not (Test-Path $caminho)) { return $false }
    if ($DryRun) { Log "wallpaper: aplicaria $caminho" 'DarkCyan'; return $true }
    $r = [Wallpaper]::SystemParametersInfo([Wallpaper]::SET, 0, $caminho, ([Wallpaper]::UPDATE -bor [Wallpaper]::SEND))
    Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name WallpaperStyle -Value '10' -Type String
    Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name TileWallpaper  -Value '0'  -Type String
    return ($r -ge 1)
}
function Set-Barra([bool]$visivel) {
    $t = [Taskbar]::FindWindow('Shell_TrayWnd', $null)
    if ($t -eq [IntPtr]::Zero) { return $false }
    if ($DryRun) { return $true }
    if ($visivel) { [void][Taskbar]::ShowWindow($t, 5) } else { [void][Taskbar]::ShowWindow($t, 0) }
    return $true
}
function Salvar-WallpaperOriginal {
    if ($script:Snap.WallpaperOriginal) { return }
    $a = Get-WallpaperAtual
    if ($a) { $script:Snap.WallpaperOriginal = $a }
}
function Restaurar-Wallpaper {
    $destino = $script:Snap.WallpaperOriginal
    if (-not $destino -or -not (Test-Path $destino)) {
        $destino = @(
            "$BaseDir\wallpapers\normal.jpg",
            "$env:SystemRoot\Web\Wallpaper\Windows\img0.jpg"
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $destino) { return "nao foi possivel restaurar o wallpaper" }
    if ($DryRun) { return "restauraria o wallpaper: $(Split-Path $destino -Leaf)" }
    [Wallpaper]::SystemParametersInfo([Wallpaper]::SET, 0, $destino, ([Wallpaper]::UPDATE -bor [Wallpaper]::SEND)) | Out-Null
    return "wallpaper restaurado: $(Split-Path $destino -Leaf)"
}

# =====================================================================
#  AREA DE TRABALHO - esconder tudo menos o app, Epic, Steam e jogos
# =====================================================================
function Get-DesktopDirs {
    $dirs = @()
    $u = [Environment]::GetFolderPath('Desktop')
    if ($u -and (Test-Path $u)) { $dirs += $u }
    if (Test-Path "$env:PUBLIC\Desktop") { $dirs += "$env:PUBLIC\Desktop" }
    return $dirs
}
function Quer-Manter([string]$nome) {
    $n = $nome.ToLower()
    if ($n -eq 'modo jogo' -or $n -eq 'modo jogo.lnk') { return $true }
    foreach ($p in $DesktopManter) { if ($n -like "*$p*") { return $true } }
    return $false
}
function Esconder-Desktop {
    New-Item $GuardDesktop -ItemType Directory -Force | Out-Null
    $movidos = 0
    foreach ($dir in (Get-DesktopDirs)) {
        foreach ($f in @(Get-ChildItem $dir -File -Filter *.lnk -Force -ErrorAction SilentlyContinue)) {
            if (Quer-Manter $f.Name) { continue }
            $dest = Join-Path $GuardDesktop $f.Name
            $script:Snap.AreaTrabalho += [pscustomobject]@{
                Origem = $f.FullName; Destino = $dest; Nome = $f.Name
            }
            if ($DryRun) { Log "area de trabalho: esconderia '$($f.Name)'" 'DarkCyan'; $movidos++; continue }
            try {
                if (Test-Path $dest) { Remove-Item $dest -Force -ErrorAction SilentlyContinue }
                Move-Item $f.FullName $dest -Force -ErrorAction SilentlyContinue
                if (-not (Test-Path $f.FullName)) { $movidos++ }
            } catch { Falhou "nao consegui esconder '$($f.Name)': $($_.Exception.Message)" }
        }
    }
    if ($movidos -gt 0) { Fez "$movidos atalho(s) da area de trabalho escondidos (app, Epic, Steam e jogos ficaram)" }
}
function Restaurar-Desktop {
    if (-not (Test-Path $GuardDesktop)) { return }
    $n = 0
    foreach ($m in $script:Snap.AreaTrabalho) {
        if (-not (Test-Path $m.Destino)) { continue }
        try {
            if (Test-Path $m.Origem) { Remove-Item $m.Origem -Force -ErrorAction SilentlyContinue }
            Move-Item $m.Destino $m.Origem -Force -ErrorAction SilentlyContinue
            $n++
        } catch { Falhou "nao consegui restaurar '$($m.Nome)'" }
    }
    if ($n -gt 0) { Fez "$n atalho(s) da area de trabalho de volta" }
}

# =====================================================================
#  ATIVAR
# =====================================================================
function Ativar {
    Clear-Host
    Write-Host ""
    Write-Host "   MODO JOGO LIMPO" -ForegroundColor Green
    Write-Host "  ============================================================" -ForegroundColor DarkGray
    Write-Host "   Perfil: $Perfil   $($Cats -join ', ')" -ForegroundColor DarkGray
    if ($DryRun) { Write-Host "   *** MODO ENSAIO (DryRun): nada sera alterado ***" -ForegroundColor Magenta }
    Write-Host ""

    $antes = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB, 2)
    $procAntes = @(Get-Process).Count
    Write-Host "  RAM livre: $antes GB   |   processos: $procAntes" -ForegroundColor Gray
    Write-Host ""

    # ---------- 0) SNAPSHOT ----------
    Titulo "[0/9] Gravando o estado do sistema (para desfazer tudo depois)..."
    Salvar-WallpaperOriginal
    # Matches.Value devolve uma STRING: indexar com [0] dava o primeiro CARACTERE
    # em vez do GUID inteiro. Forca-se um array antes de indexar.
    $planos = @((powercfg /getactivescheme | Select-String -Pattern '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}').Matches.Value)
    if ($planos.Count -gt 0) { $script:Snap.PlanoEnergia = $planos[0] }
    try {
        $pref = Get-MpPreference -ErrorAction Stop
        $script:Snap.Defender.ExclusoesCaminho  = @($pref.ExclusionPath)
        $script:Snap.Defender.ExclusoesProcesso = @($pref.ExclusionProcess)
    } catch { }
    # inicializacao
    foreach ($k in @('HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
                     'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run')) {
        $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
        if ($p) {
            foreach ($prop in @($p.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' })) {
                Add-Inic $k $prop.Name $prop.Value
            }
        }
    }
    if (-not (Salvar-Snapshot)) {
        Write-Host ""
        Write-Host "  [ABORTADO] Nao consegui gravar o snapshot. Nada foi alterado." -ForegroundColor Red
        Set-Content $ArquivoResultado -Value "ERRO: snapshot nao gravado" -Encoding UTF8
        return
    }
    Fez "estado base do sistema gravado em $(Split-Path $ArquivoSnapshot -Leaf)"

    # ---------- 1) INTERFACE DO WINDOWS 11 ----------
    if (CAtiva 'interface') {
        Titulo "[1/9] Sessao limpa do Windows 11..."
        Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'TaskbarDa' 0
        Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'TaskbarChat' 0
        Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'TaskbarPeople' 0
        Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'Start_TrackDocs' 0
        Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Shishimmer\Sidebar' 'AllowWidgets' 0
        Set-Reg 'HKCU:\Software\Microsoft\Policies\Microsoft\Windows\Windows Search' 'AllowCortana' 0
        Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Windows Search' 'Disabled' 1
        Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings' 'ToastEnabled' 0
        Set-Barra $false | Out-Null
        Fez "barra de tarefas escondida, widgets/chat/pesquisa/Cortana/notificacoes desligados"
    }

    # ---------- 2) TELEMETRIA E SERVICOS DE COLETA ----------
    if (CAtiva 'telemetria') {
        Titulo "[2/9] Telemetria e coleta de dados..."
        $n = 0
        foreach ($s in $ServicosTelemetria) {
            foreach ($real in @(Get-Service $s -ErrorAction SilentlyContinue)) {
                if (Add-Servico $real.Name) { Parar-Servico $real.Name 'Disabled'; $n++ }
            }
        }
        $nt = 0
        foreach ($t in @(Get-ScheduledTask -ErrorAction SilentlyContinue)) {
            $nome = "$($t.TaskPath)$($t.TaskName)"
            foreach ($pat in $TarefasTelemetria) {
                if ($nome -like "*$pat*" -and $t.State -ne 'Disabled') { Desativar-Tarefa $t; $nt++; break }
            }
        }
        Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry' 0
        Set-Reg 'HKCU:\Software\Policies\Microsoft\Windows\CloudContent' 'DisableWindowsConsumerFeatures' 1
        Add-ChaveCriada 'HKLM:\SYSTEM\CurrentControlSet\Control\WMI\Autologger\AutoLogger-DiagTrack-Listener'
        Set-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\WMI\Autologger\AutoLogger-DiagTrack-Listener' 'Start' 0
        $bg = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications'
        foreach ($app in @(Get-ChildItem $bg -ErrorAction SilentlyContinue)) {
            Add-Reg $app.PSPath 'Disabled'; Add-Reg $app.PSPath 'DisabledByUser'
            if (-not $DryRun) {
                Set-ItemProperty $app.PSPath -Name 'Disabled' -Value 1 -Type DWord -ErrorAction SilentlyContinue
                Set-ItemProperty $app.PSPath -Name 'DisabledByUser' -Value 1 -Type DWord -ErrorAction SilentlyContinue
            }
        }
        Fez "$n servicos de coleta desligados, $nt tarefas agendadas desativadas, telemetria = 0"
    }

    # ---------- 3) WINDOWS UPDATE (PAUSAR) ----------
    if (CAtiva 'update') {
        Titulo "[3/9] Pausando o Windows Update..."
        $n = 0
        foreach ($s in $ServicosUpdate) {
            if (Add-Servico $s) { Parar-Servico $s 'Manual'; $n++ }
        }
        Fez "$n servico(s) do Update pausados (voltam com o estado original no MODO NORMAL)"
    }

    # ---------- 4) GRAVACAO DE TELA (Game DVR) ----------
    if (CAtiva 'gravacao') {
        Titulo "[4/9] Gravacao de tela desligada (Game DVR)..."
        Set-Reg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled' 0
        Set-Reg 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled' 0
        Set-Reg 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR' 'HistoricalCaptureEnabled' 0
        Set-Reg 'HKCU:\SOFTWARE\Microsoft\GameBar' 'AllowAutoGameMode' 1
        Set-Reg 'HKCU:\SOFTWARE\Microsoft\GameBar' 'AutoGameModeEnabled' 1
        Fez "Game DVR off, Game Mode on (menos gravacao em segundo plano, mais FPS)"
    }

    # ---------- 5) DESEMPENHO E ENERGIA ----------
    if (CAtiva 'desempenho') {
        Titulo "[5/9] Plano de energia e desempenho..."
        $maxGuid = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'
        # O plano "Alto desempenho" (8c5e7fda) e o plano de origem. Se ainda nao
        # existir na lista, duplica-se e guarda-se o GUID NOVO que o Windows cria.
        $listaPlanos = (powercfg /list | Out-String)
        $temMax = $listaPlanos -match [regex]::Escape($maxGuid)
        if (-not $temMax) {
            if ($DryRun) { Log "energia: criaria o plano 'Desempenho Maximo'" 'DarkCyan' }
            else {
                $saida = powercfg /duplicatescheme $maxGuid 2>&1 | Out-String
                $criado = ([regex]::Match($saida, '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')).Value
                if ($criado) { $maxGuid = $criado; $script:Snap.PlanoCriado = $criado; $temMax = $true }
            }
        }
        if ($temMax -and -not $DryRun) {
            # O powercfg devolve codigo diferente de zero quando um alias nao
            # existe. Cada comando e verificado para nao despejar erros
            # SUB_USB nao e um alias valido: usa-se o GUID do subgrupo USB.
            Set-Power @("/setacvalueindex", $maxGuid, "2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", "0")
            Set-Power @("/setactive", $maxGuid)
        } elseif ($DryRun) {
            Log "energia: usaria o plano 'Desempenho Maximo'" 'DarkCyan'
        }
        Fez "plano 'Desempenho Maximo' ativo (CPU minima a 100%, PCIe e USB sem suspensao)"
    }

    # ---------- 6) DEFENDER ----------
    if (CAtiva 'defender') {
        Titulo "[6/9] Antivirus..."
        $excluiu = 0
        foreach ($e in @("$env:USERPROFILE\Epic Games", "$env:ProgramFiles(x86)\Steam", "${env:ProgramFiles(x86)}\Steam")) {
            if (Test-Path $e) { if (-not $DryRun) { Add-MpPreference -ExclusionPath $e -ErrorAction SilentlyContinue }; $excluiu++ }
        }
        if (-not $DryRun) { Add-MpPreference -ExclusionProcess 'FortniteClient-Win64-Shipping.exe' -ErrorAction SilentlyContinue }
        Fez "$excluiu pasta(s) liberadas da varredura (Epic/Steam)"

        if (-not $DryRun) {
            foreach ($p in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableIOAVProtection','DisableScriptScanning','DisableArchiveScanning') {
                Set-MpPreference -Property $p -Value $true -ErrorAction SilentlyContinue
            }
            $st = Get-MpComputerStatus -ErrorAction SilentlyContinue
            if ($st.RealTimeProtectionEnabled -eq $false) {
                Fez "tempo real do Defender DESLIGADO"
            } elseif ($st.IsTamperProtected) {
                Aviso "tempo real continua LIGADO: a Protecao Contra Violacao bloqueou a alteracao."
                Aviso "  Seguranca do Windows > Protecao contra virus e ameacas > Gerenciar definicoes > Protecao contra violacao = Desativado"
            } else {
                Aviso "tempo real continua LIGADO (falha desconhecida ao desativar)"
            }
        } else {
            Log "defender: desativaria o tempo real (com exclusoes)" 'DarkCyan'
        }
    }

    # ---------- 7) INICIALIZACAO ----------
    if (CAtiva 'inicializacao') {
        Titulo "[7/9] Itens de inicializacao..."
        $n = 0
        foreach ($i in $script:Snap.Inicializacao) {
            if ($i.Nome -match 'Steam|Epic|GeForce|NVIDIA|AMD|Razer|Logitech|Realtek|Rtk|Audio|Nebula|Roblox') { continue }
            if ($DryRun) { Log "inicializacao: desativaria '$($i.Nome)'" 'DarkCyan'; $n++; continue }
            Remove-ItemProperty -Path $i.Caminho -Name $i.Nome -Force -ErrorAction SilentlyContinue
            $n++
        }
        Fez "$n item(ns) de inicializacao adiados"
    }

    # ---------- 8) BLOQUEAR PROGRAMAS (IFEO) ----------
    if (CAtiva 'interface') {
        Titulo "[8/9] Bloqueando o retorno de programas..."
        $n = 0
        foreach ($b in $Bloqueados) { Set-Ifeo $b; $n++ }
        Fez "$n programas bloqueados (nao voltam sozinhos)"
    }

    # ---------- 9) AREA DE TRABALHO + PROCESSOS ----------
    if (CAtiva 'desktop') {
        Titulo "[9/9] Area de trabalho e processos..."
        Esconder-Desktop
    }
    if (CAtiva 'processos') {
        Titulo "     Parando servicos que so pesam..."
        $nPeso = 0
        foreach ($s in $ServicosPeso) {
            foreach ($real in @(Get-Service $s -ErrorAction SilentlyContinue)) {
                if ($real.Status -eq 'Running') {
                    if (Add-Servico $real.Name) { Parar-Servico $real.Name 'Manual'; $nPeso++ }
                }
            }
        }
        if ($nPeso -gt 0) { Fez "$nPeso servico(s) de fundo parados (cada um economiza 1 processo)" }
        Matar-Processos
    }

    # ---------- wallpaper no fim ----------
    if ($Wallpaper) {
        if (Set-Wallpaper $Wallpaper) { Fez "wallpaper aplicado: $(Split-Path $Wallpaper -Leaf)" }
        else { Aviso "nao consegui aplicar o wallpaper" }
    }

    # ---------- fim ----------
    if (-not $DryRun) {
        Set-Content -Path $MarcadorAtivo -Value (Get-Date).ToString() -Encoding UTF8
        Salvar-Snapshot
    }
    $depois = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB, 2)
    $procDepois = @(Get-Process).Count
    Write-Host ""
    Write-Host "  ===========================================================" -ForegroundColor Green
    if ($DryRun) {
        Write-Host "   ENSAIO CONCLUIDO - nada foi alterado" -ForegroundColor Magenta
        Write-Host "   Seria desligado: $($script:Feitos) acoes" -ForegroundColor DarkGray
    } else {
        Write-Host "   MODO JOGO LIMPO ATIVO" -ForegroundColor Green
        Write-Host "   Processos: $procAntes -> $procDepois   |   RAM livre: $antes -> $depois GB" -ForegroundColor DarkGray
    }
    if ($script:Falhas.Count -gt 0) {
        Write-Host "   Falhas: $($script:Falhas.Count)" -ForegroundColor Yellow
    }
    Write-Host "  ===========================================================" -ForegroundColor Green
    Set-Content $ArquivoResultado -Value "OK" -Encoding UTF8
}

function Matar-Processos {
    $svcPids = @{}
    foreach ($s in @(Get-CimInstance Win32_Service -ErrorAction SilentlyContinue | Where-Object { $_.ProcessId -gt 0 })) {
        $svcPids[[int]$s.ProcessId] = $s.Name
    }
    $alvo = @(); $mb = 0
    foreach ($p in @(Get-Process -ErrorAction SilentlyContinue)) {
        $n = $p.ProcessName
        if ($Essenciais -contains $n) { continue }
        if ($Manter -and ($Manter -split ',' | ForEach-Object { $_.Trim() }) -contains $n) { continue }
        $eServico = $false
        if ($svcPids.ContainsKey([int]$p.Id)) { $eServico = $true }
        $caminho = $null; try { $caminho = $p.Path } catch { }
        $eWindows = ($caminho -and $caminho -like "$env:SystemRoot\*")
        if ($eServico -and -not ($MatarSempre -contains $n)) { continue }
        if ($eWindows -and -not ($MatarSempre -contains $n)) { continue }
        if ($p.WorkingSet64 -lt 6MB) { continue }
        $alvo += $p; $mb += $p.WorkingSet64
    }
    if (-not $DryRun) {
        foreach ($p in $alvo) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
    }
    $nomes = @($alvo | Group-Object ProcessName | Sort-Object Count -Descending | Select-Object -First 6 |
                ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ', '
    Fez "$($alvo.Count) processos fechados (~$([math]::Round($mb/1MB)) MB) - $nomes"
}

# =====================================================================
#  PROCESSOS QUE DEVEM SER FECHADOS MESMO SENDO SERVICOS DO WINDOWS
#  (bloatware Acer, overlay Intel, servicos do Defender, etc.)
# =====================================================================
$MatarSempre = @(
    'AcerQAAgent','AcerDIAgent','AcerGAICameraService','AcerPixyService',
    'AcerService','AcerServiceWrapper','AcerSysHardwareService','AcerSystemCentralService',
    'AcerSense','AcerCareCenter',
    'IntelGraphicsSoftware','IntelGraphicsSoftware.Overlay','IntelGraphicsSoftware.Service',
    'IntelGraphicsSoftware.Service.Graphics','IntelCpHDCPSvc',
    'gamingservices','gamingservicesnet','PresentMonService','PresentMonSharedService',
    'mscopilot','Copilot','MicrosoftTeams','ms-teams','Teams',
    'OneDrive','OneDrive.Sync.Service','Dropbox','SearchIndexer','SearchProtocolHost','SearchFilterHost',
    'MicrosoftStartFeedProvider',
    'CrossDeviceService','CrossDeviceResume','UserOOBEBroker','AQAUserPS','ACCUserPS',
    'WUDFHost','WpcMon','SystemSettings','MpDefenderCoreService','MpCmdRun',
    'AAADSvc','ADESv2Svc','ADESv2BW','ipfsvc','ipf_uf','ipf_helper','jhi_service',
    'identity_helper','esrv','MicrosoftEdgeUpdate','MicrosoftEdgeElevationService',
    'PhoneExperienceHost','YourPhone','MicrosoftMaps','Weather','FeedbackHub','GetHelp',
    'MicrosoftSolitaireCollection','StickyNotes','MixedReality.Portal',
    'Microsoft.XboxApp','XboxPcApp','Clipchamp','MicrosoftClipchamp','GamingApp'
)

# =====================================================================
#  DESATIVAR - restaura TUDO a partir do snapshot
# =====================================================================
function Desativar {
    Clear-Host
    Write-Host ""
    Write-Host "   MODO NORMAL - restaurando" -ForegroundColor Cyan
    Write-Host "  ============================================================" -ForegroundColor DarkGray
    if ($DryRun) { Write-Host "   *** MODO ENSAIO (DryRun): nada sera alterado ***" -ForegroundColor Magenta }

    if (-not (Test-Path $ArquivoSnapshot)) {
        Write-Host "  [AVISO] Nao ha snapshot para restaurar. Vou so religar o basico." -ForegroundColor Yellow
        $script:Snap = [ordered]@{
            Versao=4; Data=''; Perfil=''; Categorias=''; Restaurado=$false
            Servicos=@(); Tarefas=@(); Registro=@(); ChavesCriadas=@()
            PlanoEnergia=''; PlanoCriado=''; Defender=[ordered]@{ExclusoesCaminho=@();ExclusoesProcesso=@()}
            Inicializacao=@(); AreaTrabalho=@(); Ifeo=@(); WallpaperOriginal=''; ProcessosMortos=@()
        }
    } else {
        try {
            $lido = Get-Content $ArquivoSnapshot -Raw -Encoding UTF8 | ConvertFrom-Json
            $script:Snap = [ordered]@{
                Versao=$lido.Versao; Data=$lido.Data; Perfil=$lido.Perfil; Categorias=$lido.Categorias
                Restaurado=$false
                Servicos=@($lido.Servicos); Tarefas=@($lido.Tarefas); Registro=@($lido.Registro)
                ChavesCriadas=@($lido.ChavesCriadas); PlanoEnergia=$lido.PlanoEnergia; PlanoCriado=$lido.PlanoCriado
                Defender=$lido.Defender; Inicializacao=@($lido.Inicializacao)
                AreaTrabalho=@($lido.AreaTrabalho); Ifeo=@($lido.Ifeo)
                WallpaperOriginal=$lido.WallpaperOriginal; ProcessosMortos=@($lido.ProcessosMortos)
            }
            Write-Host "  Snapshot de $($lido.Data) carregado." -ForegroundColor DarkGray
        } catch {
            Write-Host "  [ERRO] O snapshot esta corrompido. Nao vou mexer em nada." -ForegroundColor Red
            Set-Content $ArquivoResultado -Value "ERRO: snapshot corrompido" -Encoding UTF8
            return
        }
    }

    # --- 1) bloqueios IFEO ---
    Titulo "[1/9] Destravando os programas bloqueados..."
    $ifeo = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'
    $n = 0
    foreach ($b in (@($script:Snap.Ifeo) + $Bloqueados)) {
        if ($DryRun) { $n++; continue }
        if (-not $b) { continue }
        $chave = "$ifeo\$b"
        if (Test-Path $chave) {
            # Remove SO o bloqueio criado pelo modo jogo (Debugger = x:\bloqueado.exe).
            # Uma entrada IFEO que o utilizador ja tinha no mesmo exe fica intacta:
            # antes o restauro apagava o MicrosoftEdgeUpdate.exe do sistema.
            $dbg = (Get-ItemProperty -Path $chave -Name 'Debugger' -ErrorAction SilentlyContinue).Debugger
            if ($dbg -eq 'x:\bloqueado.exe') {
                Remove-Item -Path $chave -Recurse -Force -ErrorAction SilentlyContinue
                $n++
            }
        }
    }
    Fez "$n bloqueios removidos"

    # --- 2) chaves de registo ---
    Titulo "[2/9] Restaurando as chaves de registo..."
    $n = 0
    foreach ($r in @($script:Snap.Registro)) {
        if ($DryRun) { $n++; continue }
        if ($r.Existia) {
            if (-not (Test-Path $r.Caminho)) { New-Item -Path $r.Caminho -Force | Out-Null }
            New-ItemProperty -Path $r.Caminho -Name $r.Nome -Value $r.Valor -PropertyType $r.Tipo -Force -ErrorAction SilentlyContinue | Out-Null
        } else {
            Remove-ItemProperty -Path $r.Caminho -Name $r.Nome -Force -ErrorAction SilentlyContinue
        }
        $n++
    }
    Fez "$n valores de registo restaurados ao estado original"

    # --- 3) servicos ---
    Titulo "[3/9] Religando os servicos..."
    $n = 0; $nRod = 0
    foreach ($s in @($script:Snap.Servicos)) {
        if ($DryRun) { $n++; continue }
        Set-Service $s.Nome -StartupType $s.StartMode -ErrorAction SilentlyContinue
        if ($s.Running) { Start-Service $s.Nome -ErrorAction SilentlyContinue; $nRod++ }
        $n++
    }
    Fez "$n servicos restaurados ($nRod voltaram a rodar)"

    # --- 4) tarefas agendadas ---
    Titulo "[4/9] Reativando as tarefas agendadas..."
    $n = 0
    foreach ($t in @($script:Snap.Tarefas)) {
        if ($DryRun) { $n++; continue }
        Enable-ScheduledTask -TaskName $t.Nome -TaskPath $t.Caminho -ErrorAction SilentlyContinue | Out-Null
        $n++
    }
    Fez "$n tarefas agendadas reativadas"

    # --- 5) inicializacao ---
    Titulo "[5/9] Restaurando a inicializacao..."
    $n = 0
    foreach ($i in @($script:Snap.Inicializacao)) {
        if ($DryRun) { $n++; continue }
        if (-not (Test-Path $i.Caminho)) { continue }
        New-ItemProperty -Path $i.Caminho -Name $i.Nome -Value $i.Valor -PropertyType String -Force -ErrorAction SilentlyContinue | Out-Null
        $n++
    }
    Fez "$n itens de inicializacao de volta"

    # --- 6) area de trabalho ---
    Titulo "[6/9] Restaurando a area de trabalho..."
    if (-not $DryRun) { Restaurar-Desktop }

    # --- 7) antivirus ---
    Titulo "[7/9] Restaurando o antivirus..."
    if (-not $DryRun) {
        try {
            $pref = Get-MpPreference -ErrorAction Stop
            foreach ($e in @($pref.ExclusionPath | Where-Object { $_ -and $_.ToString().Trim() -ne '' -and $_ -notlike 'N/A*' })) {
                if ($e -and ($script:Snap.Defender.ExclusoesCaminho -notcontains $e)) {
                    Remove-MpPreference -ExclusionPath $e -ErrorAction SilentlyContinue
                }
            }
            foreach ($pr in @($pref.ExclusionProcess | Where-Object { $_ -and $_.ToString().Trim() -ne '' -and $_ -notlike 'N/A*' })) {
                if ($pr -and ($script:Snap.Defender.ExclusoesProcesso -notcontains $pr)) {
                    Remove-MpPreference -ExclusionProcess $pr -ErrorAction SilentlyContinue
                }
            }
            foreach ($e in @($script:Snap.Defender.ExclusoesCaminho | Where-Object { $_ -and $_.ToString().Trim() -ne '' }))  { Add-MpPreference -ExclusionPath $e -ErrorAction SilentlyContinue }
            foreach ($pr in @($script:Snap.Defender.ExclusoesProcesso | Where-Object { $_ -and $_.ToString().Trim() -ne '' })) { Add-MpPreference -ExclusionProcess $pr -ErrorAction SilentlyContinue }
        } catch { Falhou "nao consegui mexer nas exclusoes do Defender: $($_.Exception.Message)" }
        foreach ($p in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableIOAVProtection',
                       'DisableScriptScanning','DisableArchiveScanning','DisableRemovableDriveScanning') {
            Set-MpPreference -Property $p -Value $false -ErrorAction SilentlyContinue
        }
        Set-MpPreference -MAPSReporting 2 -ErrorAction SilentlyContinue
        Set-MpPreference -SubmitSamplesConsent 1 -ErrorAction SilentlyContinue
        foreach ($s in 'WinDefend','WdNisSvc','SecurityHealthService','Sense') {
            $sv = Get-Service $s -ErrorAction SilentlyContinue
            if ($sv -and $sv.Status -ne 'Running') { Start-Service $s -ErrorAction SilentlyContinue }
        }
        $st = Get-MpComputerStatus -ErrorAction SilentlyContinue
        if ($st.RealTimeProtectionEnabled) { Fez "antivirus: tempo real RELIGADO" }
        else { Falhou "antivirus: tempo real continua DESLIGADO (verifique a Protecao Contra Violacao)" }
    } else {
        Log "defender: religaria o tempo real e as exclusoes originais" 'DarkCyan'
    }

    # --- 8) energia ---
    Titulo "[8/9] Restaurando o plano de energia..."
    if ($script:Snap.PlanoEnergia) {
        if ($DryRun) { Log "energia: voltaria ao plano $($script:Snap.PlanoEnergia)" 'DarkCyan' }
        else { Set-Power @("/setactive", $script:Snap.PlanoEnergia); Fez "plano de energia original de volta" }
    }
    # Remove o plano "Desempenho Maximo" que o modo jogo criou (se criou).
    if ($script:Snap.PlanoCriado -and -not $DryRun) {
        # O plano pode ainda estar "em uso" durante uns instantes apos o setactive.
        Start-Sleep -Seconds 3
        $o = & powercfg /delete $script:Snap.PlanoCriado 2>&1
        if ($LASTEXITCODE -eq 0) { Log "plano 'Desempenho Maximo' temporario apagado" }
        else { Log "o plano temporario ficou (erro: $o)" 'DarkYellow' }
    }

    # --- 9) interface ---
    Titulo "[9/9] Trazendo a interface de volta..."
    if (-not $DryRun) {
        Set-Barra $true | Out-Null
        if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) {
            Start-Process explorer.exe
            Start-Sleep -Seconds 5
        }
        Set-ItemProperty 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings' -Name 'ToastEnabled' -Value 1 -Type DWord -ErrorAction SilentlyContinue
        Log (Restaurar-Wallpaper)
    }

    # --- chaves que criamos e ficaram vazias ---
    if (-not $DryRun) {
        foreach ($c in @($script:Snap.ChavesCriadas)) {
            try {
                $k = Get-Item $c -ErrorAction SilentlyContinue
                if ($k -and $k.ValueCount -eq 0 -and $k.SubKeyCount -eq 0) { Remove-Item $c -Recurse -Force -ErrorAction SilentlyContinue }
            } catch { }
        }
    }

    if (-not $DryRun) {
        $script:Snap.Restaurado = $true
        Salvar-Snapshot
        if (Test-Path $MarcadorAtivo) { Remove-Item $MarcadorAtivo -Force -ErrorAction SilentlyContinue }
    }
    $proc = @(Get-Process).Count
    Write-Host ""
    Write-Host "  ===========================================================" -ForegroundColor Cyan
    if ($DryRun) {
        Write-Host "   ENSAIO CONCLUIDO - nada foi alterado" -ForegroundColor Magenta
    } else {
        Write-Host "   MODO NORMAL ATIVO" -ForegroundColor Cyan
        Write-Host "   Processos agora: $proc   |   RAM livre: $([math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB,2)) GB" -ForegroundColor DarkGray
    }
    if ($script:Falhas.Count -gt 0) {
        Write-Host "   Pendencias: $($script:Falhas.Count)" -ForegroundColor Yellow
        foreach ($f in $script:Falhas) { Write-Host "     - $f" -ForegroundColor Yellow }
    }
    Write-Host "  ===========================================================" -ForegroundColor Cyan
    if (-not $DryRun) { Set-Content $ArquivoResultado -Value "OK" -Encoding UTF8 }
}

# =====================================================================
#  PESQUISA DO WINDOWS - desativar SEMPRE (independente do modo jogo)
#  Tem o seu proprio snapshot, para o MODO NORMAL nao mexer aqui.
# =====================================================================
$ArquivoBusca = "$Estado\busca.json"
$IfeoBusca = @('SearchHost.exe','SearchApp.exe','SearchFilterHost.exe','SearchProtocolHost.exe','SearchIndexer.exe')
$RegBusca = @(
    @{ Caminho = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search';              Nome = 'SearchboxTaskbarMode' }
    @{ Caminho = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search';              Nome = 'SearchboxTaskbarModeCache' }
    @{ Caminho = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced';  Nome = 'TaskbarSearchbox' }
    @{ Caminho = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search';           Nome = 'AllowCortana' }
    @{ Caminho = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search';           Nome = 'DisableWebSearch' }
    @{ Caminho = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search';           Nome = 'ConnectedSearchUseWeb' }
)

function Busca([string]$est) {
    Clear-Host
    Write-Host ""
    Write-Host "   PESQUISA DO WINDOWS" -ForegroundColor Cyan
    Write-Host "  ============================================================" -ForegroundColor DarkGray
    $ifeo = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'

    if ($est -eq 'on') {
        # --- guardar o estado atual ---
        $reg = @()
        foreach ($r in $RegBusca) {
            $existe = $false; $valor = $null; $tipo = 'DWord'
            $item = Get-Item -Path $r.Caminho -ErrorAction SilentlyContinue
            if ($item) {
                $v = $item.GetValue($r.Nome, $null)
                if ($null -ne $v) { $existe = $true; $valor = $v; try { $tipo = $item.GetValueKind($r.Nome).ToString() } catch { } }
            }
            $reg += [pscustomobject]@{ Caminho = $r.Caminho; Nome = $r.Nome; Existia = $existe; Valor = $valor; Tipo = $tipo }
        }
        $ifeoAtual = @()
        foreach ($b in $IfeoBusca) {
            $chave = "$ifeo\$b"
            if (Test-Path $chave) {
                $dbg = (Get-ItemProperty -Path $chave -Name 'Debugger' -ErrorAction SilentlyContinue).Debugger
                if ($dbg -eq 'x:\bloqueado.exe') { $ifeoAtual += $b }
            }
        }
        Set-Content -Path $ArquivoBusca -Value (@{ Registo = $reg; Ifeo = $ifeoAtual; Data = (Get-Date).ToString('o') } | ConvertTo-Json -Depth 6) -Encoding UTF8

        # --- esconder a caixa de pesquisa da barra de tarefas ---
        if (-not (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search')) { New-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Force | Out-Null }
        Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 2 -Type DWord -Force
        # o shell le o CACHE, nao o valor principal
        Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarModeCache' -Value 2 -Type DWord -Force
        Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarSearchbox' -Value 0 -Type DWord -Force
        if (-not (Test-Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search')) { New-Item 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Force | Out-Null }
        Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force
        Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'DisableWebSearch' -Value 1 -Type DWord -Force
        Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'ConnectedSearchUseWeb' -Value 0 -Type DWord -Force

        # --- bloquear e matar a pesquisa ---
        $n = 0
        foreach ($b in $IfeoBusca) {
            $chave = "$ifeo\$b"
            if (-not (Test-Path $chave)) { New-Item -Path $chave -Force | Out-Null }
            Set-ItemProperty -Path $chave -Name 'Debugger' -Value 'x:\bloqueado.exe' -Type String
            $n++
        }
        $mortos = 0
        foreach ($p in @(Get-Process -Name 'SearchHost','SearchApp','SearchIndexer','SearchProtocolHost','SearchFilterHost' -ErrorAction SilentlyContinue)) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; $mortos++ } catch { }
        }
        # O shell so relê a definicao quando o Explorer reinicia. Sem isto a
        # caixa "Pesquisar" continua na barra de tarefas.
        $explorer = Get-Process -Name explorer -ErrorAction SilentlyContinue
        if ($explorer) {
            foreach ($e in $explorer) { try { $e.Kill() } catch { } }
            Start-Sleep -Seconds 4
            if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
            Start-Sleep -Seconds 4
        }

        # O Explorer ao arrancar relanca a pesquisa. Mata-la outra vez.
        foreach ($p in @(Get-Process -Name 'SearchHost','SearchApp','SearchIndexer','SearchProtocolHost','SearchFilterHost' -ErrorAction SilentlyContinue)) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
        Write-Host "  Pesquisa do Windows DESATIVADA" -ForegroundColor Green
        Write-Host "    $n programas bloqueados | $mortos processos fechados" -ForegroundColor Gray
        Write-Host "    A barra de tarefas deixa de mostrar a caixa 'Pesquisar'." -ForegroundColor Gray
        Write-Host "    Para repor: o botao na pagina AJUSTES do app." -ForegroundColor DarkGray
    }
    else {
        if (-not (Test-Path $ArquivoBusca)) {
            Write-Host "  A pesquisa nunca foi desactivada por este app." -ForegroundColor Yellow
            return
        }
        $s = Get-Content $ArquivoBusca -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($r in @($s.Registo)) {
        # A caixa larga de pesquisa so volta com um sign-out (quiria do shell).
        # O modo 1 devolve o botao de lupa, que funciona igual e e o que se
        # consegue restaurar de forma confiavel.
        Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarMode' -Value 1 -Type DWord -Force
        Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'SearchboxTaskbarModeCache' -Value 1 -Type DWord -Force
        foreach ($r in @($s.Registo)) {
            if ($r.Nome -like 'SearchboxTaskbarMode*') { continue }
                if (-not (Test-Path $r.Caminho)) { New-Item -Path $r.Caminho -Force | Out-Null }
                New-ItemProperty -Path $r.Caminho -Name $r.Nome -Value $r.Valor -PropertyType $r.Tipo -Force -ErrorAction SilentlyContinue | Out-Null
            } else {
                Remove-ItemProperty -Path $r.Caminho -Name $r.Nome -Force -ErrorAction SilentlyContinue
            }
        }
        foreach ($b in @($s.Ifeo)) {
            $chave = "$ifeo\$b"
            if (Test-Path $chave) {
                $dbg = (Get-ItemProperty -Path $chave -Name 'Debugger' -ErrorAction SilentlyContinue).Debugger
                if ($dbg -eq 'x:\bloqueado.exe') { Remove-Item -Path $chave -Recurse -Force -ErrorAction SilentlyContinue }
            }
        }
        Remove-Item $ArquivoBusca -Force -ErrorAction SilentlyContinue
        $explorer = Get-Process -Name explorer -ErrorAction SilentlyContinue
        if ($explorer) {
            foreach ($e in $explorer) { try { $e.Kill() } catch { } }
            Start-Sleep -Seconds 4
            if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
            Start-Sleep -Seconds 3
        }
        Write-Host "  Pesquisa do Windows REPOSTA" -ForegroundColor Green
    }
    Write-Host "  ============================================================" -ForegroundColor DarkGray
    Write-Host ""
}
# =====================================================================
#  ESTADO
# =====================================================================
function Mostrar-Estado {
    Write-Host ""
    Write-Host "   ESTADO ATUAL DO PC" -ForegroundColor Cyan
    Write-Host "  ============================================================" -ForegroundColor DarkGray
    Write-Host "   Processos: $(@(Get-Process).Count)   |   RAM livre: $([math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB,2)) GB" -ForegroundColor Gray
    Write-Host "   Modo jogo: $(if (Test-Path $MarcadorAtivo) { 'ATIVO' } else { 'desligado' })" -ForegroundColor Gray
    if (Test-Path $ArquivoSnapshot) {
        try {
            $s = Get-Content $ArquivoSnapshot -Raw -Encoding UTF8 | ConvertFrom-Json
            Write-Host "   Snapshot: $($s.Data)  (perfil $($s.Perfil), $($s.Servicos.Count) servicos, $($s.Registro.Count) registos)" -ForegroundColor Gray
        } catch { Write-Host "   Snapshot: corrompido" -ForegroundColor Yellow }
    }
    Write-Host "  ============================================================" -ForegroundColor DarkGray
    Write-Host ""
}

# =====================================================================
#  EXECUCAO
# =====================================================================
switch ($Acao) {
    'Ativar'    { Ativar }
    'Desativar' { Desativar }
    'Estado'    { Mostrar-Estado }
    'Busca'     { Busca $EstadoBusca }
}









