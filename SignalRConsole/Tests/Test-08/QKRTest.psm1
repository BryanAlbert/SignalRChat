$global:test = "Test-08"

function Get-Description
{
	"`n${test}: Bruce adds Fred and Mom and goes offline, Fred comes on and adds Bruce
	and Mom, Bruce comes online, Mom comes online
	
	Bruce comes online, adds Fred and Mom, goes offline, Fred comes online and adds Bruce
	and Mom, Bruce and Mom come online, everyone accepts, Bruce lists and goes offline,
	Fred lists and goes offline, Mom lists and goes offline. Uses BruceInput1.txt and
	BruceInput2.txt.

	To test QKR, run Test-QKRas Bruce 1 in one console and wait for it to finish. Run
	Test-QKRas Fred in another console, log in as Mom on QKR and accept friend request,
	then run Test-QKRas Bruce 2 in the first console. Accept friend request in QKR.
	Test intermediate output with Check-QKRTest 1. Log in as Bruce on QKR and add Mom
	and Fred then pop back to Home. Run Test-QKRas Fred in one console and Test-QKRas
	Mom in the other then log in again in QKR and accpet the friend request then pop
	back to Home. Run Check-QKRTest 2 to validate the test.`n"
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
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
	if (Test-Path .\FredOutput.txt) { Remove-Item .\FredOutput.txt }
	if (Test-Path .\MomOutput.txt) { Remove-Item .\MomOutput.txt}
	Pop-Location

	if ($null -eq $showDescription -or $showDescription)
    {
		Get-Description
	}
}

function Run-Test
{
	$script = Join-Path $test "Test.txt"
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test
}

function Check-Test
{
	Push-Location $test
	$global:warningCount = 0
	$global:errorCount = 0
	Compare-Files .\BruceControl.txt .\BruceOutput.txt $true
	Compare-Files .\FredControl.txt .\FredOutput.txt $true
	Compare-Files .\MomControl.txt .\MomOutput.txt $true
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false
	Compare-Files .\FredControl.qkr .\Fred.qkr.json $false
	Compare-Files .\MomControl.qkr .\Mom.qkr.json $false

	"Total warning count: $global:warningCount"
	"Total error count: $global:errorCount"
	Pop-Location
}

# Checks intermediate state before json files are overwritten, to be run with 1
# after two console apps run Bruce and Fred and QKR runs Mom. Run with 2 after
# two console apps run Fred and Mom and QKR runs Bruce. 
function Check-QKRTest($stage)
{
	Push-Location $test
	
	$global:warningCount = 0
	$global:errorCount = 0
	switch ($stage) {
		1 {
			Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false
			Compare-Files .\FredControl.qkr .\Fred.qkr.json $false
			Compare-Files .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98eControl.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json)
			Compare-Files .\BruceControl.txt .\BruceOutput.txt
			Compare-Files .\FredControl.txt .\FredOutput.txt
			Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
		}
		2 {
			Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false
			Compare-Files .\FredControl.qkr .\Fred.qkr.json $false
			Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
			Compare-Files .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98eControl.qkr (Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json)
			Compare-Files .\BruceControl2.txt .\BruceOutput.txt $true
			Compare-Files .\FredControl2.txt .\FredOutput.txt $true
			Compare-Files .\MomControl.txt .\MomOutput.txt $true
		}
	}

	"Total warning count: $global:warningCount"
	"Total error count: $global:errorCount"
	Pop-Location
}

function Update-ControlFiles($updateQkr)
{
	Push-Location $global:test
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
		"Updating control files for $global:test"
		Copy-Item .\BruceOutput.txt .\BruceControl.txt
		Copy-Item .\FredOutput.txt .\FredControl.txt
		Copy-Item .\MomOutput.txt .\MomControl.txt
		Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
		Copy-Item .\Fred.qkr.json .\FredControl.qkr
		Copy-Item .\Mom.qkr.json .\MomControl.qkr
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
	if (((Compare-Object (Get-Content $control) (Get-Content $file)) | Measure-Object).Count -gt 0) {
		if ($logFile)
		{
			"Warning: $file has unexpected output:"
			$global:warningCount++
		}
		else
		{
			"Error: $file has unexpected output:"
			$global:errorCount++
		}

		Compare-Object (Get-Content $control) (Get-Content $file) | Format-Table -Property SideIndicator, InputObject
	}
}
