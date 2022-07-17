$global:test = "Test-05"

function Get-Description($qkr)
{
	"`n${test}: Bruce adds Fred and goes offline before Fred accepts
	
	Bruce and Fred online, Bruce adds Fred, goes offline, Fred accepts,
	goes offline, Bruce comes online, Fred comes online, Bruce lists and
	goes offline, Fred lists and goes offline.`n"

	if ($null -eq $qkr -or $qkr)
	{
	"`tTo test QKR, run Test-QKRas Bruce 1. Log in as Fred on QKR, after Bruce
	exits, accept friend request and pop to Home. Run Test-QKRas Bruce 2,
	log in as Fred again, pop to Home and verify that Bruce exits.
	
	Next log in as Bruce on QKR, run Test-QKRas Fred 1, add Fred on QKR, pop
	to Home and verify that Fred exits. Run Test-QKRas Fred 2, log in as Bruce
	again, verify that Fred exits then pop to Home. Check results with
	Check-Test.`n"
	}
}

function Reset-Test($showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceNoFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
	Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
	Copy-Item .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
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
	Check-Test $false
}

function Check-Test($checkQkr)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json 2
	Compare-Files .\FredControl.qkr .\Fred.qkr.json 2
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
		Compare-Files .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072eControl.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json) 2
	}

	Compare-Files .\BruceControl.txt .\BruceOutput.txt 1
	Compare-Files .\FredControl.txt .\FredOutput.txt 1

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
