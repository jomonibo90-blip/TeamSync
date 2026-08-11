param([switch]$Force)

# Azure Database Clear Script
$resourceGroup = "teamsync"
$serverName = "teamsync-prod-sql-2026"
$databaseName = "TeamSyncDb"
$appServiceName = "teamsync"
$azureConnectionString = "Server=tcp:teamsync-prod-sql-2026.database.windows.net,1433;Initial Catalog=TeamSyncDb;Persist Security Info=False;User ID=teamsyncadmin;Password=xpress23@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "TeamSync: Clear Azure Database" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check SqlServer module
Write-Host "Checking SqlServer module..." -ForegroundColor Cyan
$module = Get-Module -Name SqlServer -ListAvailable
if (-not $module) {
	Write-Host "Installing SqlServer module..." -ForegroundColor Yellow
	Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser
}
Import-Module SqlServer
Write-Host "OK SqlServer module ready" -ForegroundColor Green
Write-Host ""

# Test connection
Write-Host "Testing Azure connection..." -ForegroundColor Cyan
try {
	$result = Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT COUNT(*) as UserCount FROM AspNetUsers" -ErrorAction Stop
	Write-Host "OK Connected! Found $($result.UserCount) users" -ForegroundColor Green
}
catch {
	Write-Host "FAIL Cannot connect to Azure database: $_" -ForegroundColor Red
	exit 1
}

# Confirm
Write-Host ""
Write-Host "WARNING: This will DELETE ALL DATA!" -ForegroundColor Red
if (-not $Force) {
	$confirm = Read-Host "Type 'YES' to continue"
	if ($confirm -ne "YES") {
		Write-Host "Cancelled" -ForegroundColor Yellow
		exit 0
	}
}

# Clear data
Write-Host ""
Write-Host "Clearing database..." -ForegroundColor Cyan
$queries = @(
	"DELETE FROM Contributions",
	"DELETE FROM TaskNotes",
	"DELETE FROM TaskAssignments",
	"DELETE FROM Tasks",
	"DELETE FROM Notifications",
	"DELETE FROM AspNetUserClaims",
	"DELETE FROM AspNetUserRoles",
	"DELETE FROM AspNetUserLogins",
	"DELETE FROM AspNetUserTokens",
	"DELETE FROM GroupMembers",
	"DELETE FROM Groups",
	"DELETE FROM AspNetUsers",
	"DELETE FROM AspNetRoles"
)

$errorCount = 0
foreach ($query in $queries) {
	try {
		Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query $query -ErrorAction Stop
		Write-Host "  OK $query" -ForegroundColor Gray
	}
	catch {
		Write-Host "  ERROR $query : $_" -ForegroundColor Red
		$errorCount++
	}
}

if ($errorCount -gt 0) {
	Write-Host ""
	Write-Host "FAIL $errorCount errors during deletion" -ForegroundColor Red
	exit 1
}

# Verify
Write-Host ""
Write-Host "Verifying..." -ForegroundColor Cyan
$userCount = (Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT COUNT(*) as Count FROM AspNetUsers").Count
$taskCount = (Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT COUNT(*) as Count FROM Tasks").Count
$groupCount = (Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT COUNT(*) as Count FROM Groups").Count

Write-Host "  Users: $userCount" -ForegroundColor Gray
Write-Host "  Tasks: $taskCount" -ForegroundColor Gray
Write-Host "  Groups: $groupCount" -ForegroundColor Gray

Write-Host ""
Write-Host "SUCCESS Database cleared!" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEP: Restart Azure App Service" -ForegroundColor Yellow
Write-Host "  az webapp restart --resource-group $resourceGroup --name $appServiceName" -ForegroundColor Gray
Write-Host ""
Write-Host "Demo data will seed automatically after restart" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
