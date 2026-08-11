# Test Weekly Digest Email Feature
# This script generates sample data and triggers the digest email
# NOTE: For production, SMTP password should be stored in Azure Key Vault or environment variables
# Never hardcode credentials in test scripts or source code

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "   TeamSync Weekly Digest Email - Comprehensive Test" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$appUrl = "http://localhost:5278"  # HTTP port from launchSettings.json
$testEndpoint = "$appUrl/api/test/generate-and-send-digest"
$digestStatusEndpoint = "$appUrl/api/test/digest-status"

# For production: Read SMTP password from environment variable or Key Vault
# $SmtpPassword = $env:EmailSettings__SmtpPassword
# If not set, the application will use the configuration provider which will read from Key Vault
# Do NOT hardcode password here

# Allow self-signed certificates (for local development)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Step 1: Generate test data and send digest
Write-Host "[*] Step 1: Generating test data and sending digest email..." -ForegroundColor Yellow
Write-Host "    Endpoint: POST $testEndpoint" -ForegroundColor Gray
Write-Host ""

try {
	$result = Invoke-RestMethod -Uri $testEndpoint -Method Post -ContentType "application/json" -ErrorAction Stop

	Write-Host "[OK] Success! Digest email triggered." -ForegroundColor Green
	Write-Host ""
	Write-Host "[DATA] Generated Data Summary:" -ForegroundColor Cyan
	Write-Host "       Email: $($result.details.testUserEmail)" -ForegroundColor White
	Write-Host "       Group: $($result.details.groupName)" -ForegroundColor White
	Write-Host "       Tasks Created: $($result.details.tasksCreated)" -ForegroundColor White
	Write-Host "       Notifications Created: $($result.details.notificationsCreated)" -ForegroundColor White
	Write-Host ""

	Write-Host "[TASKS] Task Details:" -ForegroundColor Cyan
	$result.details.taskDetails | ForEach-Object {
		Write-Host "       [Task] $($_.title)" -ForegroundColor White
		Write-Host "              Status: $($_.status)" -ForegroundColor Gray
		Write-Host "              Priority: $($_.priority)" -ForegroundColor Gray
		Write-Host "              Assigned To: $($_.assignedTo)" -ForegroundColor Gray
		$createdDate = [datetime]::Parse($_.createdAt)
		Write-Host "              Created: $($createdDate.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
		Write-Host ""
	}
}
catch {
	Write-Host "[ERROR] Failed: $_" -ForegroundColor Red
	Write-Host ""
	Write-Host "Troubleshooting:" -ForegroundColor Yellow
	Write-Host "  1. Make sure the TeamSync application is running" -ForegroundColor White
	Write-Host "  2. Check if the port is correct (default: 7158)" -ForegroundColor White
	Write-Host "  3. Try HTTP instead of HTTPS if needed" -ForegroundColor White
	exit 1
}

# Step 2: Check digest status
Write-Host "[*] Step 2: Checking digest email status..." -ForegroundColor Yellow
Write-Host ""

try {
	$status = Invoke-RestMethod -Uri $digestStatusEndpoint -Method Get -ContentType "application/json" -ErrorAction Stop

	Write-Host "[OK] Digest Status Retrieved" -ForegroundColor Green
	Write-Host ""
	Write-Host "[STATUS] Email Information:" -ForegroundColor Cyan
	Write-Host "         Recipient: $($status.testUserEmail)" -ForegroundColor White
	if ($status.lastDigestSentAt) {
		$lastSent = [datetime]::Parse($status.lastDigestSentAt)
		Write-Host "         Last Digest Sent: $($lastSent.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
	} else {
		Write-Host "         Last Digest Sent: Never" -ForegroundColor White
	}
	Write-Host ""

	Write-Host "[NOTIFICATIONS] Recent Notifications ($($status.recentNotificationsCount)):" -ForegroundColor Cyan
	$status.recentNotifications | ForEach-Object {
		Write-Host "        [$($_.type)] $($_.message)" -ForegroundColor White
		$sentTime = [datetime]::Parse($_.createdAt)
		Write-Host "        Sent: $($sentTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
	}
	Write-Host ""
}
catch {
	Write-Host "[WARN] Could not retrieve status: $_" -ForegroundColor Yellow
	Write-Host ""
}

# Step 3: Instructions
Write-Host "[INFO] Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Check your Gmail inbox for the digest email" -ForegroundColor White
Write-Host "  2. The email should contain all 4 sample tasks with statuses" -ForegroundColor White
Write-Host "  3. Verify professional formatting and details" -ForegroundColor White
Write-Host ""

Write-Host "[SUCCESS] Test Complete! Show this output to your professor." -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan
