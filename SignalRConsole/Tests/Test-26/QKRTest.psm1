$global:test = "Test-26"

function Get-Description($verbose)
{
	"`n${test}: Mia online, new Mia on second device comes online, merges, new Mia on third device
	comes online, merges

	Mia with data online on First, Mia on a Second device with different data comes online
	and merges with First, Mia on Third device with different data yet and a friend comes
	online and merges with First and Second asynchronously.`n"

	if ($null -eq $verbose -or $verbose)
	{
	"`tRun Reset-Test with one of the following arguments to reset and/or configure:
	<none>    Reset everything
	Console   Reset only Console json files
	ResetQKR  Delete json files from QKR's LocalState folder
	First     Configure QKR with Old json file
	Second    Configure QKR with New json file

	To test QKR, run Reset-Test First and launch QKR, log in as Mia and note that her color
	is Yellow. Connect Internet as Mia and note that Mia is friendless. Run Start-TestFor
	Second in one console and Start-TestFor Third in another. Verify that QKR gains friend
	Bruce, pop back to Home, verify that Mia is turquoise and that the consoles exit, then 
	close QKR. Check preliminary results with Check-Test First.
	
	Next, run Reset-Test Second, run Start-TestFor First in one console, launch QKR and
	Connect Internet as Mia. Note that Mia is friendless and run Start-TestFor Third in 
	another console. Verify that QKR gains friend Bruce, pop back to Home and verify that
	Mia is turquoise then close QKR, verifying that both consoles exit. Check results with
	Check-Test Second.`n"
	}
}

function Reset-Test($reset, $showDescription)
{
	"Resetting $test"
	Push-Location $test
	
	$firstPath = Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json
	$secondPath = Join-Path $global:qkrLocalState Mia-second65-1468-4409-a21a-f5b4f000ee4f.json
	
	switch ($reset)
	{
		"Console"
		{
			"Resetting Console..."
			Remove-Files .\First\Output.txt, .\Second\Output.txt, .\Third\Output.txt
			Copy-Item .\First\Mia.qkr .\First\Mia.qkr.json
			Copy-Item .\Second\Mia.qkr .\Second\Mia.qkr.json
			Copy-Item .\Third\Mia.qkr .\Third\Mia.qkr.json
		}
		"ResetQKR"
		{
			"Resetting QKR"
			Remove-Files $firstPath, $secondPath
		}
		"First"
		{
			"Configuring for testing First on QKR at $global:qkrLocalState"
			Remove-Files $firstPath, $secondPath, .\First\Output.txt, .\Second\Output.txt, .\Third\Output.txt
			Copy-Item .\Second\Mia.qkr .\Second\Mia.qkr.json
			Copy-Item .\Third\Mia.qkr .\Third\Mia.qkr.json
			Copy-Item .\First\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.qkr $firstPath
		}
		"Second"
		{
			"Configuring for testing Second on QKR at $global:qkrLocalState"
			Remove-Files $firstPath, $secondPath, .\First\Output.txt, .\Second\Output.txt, .\Third\Output.txt
			Copy-Item .\First\Mia.qkr .\First\Mia.qkr.json
			Copy-Item .\Third\Mia.qkr .\Third\Mia.qkr.json
			Copy-Item .\Second\Mia-second65-1468-4409-a21a-f5b4f000ee4f.qkr $secondPath
		}
		Default
		{
			"Resetting all..."
			Remove-Files $firstPath, $secondPath, .\First\Output.txt, .\Second\Output.txt, .\Third\Output.txt
			Copy-Item .\First\Mia.qkr .\First\Mia.qkr.json
			Copy-Item .\Second\Mia.qkr .\Second\Mia.qkr.json
			Copy-Item .\Third\Mia.qkr .\Third\Mia.qkr.json
		}
	}
	
	Pop-Location
	if ($null -eq $showDescription -or $showDescription) {
		Get-Description ($reset -eq "ResetQKR" -or $reset -eq "First" -or $reset -eq "Second" -or $reset -eq "All" -or $null -eq $reset)
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
		First
		{
			Compare-Files .\First\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr $qkrPath 1 1
			Compare-Files .\Second\Mia.control.qkr .\Second\Mia.qkr.json 1 1
			Compare-Files .\Third\Mia.control.qkr .\Third\Mia.qkr.json 1 1
			Compare-Files $qkrPath .\Second\Mia.qkr.json 2 100 $true
			Compare-Files .\Second\Mia.qkr.json .\Third\Mia.qkr.json 2 100 $true
			Compare-Files $qkrPath .\Third\Mia.qkr.json 2 100 $true
			Compare-Files .\Second\Control.txt .\Second\Output.txt 1 2
			Compare-Files .\Third\Control.txt .\Third\Output.txt 1 2
			$mergeTest = "-checkmerge -qkr First $global:qkrLocalState $test Mia"
		}
		Second
		{
			Compare-Files .\First\Mia.control.qkr .\First\Mia.qkr.json 1 1
			Compare-Files .\Second\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr $qkrPath 1 1
			Compare-Files .\Third\Mia.control.qkr .\Third\Mia.qkr.json 1 1
			Compare-Files .\First\Mia.qkr.json $qkrPath 2 100 $true
			Compare-Files $qkrPath .\Third\Mia.qkr.json 2 100 $true
			Compare-Files .\First\Mia.qkr.json .\Third\Mia.qkr.json 2 100 $true
			Compare-Files .\First\Control.txt .\First\Output.txt 1 2
			Compare-Files .\Third\Control.txt .\Third\Output.txt 1 2
			$mergeTest = "-checkmerge -qkr Second $global:qkrLocalState $test Mia"
		}
		Default
		{
			Compare-Files .\First\Mia.control.qkr .\First\Mia.qkr.json 1 1
			Compare-Files .\Second\Mia.control.qkr .\Second\Mia.qkr.json 1 1
			Compare-Files .\Third\Mia.control.qkr .\Third\Mia.qkr.json 1 1
			Compare-Files .\First\Mia.qkr.json .\Second\Mia.qkr.json 2 100 $true
			Compare-Files .\Second\Mia.qkr.json .\Third\Mia.qkr.json 2 100 $true
			Compare-Files .\First\Mia.qkr.json .\Third\Mia.qkr.json 2 100 $true
			Compare-Files .\First\Control.txt .\First\Output.txt 1 2
			Compare-Files .\Second\Control.txt .\Second\Output.txt 1 2
			Compare-Files .\Third\Control.txt .\Third\Output.txt 1 2
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
		First
		{
			"Updating QKR control files for $test from $global:qkrLocalState"
			Copy-Item (Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json) .\First\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr 
		}
		Second
		{
			"Updating QKR control files for $test from $global:qkrLocalState"
			Copy-Item (Join-Path $global:qkrLocalState Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.json) .\Second\Mia-mia38308-9a9b-4a6b-9db9-9e9b6238283f.control.qkr 
		}
		Default
		{
			"Updating control files for $test"
			Copy-Item .\First\Output.txt .\First\Control.txt
			Copy-Item .\Second\Output.txt .\Second\Control.txt
			Copy-Item .\Third\Output.txt .\Third\Control.txt
			Copy-Item .\First\Mia.qkr.json .\First\Mia.control.qkr
			Copy-Item .\Second\Mia.qkr.json .\Second\Mia.control.qkr
			Copy-Item .\Third\Mia.qkr.json .\Third\Mia.control.qkr
		}
	}

	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
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
			elseif ($_ -match "`"DeviceId`": `"(first|third)[a-f0-9]{3}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`"") {
				($_ -replace "`"DeviceId`": ", "  " -replace ",", "")
			}
			elseif ($_ -match "`"DeviceId`": `"second[a-f0-9]{2}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`"") {
				($_ -replace "`"DeviceId`": ", "  " -replace ",", "")
			}
			elseif ($_ -match "`"(con|qkr)(mia|new)[a-f0-9]{2}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`": [0-9]") {
				($_ -replace ": [0-9]", "")
			}
			elseif ($_ -match "`"(first|third)[a-f0-9]{3}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`": [0-9],?") {
				($_ -replace ": [0-9],?", "")
			}
			elseif ($_ -match "`"second[a-f0-9]{2}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`": [0-9],?") {
				($_ -replace ": [0-9],?", "")
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
		elseif ($_ -match "^Merging data from.+, Id mia[a-f0-9]{5}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}, on device") {
			# remove Id printed when processing the old device's merge so that it matches a the Id printed when processing a new device's
			# merge, which happens if First and Second are merged before Second and Third, for example (and Second has the original Id)  
			$_ -replace ", Id mia[a-f0-9]{5}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12},", ""
		}
		elseif ($_ -match "`"(first|third)[a-f0-9]{3}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`": [0-9],?") {
			($_ -replace ": [0-9],?", ": #")
		}
		elseif ($_ -match "`"second[a-f0-9]{2}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`": [0-9],?") {
			($_ -replace ": [0-9],?", ": #")
		}
		elseif ($_ -match "[a-f0-9]{5}-[a-f0-9]{4}-[a-fel0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12},") {
		}
		elseif ($_ -match "^\s*[0-9]+,$") {
			$_ -replace ",$", ""
		}
		else {
			$_
		}
	}
}
