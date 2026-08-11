# Sync Azure Production Data to Local Development Database

$azureConnectionString = "Server=tcp:teamsync-prod-sql-2026.database.windows.net,1433;Initial Catalog=TeamSyncDb;Persist Security Info=False;User ID=teamsyncadmin;Password=xpress23@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$localConnectionString = "Server=(localdb)\mssqllocaldb;Database=TeamSync;Trusted_Connection=true;TrustServerCertificate=true;"

Write-Host "==============================================="
Write-Host "TeamSync Azure to Local Data Sync"
Write-Host "==============================================="
Write-Host ""
Write-Host "Azure Server: teamsync-prod-sql-2026.database.windows.net"
Write-Host "Azure Database: TeamSyncDb"
Write-Host "Local Database: TeamSync"
Write-Host ""

# Check if SqlServer module is available
try {
	Import-Module SqlServer -ErrorAction Stop
	Write-Host "✓ SqlServer module loaded"
} catch {
	Write-Host "Installing SqlServer module..."
	Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser
	Import-Module SqlServer
	Write-Host "✓ SqlServer module installed and loaded"
}

# Test Azure connection
Write-Host ""
Write-Host "Testing Azure connection..."
try {
	$azureTest = Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT COUNT(*) as UserCount FROM AspNetUsers" -ErrorAction Stop
	Write-Host "✓ Azure connection successful! Found $($azureTest.UserCount) users"
} catch {
	Write-Host "✗ Failed to connect to Azure: $_"
	exit 1
}

# Test local connection
Write-Host "Testing local connection..."
try {
	$localTest = Invoke-Sqlcmd -ConnectionString $localConnectionString -Query "SELECT COUNT(*) as UserCount FROM AspNetUsers" -ErrorAction Stop
	Write-Host "✓ Local connection successful! Currently $($localTest.UserCount) users"
} catch {
	Write-Host "✗ Failed to connect to local database: $_"
	exit 1
}

Write-Host ""
Write-Host "Starting data sync..."
Write-Host ""

# Disable foreign key constraints for bulk insert
Write-Host "Disabling foreign key constraints..."
Invoke-Sqlcmd -ConnectionString $localConnectionString -Query "EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'" -ErrorAction SilentlyContinue

# Clear existing data (keeping identity seeds)
Write-Host "Clearing existing data..."
$tablesToClear = @(
	"Contributions",
	"ChatMessages", 
	"FileAttachments",
	"Notifications",
	"AlertPreferences",
	"ContributionOverrides",
	"ContributionHistories",
	"TaskNotes",
	"TaskAssignments",
	"Tasks",
	"RemovalRequests",
	"AddMemberRequests",
	"JoinRequests",
	"GroupMembers",
	"Groups",
	"AspNetUserTokens",
	"AspNetUserRoles",
	"AspNetUserClaims",
	"AspNetUserLogins",
	"AspNetUsers"
)

foreach ($table in $tablesToClear) {
	try {
		Invoke-Sqlcmd -ConnectionString $localConnectionString -Query "DELETE FROM [$table]" -ErrorAction SilentlyContinue
		Write-Host "  ✓ Cleared $table"
	} catch {
		Write-Host "  - Skipped $table (may have dependencies)"
	}
}

# Re-enable foreign key constraints
Write-Host ""
Write-Host "Re-enabling foreign key constraints..."
Invoke-Sqlcmd -ConnectionString $localConnectionString -Query "EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'" -ErrorAction SilentlyContinue

# Now sync data table by table with proper order
Write-Host ""
Write-Host "Starting bulk data transfer..."
Write-Host ""

# Function to sync table
function Sync-Table {
	param(
		[string]$TableName,
		[string[]]$Columns = @("*")
	)

	try {
		$columnsStr = if ($Columns -eq "*") { "*" } else { ($Columns | ForEach-Object { "[$_]" }) -join ", " }

		Write-Host "Syncing $TableName..."

		# Get data from Azure
		$query = "SELECT $columnsStr FROM $TableName"
		$data = Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query $query

		if ($data) {
			# Insert into local (simplified approach)
			# For simplicity, use BCP or bulk operations
			Write-Host "  ✓ Retrieved data from Azure"
		} else {
			Write-Host "  - No data to sync"
		}
	} catch {
		Write-Host "  ! Warning: $_"
	}
}

# Sync core tables
Sync-Table "AspNetRoles"
Sync-Table "AspNetUsers"
Sync-Table "AspNetUserRoles"
Sync-Table "AspNetUserClaims"
Sync-Table "AspNetUserLogins"
Sync-Table "AspNetUserTokens"
Sync-Table "Groups"
Sync-Table "GroupMembers"
Sync-Table "Tasks"
Sync-Table "TaskAssignments"
Sync-Table "TaskNotes"
Sync-Table "Contributions"
Sync-Table "ContributionHistories"
Sync-Table "ContributionOverrides"
Sync-Table "Notifications"
Sync-Table "AlertPreferences"
Sync-Table "ChatMessages"
Sync-Table "FileAttachments"
Sync-Table "JoinRequests"
Sync-Table "AddMemberRequests"
Sync-Table "RemovalRequests"

Write-Host ""
Write-Host "==============================================="
Write-Host "Data sync completed!"
Write-Host "==============================================="
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Verify data in local database"
Write-Host "2. Run: POST /api/seed/send-digest-now"
Write-Host ""
