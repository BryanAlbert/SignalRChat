$global:test = "Test-26"

function Get-Description($qkr)
{
	"`n${test}: Mia online, new Mia on second device merges, new Mia on third device, merges

	Mia online on First, Second with different data comes online and merges with First, Third
	with different data yet comes online and merges with First and Second.`n"

	if ($null -eq $qkr -or $qkr)
	{
	 "`tTo test QKR, TODO:...`n"
	}
}

function Reset-Test($showDescription)
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\First\Mia.qkr .\First\Mia.qkr.json
	Copy-Item .\Second\Mia.qkr .\Second\Mia.qkr.json
	Copy-Item .\Third\Mia.qkr .\Third\Mia.qkr.json
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
	Compare-Files .\First\MiaControl.qkr .\First\Mia.qkr.json 2
	Compare-Files .\Second\MiaControl.qkr .\Second\Mia.qkr.json 2
	Compare-Files .\Third\MiaControl.qkr .\Third\Mia.qkr.json 2
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json) 2
		Compare-Files .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072eControl.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json) 2
	}

	Compare-Files .\First\Mia.qkr.json .\Second\Mia.qkr.json 2 $true
	Compare-Files .\Second\Mia.qkr.json .\Third\Mia.qkr.json 2 $true
	Compare-Files .\First\Mia.qkr.json .\Third\Mia.qkr.json 2 $true

	Compare-Files .\First\MiaControl.txt .\First\MiaOutput.txt 1
	Compare-Files .\Second\MiaControl.txt .\Second\MiaOutput.txt 1
	Compare-Files .\Third\MiaControl.txt .\Third\MiaOutput.txt 1

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
		Copy-Item .\First\MiaOutput.txt .\First\MiaControl.txt
		Copy-Item .\Second\MiaOutput.txt .\Second\MiaControl.txt
		Copy-Item .\Third\MiaOutput.txt .\Third\MiaControl.txt
		Copy-Item .\First\Mia.qkr.json .\First\MiaControl.qkr
		Copy-Item .\Second\Mia.qkr.json .\Second\MiaControl.qkr
		Copy-Item .\Third\Mia.qkr.json .\Third\MiaControl.qkr
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
		if ($_ -match "`"DeviceId`": `"firste13-9aeb-41dc-aa7a-21d295f8d95d`",") {
			$match2 = "`"second65-1468-4409-a21a-f5b4f000ee4f`": [0-2],?"
			$match3 = "`"third7d6-2399-4557-ba66-4ccbc049e6ad`": [0-2],?"
			"  `"DeviceId`": `"<DeviceId>`","
		}
		elseif ($_ -match "`"DeviceId`": `"second65-1468-4409-a21a-f5b4f000ee4f`",") {
			$match2 = "`"firste13-9aeb-41dc-aa7a-21d295f8d95d`": [0-2],?"
			$match3 = "`"third7d6-2399-4557-ba66-4ccbc049e6ad`": [0-2],?"
			"  `"DeviceId`": `"<DeviceId>`","
		}
		elseif ($_ -match "`"DeviceId`": `"third7d6-2399-4557-ba66-4ccbc049e6ad`",") {
			$match2 = "`"firste13-9aeb-41dc-aa7a-21d295f8d95d`": [0-2],?"
			$match3 = "`"second65-1468-4409-a21a-f5b4f000ee4f`": [0-2],?"
			"  `"DeviceId`": `"<DeviceId>`","
		}
		elseif ($_ -match "Modified: ") {
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
			elseif ($null -ne $match2 -and $null -ne $match3 -and ($_ -match $match2 -or $_ -match $match3)) {
				"    <MergeIndexMatch>"
			}
			elseif ($_ -match "^\s*[0-9]+,?") {
				"                <Number>"
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
