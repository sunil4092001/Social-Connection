<#
.SYNOPSIS
    Stores BuildConnect development secrets in .NET user-secrets (outside the repository).

.DESCRIPTION
    appsettings.json files no longer contain secrets. This script writes, per service:
      - Jwt:Key                         a newly generated random key, shared by all services
      - InternalService:UserServiceKey  a newly generated key shared by IdentityService and UserService
      - ConnectionStrings:DefaultConnection for the PostgreSQL databases
      - Neo4j:Password                  for ConnectionService
    Re-running it generates new keys, which signs everybody out (existing tokens become invalid).

    In containers use environment variables instead, for example Jwt__Key and ConnectionStrings__DefaultConnection.

.EXAMPLE
    ./scripts/setup-dev-secrets.ps1 -PostgresPassword admin -Neo4jPassword 'admin@4j'
#>
param(
    [Parameter(Mandatory = $true)][string]$PostgresPassword,
    [Parameter(Mandatory = $true)][string]$Neo4jPassword,
    [string]$PostgresHost = 'localhost',
    [int]$PostgresPort = 5432,
    [string]$PostgresUser = 'postgres'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function New-RandomKey([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    return [Convert]::ToBase64String($buffer)
}

function Set-Secret([string]$project, [string]$name, [string]$value) {
    & dotnet user-secrets set $name $value --project (Join-Path $root "$project/$project.csproj") | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to set $name for $project" }
}

function Get-PostgresConnection([string]$database) {
    "Host=$PostgresHost;Port=$PostgresPort;Database=$database;Username=$PostgresUser;Password=$PostgresPassword"
}

$jwtKey = New-RandomKey 64
$internalKey = New-RandomKey 32

$jwtServices = 'IdentityService', 'UserService', 'PostService', 'RequirementService', 'ConnectionService', 'NotificationService'
foreach ($service in $jwtServices) {
    Set-Secret $service 'Jwt:Key' $jwtKey
}

Set-Secret 'IdentityService' 'InternalService:UserServiceKey' $internalKey
Set-Secret 'UserService' 'InternalService:UserServiceKey' $internalKey

Set-Secret 'IdentityService' 'ConnectionStrings:DefaultConnection' (Get-PostgresConnection 'IdentityDb')
Set-Secret 'UserService' 'ConnectionStrings:DefaultConnection' (Get-PostgresConnection 'linkedin_users')
Set-Secret 'PostService' 'ConnectionStrings:DefaultConnection' (Get-PostgresConnection 'linkedin_posts')
Set-Secret 'RequirementService' 'ConnectionStrings:DefaultConnection' (Get-PostgresConnection 'requirements')

Set-Secret 'ConnectionService' 'Neo4j:Password' $Neo4jPassword

Write-Host "Development secrets stored for: $($jwtServices -join ', ')."
Write-Host 'New JWT and internal keys were generated; sign in again in the browser.'
