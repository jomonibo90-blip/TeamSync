param(
	[string]$ResourceGroup = "teamsync",
	[string]$AppServiceName = "teamsync"
)

# Restart Azure App Service
Write-Host "Restarting Azure App Service: $AppServiceName" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host ""

try {
	# Check Azure CLI
	$azCmd = Get-Command az -ErrorAction SilentlyContinue
	if ($azCmd) {
		Write-Host "Using Azure CLI..." -ForegroundColor Cyan
		az webapp restart --resource-group $ResourceGroup --name $AppServiceName
		Write-Host "OK App Service restarted" -ForegroundColor Green
	}
	else {
		Write-Host "Azure CLI not found" -ForegroundColor Yellow
		Write-Host ""
		Write-Host "To restart the app service, run this command in Azure Cloud Shell or a terminal with Azure CLI:" -ForegroundColor Yellow
		Write-Host ""
		Write-Host "  az webapp restart --resource-group $ResourceGroup --name $AppServiceName" -ForegroundColor Gray
		Write-Host ""
		Write-Host "Or use the Azure Portal:" -ForegroundColor Yellow
		Write-Host "  1. Go to portal.azure.com" -ForegroundColor Gray
		Write-Host "  2. Search for 'App Services'" -ForegroundColor Gray
		Write-Host "  3. Click '$AppServiceName'" -ForegroundColor Gray
		Write-Host "  4. Click 'Restart' button" -ForegroundColor Gray
		Write-Host ""
	}
}
catch {
	Write-Host "ERROR: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Once restarted:" -ForegroundColor Green
Write-Host "  1. Wait 30-60 seconds for app to start" -ForegroundColor Gray
Write-Host "  2. Check Azure App Service logs for 'Database initialization completed successfully'" -ForegroundColor Gray
Write-Host "  3. Visit the app to verify demo data is loaded" -ForegroundColor Gray
Write-Host ""
