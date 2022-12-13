$global:test = "Test-08"

function Get-Description($verbose)
{
	"`n${test}: Bruce adds Fred and Mom and goes offline, Fred comes on and adds Bruce
	and Mom, Bruce comes online, Mom comes online
	
	Bruce comes online, adds Fred and Mom, goes offline, Fred comes online and adds Bruce
	and Mom, Bruce and Mom come online, everyone accepts, Bruce lists and goes offline,
	Fred lists and goes offline, Mom lists and goes offline. Uses BruceInput1.txt and
	BruceInput2.txt.`n"

	if ($null -eq $verbose -or $verbose)
	{
	"`tRun Reset-Test with one of the following arguments to reset and/or configure:
	<none>    Reset everything
	Console   Reset only Console json files
	ResetQKR  Delete json files from QKR's LocalState folder
	QKR       Configure for testing QKR
	

	To test QKR, run Reset-Test QKR then run Start-TestFor Bruce 1 in one console and wait
	for it to finish. Run Start-TestFor Fred in another console, Connect Internet as Mom
	on QKR and accept the friend request, verify friendship then run Start-TestFor Bruce 2 in
	the first console. Accept the friend request in QKR, verify friendship, verify that Bruce
	and Fred have exited, pop to Home and exit QKR. Test intermediate output with
	Check-QKRTest 1.
	
	Next, Connect Internet as Bruce on QKR and add Mom (jeanmariealbert@hotmail.com) and Fred
	(fred@gmail.com) then pop back to Home. Run Start-TestFor Fred in one console and
	Start-TestFor Mom in the other, then Connect Internet as Bruce again in QKR. Accpet the
	friend request, verify friendship, pop back to Home and close QKR. Verify that Mom and
	Fred have exited and run Check-QKRTest 2 to validate the test.`n"
	}
}

function Reset-Test($reset, $showDescription)
{
	"Resetting $test"
	Push-Location $test
	$brucePath = Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json
	$momPath = Join-Path $global:qkrLocalState Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.json
	switch ($reset)
	{
		"Console"
		{
			"Resetting Console..."
			Remove-Files .\BruceOutput.txt, .\FredOutput.txt, .\MomOutput.txt
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Fred.qkr .\Fred.qkr.json
			Copy-Item .\Mom.qkr .\Mom.qkr.json
		}
		"ResetQKR"
		{
			"Resetting QKR"
			Remove-Files $brucePath, $momPath
		}
		"QKR"
		{
			"Configuring for QKR testing at: $global:qkrLocalState"
			Remove-Files .\BruceOutput.txt, .\FredOutput.txt, .\MomOutput.txt, $brucePath, $momPath
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Fred.qkr .\Fred.qkr.json
			Copy-Item .\Mom.qkr .\Mom.qkr.json
			Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr $brucePath
			Copy-Item .\Mom-mom0c866-8cb0-4a10-ad96-dfe5f9ebd98e.qkr $momPath
		}
		Default
		{
			"Resetting all..."
			Remove-Files .\BruceOutput.txt, .\FredOutput.txt, .\MomOutput.txt, $brucePath, $momPath
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Fred.qkr .\Fred.qkr.json
			Copy-Item .\Mom.qkr .\Mom.qkr.json
		}
	}
	
	Pop-Location
	if ($null -eq $showDescription -or $showDescription) {
		Get-Description ($reset -eq "ResetQKR" -or $reset -eq "QKR" -or $reset -eq "All" -or $null -eq $reset)
	}
}

function Remove-Files($files)
{
	foreach ($file in $files) {
		Remove-File $file
	}
}

function Remove-File($file)
{
	if (Test-Path $file)
	{
		"Deleting $file"
		Remove-Item $file
	}
}

function Run-Test
{
	$script = Join-Path $test "Test.txt"
	Reset-Test "Console" $true
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test $false
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
			Copy-Item .\Fred.qkr .\Fred.qkr.json
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

function Compare-Files($control, $file, $errorLevel, $merge)
{
	"Comparing: $control with $file"
	$controlText = Get-FilteredText $control $merge
	$fileText = Get-FilteredText $file $merge
	if ($null -eq $syncWindow)
	{
	if ($file -match "\.json$") {
		$syncWindow = 0
	} else {
		$syncWindow = [int32]::MaxValue
		}
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
