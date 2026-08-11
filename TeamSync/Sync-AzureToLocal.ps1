# TeamSync: Sync Azure Production Data to Local Development Database
# This script uses BCP and SQLCMD to copy data from Azure to local

param(
	[string]$AzurePassword = "xpress23@"
)

$ErrorActionPreference = "SilentlyContinue"

$azureServer = "teamsync-prod-sql-2026.database.windows.net"
$azureDb = "TeamSyncDb"
$azureUser = "teamsyncadmin"

$localServer = "(localdb)\mssqllocaldb"
$localDb = "TeamSync"

Write-Host "╔═══════════════════════════════════════════════════════╗"
Write-Host "║       TeamSync: Azure to Local Data Sync              ║"
Write-Host "╚═══════════════════════════════════════════════════════╝"
Write-Host ""
Write-Host "Azure:  $azureServer / $azureDb"
Write-Host "Local:  $localServer / $localDb"
Write-Host ""

# Define tables to sync in order
$tables = @(
	"AspNetRoles",
	"AspNetUsers",
	"AspNetUserRoles",
	"AspNetUserClaims",
	"AspNetUserLogins",
	"AspNetUserTokens",
	"Groups",
	"GroupMembers",
	"Tasks",
	"TaskAssignments",
	"TaskNotes",
	"Contributions",
	"ContributionHistories",
	"ContributionOverrides",
	"Notifications",
	"AlertPreferences",
	"ChatMessages",
	"FileAttachments",
	"JoinRequests",
	"AddMemberRequests",
	"RemovalRequests"
)

Write-Host "Clearing local database..."
$clearQuery = "
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
"

foreach ($table in $tables) {
	$clearQuery += "DELETE FROM [$table]; "
}

$clearQuery += "
GO
EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO
"

$clearQuery | sqlcmd -S $localServer -d $localDb -U sa -P "" 2>$null
Write-Host "✓ Local database cleared"
Write-Host ""

# Function to sync a single table
function Sync-Table {
	param([string]$TableName)

	try {
		# Export from Azure using BCP
		Write-Host "  Syncing: $TableName..."

		$queryString = "SELECT * FROM [$TableName]"

		# Use sqlcmd to copy data
		$copyQuery = @"
INSERT INTO $localDb.dbo.[$TableName]
SELECT * FROM OPENROWSET(
	'SQLOLEDB',
	'Server=$azureServer,1433;
	 Database=$azureDb;
	 User ID=$azureUser;
	 Password=$AzurePassword;',
	'SELECT * FROM [$TableName]'
)
"@

		# Simpler approach: Export to CSV then import
		# First get data from Azure
		$exportFile = "$env:TEMP\$TableName.csv"

		$exportCmd = "SQLCMD -S $azureServer -d $azureDb -U $azureUser -P $AzurePassword -Q `"SET NOCOUNT ON; SELECT * FROM [$TableName]`" -o `"$exportFile`" -W -w 1000" 

		Invoke-Expression $exportCmd 2>$null

		if (Test-Path $exportFile -PathType Leaf) {
			# Import to local using BCP
			$importCmd = "BCP $localDb.dbo.$TableName in `"$exportFile`" -S $localServer -T -c -F 2"
			Invoke-Expression $importCmd 2>$null
			Remove-Item $exportFile -Force -ErrorAction SilentlyContinue
			Write-Host "  ✓ $TableName synced"
			return $true
		} else {
			Write-Host "  - $TableName: No data or empty"
			return $false
		}
	}
	catch {
		Write-Host "  ! $TableName: Error - $($_.Exception.Message)"
		return $false
	}
}

# Sync all tables
Write-Host "Syncing data from Azure to Local..."
Write-Host ""

$synced = 0
foreach ($table in $tables) {
	if (Sync-Table $table) {
		$synced++
	}
}

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════╗"
Write-Host "║                  Sync Complete!                       ║"
Write-Host "╚═══════════════════════════════════════════════════════╝"
Write-Host ""
Write-Host "Tables synced: $synced / $($tables.Count)"
Write-Host ""
Write-Host "✓ Your local database now has all production data!"
Write-Host ""
Write-Host "Next Steps:"
Write-Host "1. Make sure the app is running: dotnet run"
Write-Host "2. Call: POST http://localhost:5278/api/seed/send-digest-now"
Write-Host ""
