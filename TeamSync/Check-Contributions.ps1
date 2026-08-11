param([string]$ResourceGroup = "teamsync")

$azureConnectionString = "Server=tcp:teamsync-prod-sql-2026.database.windows.net,1433;Initial Catalog=TeamSyncDb;Persist Security Info=False;User ID=teamsyncadmin;Password=xpress23@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "Checking Azure database for contributions..." -ForegroundColor Cyan

try {
	Import-Module SqlServer -ErrorAction SilentlyContinue

	# Check contribution count
	$result = Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT COUNT(*) as Count FROM Contributions" -ErrorAction Stop
	Write-Host "Total contributions in database: $($result.Count)" -ForegroundColor Cyan

	# Check tasks with contributions
	$taskContribs = Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query @"
	SELECT TOP 10 
		t.Id, 
		t.Title, 
		COUNT(c.Id) as ContributionCount
	FROM Tasks t
	LEFT JOIN Contributions c ON t.Id = c.TaskId
	GROUP BY t.Id, t.Title
	ORDER BY ContributionCount DESC
"@ -ErrorAction Stop

	Write-Host ""
	Write-Host "Tasks and their contribution counts:" -ForegroundColor Cyan
	foreach ($row in $taskContribs) {
		Write-Host "  Task: $($row.Title) - Contributions: $($row.ContributionCount)" -ForegroundColor Gray
	}

	# Sample contribution
	$sample = Invoke-Sqlcmd -ConnectionString $azureConnectionString -Query "SELECT TOP 3 Id, TaskId, Description, HoursSpent, ContributedAt FROM Contributions ORDER BY ContributedAt DESC" -ErrorAction Stop
	if ($sample) {
		Write-Host ""
		Write-Host "Sample contributions:" -ForegroundColor Cyan
		foreach ($c in $sample) {
			Write-Host "  - ID: $($c.Id), Task: $($c.TaskId), Hours: $($c.HoursSpent), Desc: $($c.Description)" -ForegroundColor Gray
		}
	}
}
catch {
	Write-Host "ERROR: $_" -ForegroundColor Red
}
