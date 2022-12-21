$global:test = "Test-25"

function Get-Description($verbose)
{
	"`n${test}: Old device with Addition tables merged with new deivce with Addition tables
	
	Old Mia online, New Mia comes online, merges, lists, exits, Old merges, lists, exits.`n"

	if ($null -eq $verbose -or $verbose)
	{
	"`tRun Reset-Test with one of the following arguments to reset and/or configure:
	<none>    Reset everything
	Console   Reset only Console json files
	ResetQKR  Delete json files from QKR's LocalState folder
	Old       Configure QKR with Old json file
	New       Configure QKR with New json file

	To test QKR, run Reset-Test New, run Start-TestFor Old then Connect Internet as Mia on
	QKR. Pop back to Home and verify that Mia is yellow and that the console exits, then
	close QKR. Check preliminary results with Check-Test New.
	
	Next, run Reset-Test Old, launch QKR and note that Mia is turquoise, Connect Internet
	as Mia on QKR then run Start-TestFor New. Pop back to Home and verify that Mia is
	yellow and that the console exits, then close QKR. Check results with Check-Test Old.`n"
	}
}

function Reset-Test($reset, $showDescription)
{
	"Resetting $test"
	Push-Location $test
	
	$oldPath = Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json
	$newPath = Join-Path $global:qkrLocalState Mia-qkrnew65-1468-4409-a21a-f5b4f000ee4f.json
	
	switch ($reset)
	{
		"Console"
		{
			"Resetting Console..."
			Remove-Files .\Old\Output.txt, .\New\Output.txt
			Copy-Item .\New\Mia.qkr .\New\Mia.qkr.json
			Copy-Item .\Old\Mia.qkr .\Old\Mia.qkr.json
		}
		"ResetQKR"
		{
			"Resetting QKR"
			Remove-Files $oldPath, $newPath
		}
		"Old"
		{
			"Configuring for testing Old on QKR at $global:qkrLocalState"
			Remove-Files $oldPath, $newPath, .\New\Output.txt
			Copy-Item .\New\Mia.qkr .\New\Mia.qkr.json
			Copy-Item .\Old\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.qkr $oldPath
		}
		"New"
		{
			"Configuring for testing New on QKR at $global:qkrLocalState"
			Remove-Files $oldPath, $newPath, .\Old\Output.txt
			Copy-Item .\Old\Mia.qkr .\Old\Mia.qkr.json
			Copy-Item .\New\Mia-qkrnew65-1468-4409-a21a-f5b4f000ee4f.qkr $newPath
		}
		Default
		{
			"Resetting all..."
			Remove-Files $oldPath, $newPath, .\Old\Output.txt, .\New\Output.txt
			Copy-Item .\New\Mia.qkr .\New\Mia.qkr.json
			Copy-Item .\Old\Mia.qkr .\Old\Mia.qkr.json
		}
	}
	
	Pop-Location
	if ($null -eq $showDescription -or $showDescription) {
		Get-Description ($reset -eq "ResetQKR" -or $reset -eq "Old" -or $reset -eq "New" -or $reset -eq "All" -or $null -eq $reset)
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

function Check-Test($stage)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	$qkrPath = Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json
	
	switch ($stage)
	{
		Old
		{
			Compare-Files .\Old\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr $qkrPath 1 1
			Compare-Files .\New\Mia.control.qkr .\New\Mia.qkr.json 1 1
			Compare-Files $qkrPath .\New\Mia.qkr.json 2 100 $true
			Compare-Files .\New\Control.txt .\New\Output.txt 1 2
			$mergeTest = "-checkmerge -qkr Old $global:qkrLocalState $test Mia"
		}
		New
		{
			Compare-Files .\Old\Mia.control.qkr .\Old\Mia.qkr.json 1 1
			Compare-Files .\New\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr $qkrPath 1 1
			Compare-Files .\Old\Mia.qkr.json $qkrPath 2 100 $true
			Compare-Files .\Old\Control.txt .\Old\Output.txt 1 2
			$mergeTest = "-checkmerge -qkr New $global:qkrLocalState $test Mia"
		}
		Default
		{
			Compare-Files .\Old\Mia.control.qkr .\Old\Mia.qkr.json 1 1
			Compare-Files .\New\Mia.control.qkr .\New\Mia.qkr.json 1 1
			Compare-Files .\Old\Mia.qkr.json .\New\Mia.qkr.json 2 100 $true
			Compare-Files .\Old\Control.txt .\Old\Output.txt 1 2
			Compare-Files .\New\Control.txt .\New\Output.txt 1 2
			$mergeTest = "-checkmerge $test Mia"
		}
	}	
	
	Pop-Location
	"Calling SignalRConsole with: $mergeTest"
	dotnet.exe .\SignalRConsole.dll $mergeTest.Split()
	if ($LASTEXITCODE -lt 0) {
		$script:errorCount++
	}

	"`nWarning count: $script:warningCount"
	"Error count: $script:errorCount"
	$global:totalWarningCount += $script:warningCount
	$global:totalErrorCount += $script:errorCount
}

function Update-ControlFiles($stage)
{
	Push-Location $test
	switch ($stage) {
		Old
		{
			"Updating QKR control files for $test from $global:qkrLocalState"
			Copy-Item (Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json) .\Old\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr 
		}
		New
		{
			"Updating QKR control files for $test from $global:qkrLocalState"
			Copy-Item (Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json) .\New\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr 
		}
		Default
		{
			"Updating control files for $test"
			Copy-Item .\Old\Output.txt .\Old\Control.txt
			Copy-Item .\New\Output.txt .\New\Control.txt
			Copy-Item .\Old\Mia.qkr.json .\Old\Mia.control.qkr
			Copy-Item .\New\Mia.qkr.json .\New\Mia.control.qkr
		}
	}

	Pop-Location
}

function Compare-Files($control, $file, $errorLevel, $syncWindow, $merge)
{
	"Comparing: $control with $file"
	$controlText = Get-FilteredText $control $merge
	$fileText = Get-FilteredText $file $merge

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
		elseif ($_ -match "`"Modified`": `".{19}`"") {
			$_ -replace "`"Modified`": `".{19}`",?", "`"Modified`": `"<Date>`""
		}
		elseif ($_ -match "Modified Date: .{19}") {
			$_ -replace "Modified Date: .{19}", "Modified Date: `"<Date>`""
		}
		elseif ($merge)
		{
			# comparing old json to new json, ignore created date, strip "DeviceId": etc. to match one file's MergeIndex entry
			# with the other's DeviceId, ignore counts in merge data
			if ($_ -match "`"Created`": `".{19}`",") {
				$_ -replace ".{19}`",$", "<Date>`""
			}
			elseif ($_ -match "`"DeviceId`": `"(con|qkr)(mia|new)[a-f0-9]{2}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`"") {
				($_ -replace "`"DeviceId`": ", "  " -replace ",", "")
			}
			elseif ($_ -match "`"(con|qkr)(mia|new)[a-f0-9]{2}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`": [0-9]") {
				($_ -replace ": [0-9]", "")
			}
			elseif ($_ -match "`"BluetoothDeviceName`": null,") {
				$_ -replace ",", ""
			}
			elseif ($_ -match "^  ],$") {
				$_ -replace ",$", ""
			}
			elseif (!($_ -match "^\s*[0-9]+,?$")) {
				$_
			}
		}
		elseif ($_ -match "(con|qkr).{5}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}") {
			$_ -replace "(con|qkr)", "xxx"
		}
		else {
			$_
		}
	}
}
