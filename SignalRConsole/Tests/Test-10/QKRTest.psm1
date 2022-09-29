$global:test = "Test-10"

function Get-Description($verbose)
{
	"`n${test}: Bruce pending friends with Fred, adds Fred

	Bruce pending friendship with Fred, comes online and adds Fred, gets a message,
	lists and goes offline.`n"

	if ($null -eq $verbose -or $verbose)
	{
	"`tRun Reset-Test with one of the following arguments to reset and/or configure:
	<none>    Reset everything
	Console   Reset only Console json files
	ResetQKR  Delete json files from QKR's LocalState folder
	QKR       Configure for testing QKR
	
	To test QKR, run Reset-Test QKR then Connect Internet as Bruce on QKR and add
	Fred, (fred@gmail.com), verify the status message and close QKR. Check results
	with Check-Test `$true.`n"
	}
}

function Reset-Test($reset, $showDescription)
{
	"Resetting $test"
	Push-Location $test
	$brucePath = Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json
	switch ($reset)
	{
		"Console"
		{
			"Resetting Console..."
			Remove-Files .\BruceOutput.txt
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
		}
		"ResetQKR"
		{
			"Resetting QKR"
			Remove-Files $brucePath
		}
		"QKR"
		{
			"Configuring for QKR testing at: $global:qkrLocalState"
			Remove-Files .\BruceOutput.txt, $brucePath
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr $brucePath
		}
		Default
		{
			"Resetting all..."
			Remove-Files .\BruceOutput.txt, $brucePath
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
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
	Reset-Test "Console" $true
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test $false
}

function Check-Test($checkQkr)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
	}
	else
	{
		Compare-Files .\Bruce.control.qkr .\Bruce.qkr.json 2
		Compare-Files .\BruceControl.txt .\BruceOutput.txt 1
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
		Copy-Item (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.control.qkr 
	}
	else
	{
		"Updating control files for $test"
		Copy-Item .\BruceOutput.txt .\BruceControl.txt
		Copy-Item .\Bruce.qkr.json .\Bruce.control.qkr
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
