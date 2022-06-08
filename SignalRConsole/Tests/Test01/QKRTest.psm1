$global:test = "Test01"

function Get-Description
{
	"`n${test}: Friends listing friends"
	"
	Bruce online, Fred online, Bruce lists and goes offline, Fred lists and
	goes offline.`n"	
}

function Reset-Test
{
	"Resetting $global:test"
	Push-Location $global:test
	Copy-Item .\BruceFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredFriends.qkr .\Fred.qkr.json
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
	if (Test-Path .\FredOutput.txt) { Remove-Item .\FredOutput.txt }
	Pop-Location
}

function Run-Test
{
	$script = Join-Path $global:test "Test.txt"
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Push-Location $global:test
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

function Print-Files($inputFiles)
{
	Push-Location $global:test
	if ($null -ne $inputFiles)
	{
		"Input files for $($global:test):"
		Get-ChildItem Test.txt, *Input*.txt | ForEach-Object {
			$_.Name
			Get-Content $_
			""
		}
	}
	else
	{
		"Output files for $($global:test):"
		Get-ChildItem *.qkr.json | ForEach-Object { $_.Name; Get-Content $_; "" }
		Get-ChildItem *Output.txt | ForEach-Object { $_.Name; Get-Content $_; "" }
	}

	Pop-Location
}

function Update-ControlFiles
{
	"Updating control files for $global:test"
	Push-Location $global:test
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
