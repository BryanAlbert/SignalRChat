$global:test = "Test-06"

function Get-Description($qkr)
{
	"`n${test}: Bruce, Fred, and Mom online, Bruce and Fred befriend Mom

	Mom, Bruce, and Fred online, Bruce and Fred befriend Mom, Mom accepts both,
	Bruce and Fred list, go offline, Mom lists and goes offline.`n"

	if ($null -eq $qkr -or $qkr)
	{
	"`tTo test QKR, run Reset-Test `$true then connect as Mom on QKR, run Start-TestFor
	Bruce in one console and Start-TestFor Fred in another. Accept friend requests,
	verify friendships and that Bruce and Fred exit, then pop to Home page. Test
	intermediate output with Check-QKRTest 1. 
	
	Next run Start-TestFor Mom in one console, connect as Bruce on QKR, add Mom
	(jeanmariealbert@hotmail.com) and verify friendship. Run Start-TestFor Fred in
	the other console. Pop to Home, verify that Mom and Fred have exited and exit
	QKR. Run Check-QKRTest 2 to validate the test.`n"
	}
}

function Reset-Test($resetQkr, $showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceNoFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
	Copy-Item .\MomNoFriends.qkr .\Mom.qkr.json

	if ($resetQkr -eq $true)
	{
		"Resetting QKR files at $global:qkrLocalState"
		Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
		Copy-Item .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json)
	}

	Get-ChildItem *Output.txt | ForEach-Object {
		Remove-Item $_
	}

	Pop-Location

	if ($null -eq $showDescription -or $showDescription) {
		Get-Description $true
	}
}

function Start-TestFor($user, $number)
{
    if ($null -eq $number)
    {
        "Calling Start-Chat in $test for $user"
        Start-Chat (Join-Path $test ($user + "Input.txt")) (Join-Path $test ($user + "Output.txt")) $user
    }
    else
    {
        "Calling Start-Chat in $test for $user, input script number $number"
        Start-Chat (Join-Path $test ($user + "Input$number.txt")) (Join-Path $test ($user + "Output.txt")) $user
    }
}

function Run-Test
{
	$script = Join-Path $test "Test.txt"
	"Running script $script"
	Reset-Test $false $true
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test
}

function Check-Test
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	Compare-Files .\Bruce.control.qkr .\Bruce.qkr.json 2
	Compare-Files .\Fred.control.qkr .\Fred.qkr.json 2
	Compare-Files .\Mom.control.qkr .\Mom.qkr.json 2
	Compare-Files .\BruceControl.txt .\BruceOutput.txt 1
	Compare-Files .\FredControl.txt .\FredOutput.txt 1
	Compare-Files .\MomControl.txt .\MomOutput.txt 1

	"Warning count: $script:warningCount"
	"Error count: $script:errorCount"
	$global:totalWarningCount += $script:warningCount
	$global:totalErrorCount += $script:errorCount
	Pop-Location
}

# Checks intermediate state before json files are overwritten, to be run with 1
# after two console apps run Bruce and Fred and QKR runs Mom. Run with 2 after
# two console apps run Fred and Mom and QKR runs Bruce. 
function Check-QKRTest($stage)
{
	Push-Location $test
	
	$script:warningCount = 0
	$script:errorCount = 0
	$tempWarningList = $global:warningList
	$tempErrorList = $global:errorList
	Compare-Files .\Bruce.control.qkr .\Bruce.qkr.json 2
	Compare-Files .\Fred.control.qkr .\Fred.qkr.json 2

	switch ($stage)
	{
		1 {
			Compare-Files .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.control.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json) 2
			Compare-Files .\BruceControl.txt .\BruceOutput.txt 1
			Compare-Files .\FredControl.txt .\FredOutput.txt 1
			Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
		}
		2 {
			Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
			Compare-Files .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.control.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json) 2
			Compare-Files .\BruceControl2.txt .\BruceOutput.txt 1
			Compare-Files .\FredControl2.txt .\FredOutput.txt 1
			Compare-Files .\MomControl.txt .\MomOutput.txt 1
		}
	}

	"Warning count: $script:warningCount"
	"Error count: $script:errorCount"
	$global:warningList = $tempWarningList
	$global:errorList = $tempErrorList
	Pop-Location
}

function Update-ControlFiles($updateQkr)
{
	Push-Location $test
	if ($updateQkr -eq $true)
	{
		"Updating QKR control files for $test from $global:qkrLocalState"
		Copy-Item .\Bruce.qkr.json .\Bruce.control.qkr
		Copy-Item .\Fred.qkr.json .\Fred.control.qkr
		Copy-Item .\Mom.qkr.json .\Mom.control.qkr
		Copy-Item (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.control.qkr 
		Copy-Item (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json) .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.control.qkr 
		Copy-Item .\MomOutput.txt .\MomControl.txt
		Copy-Item .\BruceOutput.txt .\BruceControl2.txt
		Copy-Item .\FredOutput.txt .\FredControl2.txt
	}
	else
	{
		"Updating control files for $test"
		Copy-Item .\Bruce.qkr.json .\Bruce.control.qkr
		Copy-Item .\Fred.qkr.json .\Fred.control.qkr
		Copy-Item .\Mom.qkr.json .\Mom.control.qkr
		Copy-Item .\BruceOutput.txt .\BruceControl.txt
		Copy-Item .\FredOutput.txt .\FredControl.txt
		Copy-Item .\MomOutput.txt .\MomControl.txt
	}
	
	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
}

function Compare-Files($control, $file, $errorLevel, $merge)
{
	"Comparing: $control with $file"
	$controlText = Get-FilteredText $control $merge
	$fileText = Get-FilteredText $file $merge
	if ($file -match "\.json$") {
		$syncWindow = 0
	} else {
		$syncWindow = [int32]::MaxValue
	}

	if (((Compare-Object -SyncWindow $syncWindow $controlText $fileText) | Measure-Object).Count -gt 0)
	{
		if ($errorLevel -eq 1)
		{
			"Warning: $file has unexpected output:"
			$script:warningCount++
			$global:warningList += $test
		}
		elseif ($errorLevel -eq 2)
		{
			"Error: $file has unexpected output:"
			$script:errorCount++
			$global:errorList += $test
		}
		else
		{
			"Files differ, check output:"
		}

		Compare-Object -SyncWindow $syncWindow $controlText $fileText | Format-Table -Property SideIndicator, InputObject
	}
}

function Get-FilteredText($file, $merge)
{
	Get-Content $file | ForEach-Object `
	{
		if ($_ -match "Modified: ") {
			$_ -replace "Modified: .{19}", "Modified <Date>"
		}
		elseif ($_ -match "`"Modified`": `".{19}") {
			$_ -replace "`"Modified`": `".{19}", "`"Modified`": `"<Date>`""
		}
		elseif ($_ -match "Modified Date: .{19}") {
			$_ -replace "Modified Date: .{19}", "Modified Date: `"<Date>`""
		}
		elseif ($_ -match "(con|qkr).{5}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}") {
			$_ -replace "(con|qkr)", "xxx"
		}
		elseif ($merge)
		{
			if ($_ -match "`"Created`": `".{19}") {
				$_ -replace "`"Created`": `".{19}", "`"Created`": `"<Date>`""
			}
			elseif ($_ -match "`"DeviceId`": `"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`",") {
				("  " + $_ -replace "`"DeviceId`": ", "" -replace ",", "") + ": 0"
			}
			else {
				$_
			}
		}
		else {
			$_
		}
	}
}
