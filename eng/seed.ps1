param([string]$BaseUrl = 'http://localhost:5088')
$ErrorActionPreference = 'Stop'
if (!([Uri]$BaseUrl).IsLoopback) { throw 'Development seeding is limited to a loopback URL.' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskEnvPath = Join-Path $taskRoot '.env'
if (!(Test-Path -LiteralPath $taskEnvPath)) { throw 'Run eng/start.ps1 first.' }
$taskEnvironment = @{}
foreach ($taskLine in Get-Content -LiteralPath $taskEnvPath) { if ($taskLine -match '^([A-Z_]+)=(.*)$') { $taskEnvironment[$matches[1]] = $matches[2] } }
if (!$taskEnvironment['DEMO_PASSWORD']) {
    $taskEnvironment['DEMO_PASSWORD'] = 'Demo!Aa1' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    Add-Content -LiteralPath $taskEnvPath -Value ("DEMO_PASSWORD=" + $taskEnvironment['DEMO_PASSWORD'])
}
$taskEmail = 'demo@example.test'
$taskBase = $BaseUrl.TrimEnd('/') + '/api/v1/'
$taskSessionInfo = Invoke-RestMethod -Uri ($taskBase + 'session') -SessionVariable taskSession
$taskHeaders = @{ 'X-CSRF-TOKEN' = $taskSessionInfo.csrfToken }
$taskAccount = @{ email=$taskEmail; password=$taskEnvironment['DEMO_PASSWORD'] } | ConvertTo-Json
try { $null = Invoke-RestMethod -Uri ($taskBase + 'account/register') -Method Post -Body $taskAccount -ContentType 'application/json' -WebSession $taskSession -Headers $taskHeaders }
catch { $null = Invoke-RestMethod -Uri ($taskBase + 'account/login') -Method Post -Body $taskAccount -ContentType 'application/json' -WebSession $taskSession -Headers $taskHeaders }
$taskSessionInfo = Invoke-RestMethod -Uri ($taskBase + 'session') -WebSession $taskSession
$taskHeaders['X-CSRF-TOKEN'] = $taskSessionInfo.csrfToken
$taskMarkerPath = Join-Path $taskRoot 'artifacts/dev-seed.json'
New-Item -ItemType Directory -Path (Split-Path -Parent $taskMarkerPath) -Force | Out-Null
$taskMarker = if (Test-Path -LiteralPath $taskMarkerPath) { Get-Content -LiteralPath $taskMarkerPath -Raw | ConvertFrom-Json -AsHashtable } else { @{ Projects=@{}; Entries=@() } }
$taskDefaultClient = @(Invoke-RestMethod -Uri ($taskBase + 'clients') -WebSession $taskSession) | Where-Object isDefault | Select-Object -First 1
$taskProjects = @(Invoke-RestMethod -Uri ($taskBase + 'projects') -WebSession $taskSession)
foreach ($taskName in @('WhatHaveIDone','RSM')) {
    $taskExisting = $taskProjects | Where-Object { $_.id -eq $taskMarker.Projects[$taskName] } | Select-Object -First 1
    if (!$taskExisting) { $taskExisting = $taskProjects | Where-Object name -eq $taskName | Select-Object -First 1 }
    if (!$taskExisting) {
        $taskBody = @{name=$taskName;clientId=$taskDefaultClient.id;color=$(if ($taskName -eq 'RSM') { '#667fb7' } else { '#009cde' })} | ConvertTo-Json
        $taskId = Invoke-RestMethod -Uri ($taskBase + 'projects') -Method Post -Body $taskBody -ContentType 'application/json' -Headers $taskHeaders -WebSession $taskSession
    } else { $taskId = $taskExisting.id }
    $taskMarker.Projects[$taskName] = $taskId
}
$taskZone = [TimeZoneInfo]::FindSystemTimeZoneById('Europe/Prague')
$taskDay = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow,$taskZone).Date
$taskSamples = @(
    @{Key='planning';Description='Návrh aplikace';Project='WhatHaveIDone';Start=$taskDay.AddHours(9);End=$taskDay.AddHours(10.5);Tags=@('plánování')},
    @{Key='consultation';Description='Konzultace';Project='RSM';Start=$taskDay.AddHours(10);End=$taskDay.AddHours(11);Tags=@('schůzka')},
    @{Key='unassigned';Description='';Project='';Start=$taskDay.AddHours(13);End=$taskDay.AddHours(13.75);Tags=@()},
    @{Key='overnight';Description='Úprava přihlášení';Project='RSM';Start=$taskDay.AddDays(-1).AddHours(23);End=$taskDay.AddHours(1);Tags=@('vývoj')}
)
foreach ($taskSample in $taskSamples) {
    if ($taskMarker.Entries -contains $taskSample.Key) { continue }
    $taskBytes = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('whid-development-' + $taskSample.Key))
    $taskCreateId = [Guid]::new([byte[]]$taskBytes[0..15])
    $taskBody = @{createRequestId=$taskCreateId;description=$taskSample.Description;projectId=$(if ($taskSample.Project) {$taskMarker.Projects[$taskSample.Project]} else {$null});tags=$taskSample.Tags;start=([DateTimeOffset][TimeZoneInfo]::ConvertTimeToUtc($taskSample.Start,$taskZone)).ToString('O');end=([DateTimeOffset][TimeZoneInfo]::ConvertTimeToUtc($taskSample.End,$taskZone)).ToString('O')} | ConvertTo-Json -Depth 5
    $null = Invoke-RestMethod -Uri ($taskBase + 'time-entries') -Method Post -Body $taskBody -ContentType 'application/json' -WebSession $taskSession -Headers $taskHeaders
    $taskMarker.Entries += $taskSample.Key
    $taskMarker | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskMarkerPath -Encoding utf8
}
$null = Invoke-RestMethod -Uri ($taskBase + 'onboarding/complete') -Method Post -Body 'null' -ContentType 'application/json' -WebSession $taskSession -Headers $taskHeaders
$taskMarker | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskMarkerPath -Encoding utf8
Write-Output "Synthetic sample data ready for $taskEmail. Its generated password is stored only in the ignored .env file."
