$global:test = "Test-23"

function Get-Description($qkr)
{
	"`n${test}: After second merge, merge more changes from Old and New devices
	
	Old Mia online, New Mia comes online, merges, lists, exits, Old merges, lists, exits.`n"

	if ($null -eq $qkr -or $qkr)
	{
	"`tTo test QKR... Test results with Check-Test.`n"
	}
}

function Reset-Test($showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\New\Mia.qkr .\New\Mia.qkr.json
	Copy-Item .\Old\Mia.qkr .\Old\Mia.qkr.json
#	Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
#	Copy-Item .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
	Get-ChildItem -Recurse *Output.txt | ForEach-Object {
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
	Compare-Files .\Old\MiaControl.qkr .\Old\Mia.qkr.json 2
	Compare-Files .\New\MiaControl.qkr .\New\Mia.qkr.json 2
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
		Compare-Files .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072eControl.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json) 2
	}

	Compare-Files .\Old\Mia.qkr.json .\New\Mia.qkr.json 2 $true

	Compare-Files .\Old\MiaControl.txt .\Old\MiaOutput.txt 1
	Compare-Files .\New\MiaControl.txt .\New\MiaOutput.txt 1

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
		Copy-Item .\Old\MiaOutput.txt .\Old\MiaControl.txt
		Copy-Item .\New\MiaOutput.txt .\New\MiaControl.txt
		Copy-Item .\Old\Mia.qkr.json .\Old\MiaControl.qkr
		Copy-Item .\New\Mia.qkr.json .\New\MiaControl.qkr
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
