param([switch]$DatabaseOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Push-Location $taskRoot
try {
    if (!(Test-Path -LiteralPath '.env')) {
        $taskSecret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        "POSTGRES_PASSWORD=$taskSecret`nPOSTGRES_PORT=55432`n" | Set-Content -LiteralPath '.env' -Encoding utf8
    }
    $taskEnvironment = @{}
    foreach ($taskLine in Get-Content -LiteralPath '.env') {
        if ($taskLine -match '^([A-Z_]+)=(.*)$') { $taskEnvironment[$matches[1]] = $matches[2] }
    }
    docker compose up -d --wait db
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL startup failed.' }
    if ($DatabaseOnly) { return }
    $env:ConnectionStrings__DB = "Host=localhost;Port=$($taskEnvironment['POSTGRES_PORT']);Database=what_have_i_done;Username=what_have_i_done;Password=$($taskEnvironment['POSTGRES_PASSWORD'])"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run --project src/WhatHaveIDone.API --urls http://localhost:5088
} finally { Pop-Location }
