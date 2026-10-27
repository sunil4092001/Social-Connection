<#
.SYNOPSIS
    Removes every end-to-end test user (e2e.*@example.com) and their data from the local development stores:
    IdentityDb, linkedin_users, linkedin_posts, requirements (PostgreSQL), linkedin_notifications (SQL Server)
    and the Neo4j graph. Real users are never matched.
#>
param(
    [Parameter(Mandatory = $true)][string]$PostgresPassword,
    [Parameter(Mandatory = $true)][string]$Neo4jPassword,
    [string]$PsqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe',
    [string]$SqlServer = 'localhost',
    [string]$Neo4jHttp = 'http://localhost:7474',
    [string]$Neo4jDatabase = 'LinkedInDB'
)

$ErrorActionPreference = 'Stop'
$env:PGPASSWORD = $PostgresPassword
# SQL goes through a file: Windows PowerShell strips the double quotes around identifiers in native arguments.
function Psql([string]$database, [string]$sql) {
    $file = New-TemporaryFile
    try {
        Set-Content -Path $file -Value $sql -Encoding ascii
        & $PsqlPath -h localhost -U postgres -d $database -t -A -q -f $file
    }
    finally { Remove-Item $file -Force }
}

$ids = @(Psql 'IdentityDb' "SELECT ""Id"" FROM ""Users"" WHERE ""Email"" LIKE 'e2e.%@example.com';" | Where-Object { $_ })
if ($ids.Count -eq 0) { Write-Host 'No e2e test users found.'; exit 0 }
$list = ($ids | ForEach-Object { "'$_'" }) -join ','
Write-Host "Removing $($ids.Count) e2e users..."

# Requirements owned by test users, their outbox rows, then responses by test professionals on any requirement.
Psql 'requirements' "DELETE FROM ""OutboxMessages"" WHERE ""MessageKey"" IN (SELECT ""Id""::text FROM ""Requirements"" WHERE ""CustomerId""::text IN ($list)); DELETE FROM ""Requirements"" WHERE ""CustomerId""::text IN ($list); DELETE FROM ""RequirementResponses"" WHERE ""ProfessionalId""::text IN ($list);" | Out-Null
Psql 'linkedin_posts' "DELETE FROM ""OutboxMessages"" WHERE ""MessageKey"" IN (SELECT ""PostId""::text FROM ""Post"" WHERE ""AuthorUserId""::text IN ($list)); DELETE FROM ""Post"" WHERE ""AuthorUserId""::text IN ($list); DELETE FROM ""Comment"" WHERE ""UserId""::text IN ($list); DELETE FROM ""Post_Like"" WHERE ""UserId""::text IN ($list);" | Out-Null
Psql 'linkedin_users' "DELETE FROM ""UserProfiles"" WHERE ""UserId""::text IN ($list);" | Out-Null
sqlcmd -S $SqlServer -E -I -d linkedin_notifications -Q "SET NOCOUNT ON; DELETE FROM Notifications WHERE RecipientUserId IN ($list);" | Out-Null

$body = @{ statements = @(
    @{ statement = 'MATCH (u:User) WHERE u.id IN $ids DETACH DELETE u'; parameters = @{ ids = $ids } },
    @{ statement = 'MATCH (m:OutboxMessage) WHERE any(id IN $ids WHERE m.key CONTAINS id) DELETE m'; parameters = @{ ids = $ids } }
) } | ConvertTo-Json -Depth 5
$auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("neo4j:$Neo4jPassword"))
Invoke-RestMethod -Method Post -Uri "$Neo4jHttp/db/$Neo4jDatabase/tx/commit" -Headers @{ Authorization = "Basic $auth" } -ContentType 'application/json' -Body $body | Out-Null

# Identity last (refresh tokens cascade).
Psql 'IdentityDb' "DELETE FROM ""Users"" WHERE ""Email"" LIKE 'e2e.%@example.com';" | Out-Null
Write-Host 'Done.'
