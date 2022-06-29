$global:test = "Test-09"

function Get-Description($qkr)
{
	"`n${test}: Bruce friends with Fred, adds Fred

	Bruce, friends with Fred, comes online and adds Fred, gets a message,
	lists and goes offline.`n"

	if ($null -eq $qkr -or $qkr)
	{
	"`tTo test QKR, log in as Bruce on QKR and add Fred, verify the Status
	message and pop to Home. Check results with Check-Test.`n"
	}
}

function Reset-Test($showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceFriends.qkr .\Bruce.qkr.json
	Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
	Get-ChildItem *Output.txt | ForEach-Object {
		Remove-Item $_
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
	
	if ($null -eq $checkQkr -or $checkQkr) {
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
	}
	else {
		Compare-Files .\BruceControl.txt .\BruceOutput.txt $true
	}

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
	}
	else
	{
		"Updating control files for $test"
		Copy-Item .\BruceOutput.txt .\BruceControl.txt
		Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
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
