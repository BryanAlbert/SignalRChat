$global:test = "Test-18"

function Get-Description($qkr)
{
	"`n${test}: Blocked, Bruce adds Fred (blocked), unfriends, adds (blocked),
	fred unfriends, adds
	
	Bruce and Fred blocked, Fred comes online, Bruce comes online, adds Fred and gets
	message, unfriends Fred, adds Fred and gets message, Fred unfriends, adds Bruce,
	Bruce accepts lists, goes offline, Fred lists, goes offline.`n"

	if ($null -eq $qkr -or $qkr)
	{
	"`tTo test QKR, log in as Fred on QKR and run Test-QKRas Bruce. When Bruce is
	waiting unfriend Bruce in QKR, add Bruce, then pop to Home.
	
	Next, run Test-QKRas Fred, log in as Bruce on QKR, add Fred and note Status,
	unfriend Fred then add Fred again. Accept friend request, verify Status then
	pop to Home. Run Check-Test to validate the test.`n"
	}
}

function Reset-Test($showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceBlocked.qkr .\Bruce.qkr.json
	Copy-Item .\FredBlocked.qkr .\Fred.qkr.json
	Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
	Copy-Item .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
	if (Test-Path .\BruceOutput.txt) {
		Remove-Item .\BruceOutput.txt
	}

	if (Test-Path .\FredOutput.txt) {
		Remove-Item .\FredOutput.txt
	}

	Pop-Location

	if ($null -eq $showDescription -or $showDescription) {
		Get-Description $true
	}
}

function Run-Test
{
	$script = Join-Path $test "Test.txt"
	"Running script $script"
	Get-Description $false
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test $false
}

function Check-Test($checkQkr)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false
	Compare-Files .\FredControl.qkr .\Fred.qkr.json $false
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
		Compare-Files .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072eControl.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
	}

	Compare-Files .\BruceControl.txt .\BruceOutput.txt $true
	Compare-Files .\FredControl.txt .\FredOutput.txt $true

	"Warning count: $script:warningCount"
	"Error count: $script:errorCount"
	$global:totalWarningCount += $script:warningCount
	$global:totalErrorCount += $script:errorCount
	Pop-Location
}

function Update-ControlFiles($updateQkr)
{
	Push-Location $test
	if ($updateQkr -eq $true)
	{
		"Updating QKR control files for $test from $global:qkrLocalState"
		Copy-Item (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr 
		Copy-Item (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json) .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072eControl.qkr 
	}
	else
	{
		"Updating control files for $test"
		Copy-Item .\BruceOutput.txt .\BruceControl.txt
		Copy-Item .\FredOutput.txt .\FredControl.txt
		Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
		Copy-Item .\Fred.qkr.json .\FredControl.qkr
	}
	
	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
}

function Compare-Files($control, $file, $logFile)
{
	"Comparing: $control with $file"
	if (((Compare-Object (Get-Content $control) (Get-Content $file)) | Measure-Object).Count -gt 0)
	{
		if ($logFile)
		{
			"Warning: $file has unexpected output:"
			$script:warningCount++
			$global:warningList += $test
		}
		else
		{
			"Error: $file has unexpected output:"
			$script:errorCount++
			$global:errorList += $test
		}

		Compare-Object (Get-Content $control) (Get-Content $file) | Format-Table -Property SideIndicator, InputObject
	}
}
