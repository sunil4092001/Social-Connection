<#
.SYNOPSIS
    Creates .env for docker-compose.yml with freshly generated secrets.

.DESCRIPTION
    .env is git-ignored. Re-run with -Force to replace it (this invalidates logins and, because the database
    passwords change, requires removing the old volumes: docker compose down -v).
#>
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root '.env'

if ((Test-Path $envFile) -and -not $Force) {
    Write-Host '.env already exists; use -Force to replace it.'
    exit 0
}

function New-Secret([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    # URL-safe characters only, so the values can be used in connection strings without escaping.
    return ([Convert]::ToBase64String($buffer)).Replace('+', 'A').Replace('/', 'B').TrimEnd('=')
}

@"
JWT_KEY=$(New-Secret 64)
INTERNAL_SERVICE_KEY=$(New-Secret 32)
POSTGRES_PASSWORD=$(New-Secret 18)
SQLSERVER_PASSWORD=Bc1-$(New-Secret 18)
NEO4J_PASSWORD=$(New-Secret 18)
"@ | Set-Content -Path $envFile -Encoding ascii -NoNewline

Write-Host "Created $envFile. Start the stack with: docker compose up -d --build"
