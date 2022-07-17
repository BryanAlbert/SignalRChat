$global:test = "Test-08"

function Get-Description($qkr)
{
	"`n${test}: Bruce adds Fred and Mom and goes offline, Fred comes on and adds Bruce
	and Mom, Bruce comes online, Mom comes online
	
	Bruce comes online, adds Fred and Mom, goes offline, Fred comes online and adds Bruce
	and Mom, Bruce and Mom come online, everyone accepts, Bruce lists and goes offline,
	Fred lists and goes offline, Mom lists and goes offline. Uses BruceInput1.txt and
	BruceInput2.txt.`n"

	if ($null -eq $qkr -or $qkr)
	{
	"`tTo test QKR, run Test-QKRas Bruce 1 in one console and wait for it to finish. Run
	Test-QKRas Fred in another console, log in as Mom on QKR and accept friend request,
	then run Test-QKRas Bruce 2 in the first console. Accept friend request in QKR, verify
	that Bruce and Fred have finished and pop to Home. Test intermediate output with
	Check-QKRTest 1.
	
	Next, log in as Bruce on QKR and add Mom and Fred then pop back to Home. Run Test-QKRas
	Fred in one console and Test-QKRas Mom in the other then log in as Bruce again in QKR.
	Accpet the friend request then pop back to Home. Verify that Mom and Fred have exited
	and run Check-QKRTest 2 to validate the test.`n"
	}
}

function Reset-Test($showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceNoFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
	Copy-Item .\MomNoFriends.qkr .\Mom.qkr.json
	Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
	Copy-Item .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
	Copy-Item .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json)
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
	Reset-Test $true
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test
}

function Check-Test
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json 2
	Compare-Files .\FredControl.qkr .\Fred.qkr.json 2
	Compare-Files .\MomControl.qkr .\Mom.qkr.json 2
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
	switch ($stage) {
		1 {
			Compare-Files .\BruceControl.qkr .\Bruce.qkr.json 2
			Compare-Files .\FredControl.qkr .\Fred.qkr.json 2
			Compare-Files .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98eControl.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json) 2
			Compare-Files .\BruceControl.txt .\BruceOutput.txt 1
			Compare-Files .\FredControl.txt .\FredOutput.txt 1
			Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
		}
		2 {
			Compare-Files .\BruceControl.qkr .\Bruce.qkr.json 2
			Compare-Files .\FredControl.qkr .\Fred.qkr.json 2
			Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
			Compare-Files .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98eControl.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json) 2
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
		"Updating QKR control files for $test"
		Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
		Copy-Item .\Fred.qkr.json .\FredControl.qkr
		Copy-Item .\Mom.qkr.json .\MomControl.qkr
		Copy-Item (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr 
		Copy-Item (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json) .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98eControl.qkr 
		Copy-Item .\MomOutput.txt .\MomControl.txt
		Copy-Item .\BruceOutput.txt .\BruceControl2.txt
		Copy-Item .\FredOutput.txt .\FredControl2.txt
	}
	else
	{
		"Updating control files for $test"
		Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
		Copy-Item .\Fred.qkr.json .\FredControl.qkr
		Copy-Item .\Mom.qkr.json .\MomControl.qkr
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
	if (((Compare-Object $controlText $fileText) | Measure-Object).Count -gt 0)
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

		Compare-Object $controlText $fileText | Format-Table -Property SideIndicator, InputObject
	}
}

function Get-FilteredText($file, $merge)
{
	Get-Content $file | ForEach-Object {
		if ($_ -match "Modified: ") {
			$_ -replace "Modified: .{19}", "Modified <Date>"
		}
		elseif ($_ -match "`"Modified`": `".{19}") {
			$_ -replace "`"Modified`": `".{19}", "`"Modified`": `"<Date>`""
		}
		elseif ($_ -match "Modified Date: .{19}") {
			$_ -replace "Modified Date: .{19}", "Modified Date: `"<Date>`""
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
