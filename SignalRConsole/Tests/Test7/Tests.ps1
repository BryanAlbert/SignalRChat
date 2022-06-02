
# Bruce, Fred and Mom online, Bruce and Fred befriend Mom, Bruce and Fred list, exit, Mom exits

$Global:tests = "Test7"
$global:errorCount = 0

function Reset-Test
{
	"Resetting $tests"
	Push-Location $tests
	Copy-Item .\BruceNoFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
	Copy-Item .\MomNoFriends.qkr .\Mom.qkr.json
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
	if (Test-Path .\FredOutput.txt) { Remove-Item .\FredOutput.txt }
	if (Test-Path .\MomOutput.txt) { Remove-Item .\MomOutput.txt}
	Pop-Location
}

function Run-Test
{
	$script = Join-Path $tests "Test.txt"
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Push-Location $tests
	
	# MomOutput.txt may be different between test runs, so we don't keep a MomControl.txt file. 
	$global:warningCount = 0
	$global:errorCount = 0
	Compare-Files .\BruceControl.txt .\BruceOutput.txt $true
	Compare-Files .\FredControl.txt .\FredOutput.txt $true
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json $false
	Compare-Files .\FredControl.qkr .\Fred.qkr.json $false
	Compare-Files .\MomControl.qkr .\Mom.qkr.json $false

	"Total warning count: $global:warningCount"
	"Total error count: $global:errorCount"
	Pop-Location
}

function Print-Files
{
	"Results for $tests"
	Push-Location $tests
	Get-ChildItem *.qkr.json | ForEach-Object { $_.Name; Get-Content $_; "" }
	Get-ChildItem *Output.txt | ForEach-Object { $_.Name; Get-Content $_; "" }
	Pop-Location
}

function Update-ControlFiles
{
	"Updating control files for $tests"
	Push-Location $tests
	Copy-Item .\BruceOutput.txt .\BruceControl.txt
	Copy-Item .\FredOutput.txt .\FredControl.txt
	Copy-Item .\Bruce.qkr.json .\BruceControl.qkr
	Copy-Item .\Fred.qkr.json .\FredControl.qkr
	Copy-Item .\Mom.qkr.json .\MomControl.qkr
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
