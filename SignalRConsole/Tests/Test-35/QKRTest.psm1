$global:test = "Test-35"

function Get-Description($verbose)
{
	"`n${test}: Leader goes away and follower disconnects
	
	Bruce online, Fred online, Bruce initiates chat, Fred goes away, Bruce
	disconnects, exits, Fred exits.`n"

	if ($null -eq $verbose -or $verbose)
	{
	"`tRun Reset-Test with one of the following arguments to reset and/or configure:
	<none>    Reset everything
	Console   Reset only Console json files
	ResetQKR  Delete json files from QKR's LocalState folder
	QKR       Configure for testing QKR
	
	To test QKR, run Reset-Test QKR then run Start-TestFor Bruce and Connect with
	Internet as Fred on QKR. When Bruce goes away, disconnect on QKR, verifying that
	Bruce disconnects and exits, then pop to Home.

	Next Connect Internet as Bruce on QKR, run Start-TestFor Fred, connect with Fred
	on QKR and tap Tables. When Fred disconnects (status is Listening on Console),
	pop back to Internet Connect, verify that we're no longer chatting and pop to
	Home. Verify that Fred exits and clsoe QKR. Test results with Check-Test `$true.`n"
	}
}

function Reset-Test($reset, $showDescription)
{
	"Resetting $test"
	Push-Location $test
	$brucePath = Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json
	$fredPath = Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json
	switch ($reset)
	{
		"Console"
		{
			"Resetting Console..."
			Remove-Files .\BruceOutput.txt, .\FredOutput.txt
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Fred.qkr .\Fred.qkr.json
		}
		"ResetQKR"
		{
			"Resetting QKR"
			Remove-Files $brucePath, $fredPath
		}
		"QKR"
		{
			"Configuring for QKR testing at: $global:qkrLocalState"
			Remove-Files .\BruceOutput.txt, .\FredOutput.txt, $brucePath, $fredPath
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Fred.qkr .\Fred.qkr.json
			Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr $brucePath
			Copy-Item .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.qkr $fredPath
		}
		Default
		{
			"Resetting all..."
			Remove-Files .\BruceOutput.txt, .\FredOutput.txt, $brucePath, $fredPath
			Copy-Item .\Bruce.qkr .\Bruce.qkr.json
			Copy-Item .\Fred.qkr .\Fred.qkr.json 
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

function Check-Test($checkQkr)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	Compare-Files .\Bruce.control.qkr .\Bruce.qkr.json 2
	Compare-Files .\Fred.control.qkr .\Fred.qkr.json 2
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
		Compare-Files .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.control.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json) 2
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
		Copy-Item (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.control.qkr 
		Copy-Item (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json) .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.control.qkr 
	}
	else
	{
		"Updating control files for $test"
		Copy-Item .\BruceOutput.txt .\BruceControl.txt
		Copy-Item .\FredOutput.txt .\FredControl.txt
		Copy-Item .\Bruce.qkr.json .\Bruce.control.qkr
		Copy-Item .\Fred.qkr.json .\Fred.control.qkr
	}

	Pop-Location
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
