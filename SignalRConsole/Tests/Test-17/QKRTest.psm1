$global:test = "Test-17"

function Get-Description
{
	"`n${test}: Bruce adds Fred, offline, Fred online, adds Bruce, Bruce online"
	"
	Bruce comes online, adds Fred, lists and goes offline, Fred comes online, adds Bruce,
	Bruce comes online, accepts request, lists and goes offline, Fred lists and goes offline.`n"
}

function Reset-Test
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceNoFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
	Copy-Item .\Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.qkr (Join-Path $global:qkrLocalState Bruce-brucef68-3c37-4aef-b8a6-1649659bbbc4.json)
	Copy-Item .\Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.qkr (Join-Path $global:qkrLocalState Fred-fredac24-3f25-41e0-84f2-3f34f54d072e.json)
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
	if (Test-Path .\FredOutput.txt) { Remove-Item .\FredOutput.txt }
	Pop-Location
}

function Run-Test
{
	$script = Join-Path $test "Test.txt"
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Push-Location $test
	$global:warningCount = 0
	$global:errorCount = 0
	Compare-Files .\BruceControl.txt .\BruceOutput.txt $true
	Compare-Files .\FredControl.txt .\FredOutput.txt $true
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false
	Compare-Files .\FredControl.qkr .\Fred.qkr.json $false

	"Total warning count: $global:warningCount"
	"Total error count: $global:errorCount"
	Pop-Location
}

function Update-ControlFiles
{
	"Updating control files for $test"
	Push-Location $test
	Copy-Item .\BruceOutput.txt .\BruceControl.txt
	Copy-Item .\FredOutput.txt .\FredControl.txt
	Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
	Copy-Item .\Fred.qkr.json .\FredControl.qkr
	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
}


function Compare-Files($control, $file, $logFile)
{
	"Comparing: $control with $file"
	if (((Compare-Object (Get-Content $control) (Get-Content $file)) | Measure-Object).Count -gt 0) {
		if ($logFile)
		{
			"Warning: $file has unexpected output:"
			$global:warningCount++
		}
		else
		{
			"Error: $file has unexpected output:"
			$global:errorCount++
		}

		Compare-Object (Get-Content $control) (Get-Content $file)
	}
}
