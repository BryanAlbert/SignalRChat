$global:test = "Test-20"

function Get-Description($qkr)
{
	"`n${test}: Merging old account with no tables and new account with tables
	
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
	Get-Description $false
	dotnet.exe .\SignalRConsole.dll $script
	Check-Test $false
}

function Check-Test($checkQkr)
{
	Push-Location $test
	$script:warningCount = 0
	$script:errorCount = 0
	Compare-Files .\Old\MiaControl.qkr .\Old\Mia.qkr.json
	Compare-Files .\New\MiaControl.qkr .\New\Mia.qkr.json
	
	if ($null -eq $checkQkr -or $checkQkr)
	{
		Compare-Files .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4Control.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
		Compare-Files .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072eControl.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
	}

	Compare-Files .\Old\MiaControl.txt .\Old\MiaOutput.txt 2
	Compare-Files .\New\MiaControl.txt .\New\MiaOutput.txt 2

	Compare-Files .\Old\Mia.qkr.json .\New\Mia.qkr.json

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

function Compare-Files($control, $file, $errorLevel)
{
	"Comparing: $control with $file"
	if (((Compare-Object (Get-Content $control) (Get-Content $file)) | Measure-Object).Count -gt 0)
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

		Compare-Object (Get-Content $control) (Get-Content $file) | Format-Table -Property SideIndicator, InputObject
	}
}
