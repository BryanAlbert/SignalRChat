$global:test = "Test-20"

function Get-Description($qkr)
{
	"`n${test}: Merging old account with tables with a new, empty account
	
	Old Mia online, New Mia comes online, merges, lists, exits, Old merges, lists, exits.
	
	Run Reset-Test with the following arguments to reset and configure:
	<none> Reset everything
	`$false Reset only Console json files
	QKR    Delete json files from QKR's LocalState folder
	Old    Configure QKR with Old json file
	New    Configure QKR with New json file
	First  Configure QKR with First json file
	Second Configure QKR with Second json file
	Third  Configure QKR with Third json file`n"

	if ($null -eq $qkr -or $qkr) {
	"`tTo test QKR, run Reset-Test New, run Start-TestFor Old then connect as Mia on QKR.
	Pop back to Home and verify that Mia is yellow, then close QKR. Check preliminary
	results with Check-Test New.
	
	Next, run Reset-Test Old, connect as Mia on QKR then run Start-TestFor New. Pop back
	to Home and verify that Mia is yellow, then close QKR. check results with
	Check-Test Old.`n"
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
		"QKR"
		{
			Remove-Files $oldPath, $newPath
		}
		"Old"
		{
			Remove-Files $oldPath, $newPath, .\New\Output.txt
			Copy-Item .\New\Mia.qkr .\New\Mia.qkr.json
			"Resetting QKR to Old at $global:qkrLocalState"
			Copy-Item .\Old\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.qkr $oldPath
		}
		"New"
		{
			Remove-Files $oldPath, $newPath, .\Old\Output.txt
			Copy-Item .\Old\Mia.qkr .\Old\Mia.qkr.json 
			"Resetting QKR to New at $global:qkrLocalState"
			Copy-Item .\New\Mia-qkrnew65-1468-4409-a21a-f5b4f000ee4f.qkr $newPath
		}
		"First"
		{
			"Resetting QKR to First at $global:qkrLocalState"
			"QKR is First"
		}
		"Second"
		{
			"Resetting QKR to Second at $global:qkrLocalState"
			"QKR is Second"
		}
		"Third"
		{
			"Resetting QKR to Third at $global:qkrLocalState"
			"QKR is Third"
		}
		$false
		{
			"Resetting Console..."
			Remove-Files .\Old\Output.txt, .\New\Output.txt
			Copy-Item .\New\Mia.qkr .\New\Mia.qkr.json
			Copy-Item .\Old\Mia.qkr .\Old\Mia.qkr.json 
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
		Get-Description $true
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

function Start-TestFor($age)
{
	$where = Join-Path $test $age
	"Calling Start-Chat in $where for Mia"
	Start-Chat (Join-Path $where "Input.txt") (Join-Path $where "Output.txt") $age
}


function Run-Test
{
	$script = Join-Path $test "Test.txt"
	"Running script $script"
	Reset-Test $false $true
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test
}

function Check-Test($stage)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	
	switch ($stage)
	{
		Old
		{
			$qkrPath = Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json
			Compare-Files .\Old\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283fControl.qkr $qkrPath 2
			Compare-Files .\New\MiaControl.qkr .\New\Mia.qkr.json 2
			Compare-Files $qkrPath .\New\Mia.qkr.json 2 $true
			Compare-Files .\New\Control.txt .\New\Output.txt 1
		}
		New
		{
			$qkrPath = Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json
			Compare-Files .\Old\MiaControl.qkr .\Old\Mia.qkr.json 2
			Compare-Files .\New\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283fControl.qkr $qkrPath 2
			Compare-Files .\Old\Mia.qkr.json $qkrPath 2 $true
			Compare-Files .\Old\Control.txt .\Old\Output.txt 1
		}
		Default
		{
			Compare-Files .\Old\MiaControl.qkr .\Old\Mia.qkr.json 2
			Compare-Files .\New\MiaControl.qkr .\New\Mia.qkr.json 2
			Compare-Files .\Old\Mia.qkr.json .\New\Mia.qkr.json 2 $true
			Compare-Files .\Old\Control.txt .\Old\Output.txt 1
			Compare-Files .\New\Control.txt .\New\Output.txt 1
		}
	}	


	"Warning count: $script:warningCount"
	"Error count: $script:errorCount"
	$global:totalWarningCount += $script:warningCount
	$global:totalErrorCount += $script:errorCount
	Pop-Location
}

function Update-ControlFiles($stage)
{
	Push-Location $test
	switch ($stage) {
		Old
		{
			"Updating QKR control files for $test from $global:qkrLocalState"
			Copy-Item (Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json) .\Old\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283fControl.qkr 
		}
		New
		{
			"Updating QKR control files for $test from $global:qkrLocalState"
			Copy-Item (Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json) .\New\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283fControl.qkr 
		}
		Default
		{
			"Updating control files for $test"
			Copy-Item .\Old\Output.txt .\Old\Control.txt
			Copy-Item .\New\Output.txt .\New\Control.txt
			Copy-Item .\Old\Mia.qkr.json .\Old\MiaControl.qkr
			Copy-Item .\New\Mia.qkr.json .\New\MiaControl.qkr
		}
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
	if (($file -match "\.json$") -and !$merge) {
		$syncWindow = 1
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
	Get-Content $file | ForEach-Object {
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
			elseif ($_ -match "^  ],") {
				$_ -replace ",", ""
			}
			elseif (!($_ -match "^\s*[0-9]+,?")) {
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
