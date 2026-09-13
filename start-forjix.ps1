[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = $PSScriptRoot
$migratorProject = Join-Path $repositoryRoot 'tools\Forjix.DatabaseMigrator\Forjix.DatabaseMigrator.csproj'
$webRoot = Join-Path $repositoryRoot 'web\forjix-web'

$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'

function Test-LocalPort([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        return $client.ConnectAsync('127.0.0.1', $Port).Wait(300)
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

if (Test-LocalPort 7235) {
    throw 'A porta 7235 já está em uso. Pare a depuração da API antes de iniciar tudo junto.'
}

if (Test-LocalPort 4200) {
    throw 'A porta 4200 já está em uso. Encerre o Angular atual antes de iniciar tudo junto.'
}

Write-Host 'Preparando bancos de dados...' -ForegroundColor Cyan
dotnet run --project $migratorProject
if ($LASTEXITCODE -ne 0) {
    throw 'A preparação do banco falhou. Confira os User Secrets do DatabaseMigrator.'
}

if (-not (Test-Path (Join-Path $webRoot 'node_modules'))) {
    Write-Host 'Instalando dependências do frontend...' -ForegroundColor Cyan
    npm.cmd ci --prefix $webRoot
    if ($LASTEXITCODE -ne 0) { throw 'A instalação do frontend falhou.' }
}

$apiProcess = $null
$webProcess = $null

try {
    Write-Host 'Iniciando API em https://localhost:7235...' -ForegroundColor Green
    $apiProcess = Start-Process dotnet -ArgumentList @(
    'run', '--project', 'src/Forjix.Api/Forjix.Api.csproj', '--launch-profile', 'https'
) -WorkingDirectory $repositoryRoot -NoNewWindow -PassThru

$apiReady = $false
foreach ($attempt in 1..60) {
    Start-Sleep -Milliseconds 500
    $apiProcess.Refresh()
    if ($apiProcess.HasExited) {
        throw "A API não iniciou e retornou o código $($apiProcess.ExitCode). Confira a saída acima."
    }

    if (Test-LocalPort 7235) {
        $apiReady = $true
        break
    }
}

if (-not $apiReady) {
    throw 'A API não abriu a porta 7235 dentro de 30 segundos.'
}

Write-Host 'Iniciando Angular em http://localhost:4200...' -ForegroundColor Green
$webProcess = Start-Process npm.cmd -ArgumentList @(
    'start'
) -WorkingDirectory $webRoot -NoNewWindow -PassThru

Write-Host 'Forjix iniciado. Pressione Ctrl+C para encerrar API e frontend.' -ForegroundColor Yellow

    while (-not $apiProcess.HasExited -and -not $webProcess.HasExited) {
        Start-Sleep -Milliseconds 500
        $apiProcess.Refresh()
        $webProcess.Refresh()
    }

    if ($apiProcess.HasExited) {
        throw "A API foi encerrada com o código $($apiProcess.ExitCode)."
    }

    throw "O frontend foi encerrado com o código $($webProcess.ExitCode)."
}
finally {
    foreach ($process in @($apiProcess, $webProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
