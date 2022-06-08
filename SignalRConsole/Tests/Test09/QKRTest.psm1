$global:test = "Test9"

function Describe-Test
{
	"`n${test}: Bruce friends with Fred, adds Fred"
	"
	Bruce, friends with Fred, comes online and adds Fred, gets a message,
	lists and goes offline.`n"
}

function Reset-Test
{
	"Resetting $test"
	Push-Location $test
	Copy-Item .\BruceFriends.qkr .\Bruce.qkr.json
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
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
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false

	"Total warning count: $global:warningCount"
	"Total error count: $global:errorCount"
	Pop-Location
}

function Print-Files
{
	"Results for $test"
	Push-Location $test
	Get-ChildItem *.qkr.json | ForEach-Object { $_.Name; Get-Content $_; "" }
	Get-ChildItem *Output.txt | ForEach-Object { $_.Name; Get-Content $_; "" }
	Pop-Location
}

function Update-ControlFiles
{
	"Updating control files for $test"
	Push-Location $test
	Copy-Item .\BruceOutput.txt .\BruceControl.txt
	Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
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
