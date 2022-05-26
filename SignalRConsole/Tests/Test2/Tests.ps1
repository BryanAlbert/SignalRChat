
# Fred online blocks, Bruce online, adds Fred, waits for Fred online then lists
# friends and exists, meanwhile Fred accepts, waits for Bruce to go offline, lists, exits

$Global:tests = "Test2"

function Reset-Test
{
	"Resetting $tests"
	Push-Location $tests
	Copy-Item .\BruceNoFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredNoFriends.qkr .\Fred.qkr.json
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
	if (Test-Path .\FredOutput.txt) { Remove-Item .\FredOutput.txt }
	Pop-Location
}

function Run-Test
{
	$errorCount = 0
	$script = Join-Path $tests "Test.txt"
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Push-Location $tests
	Compare-Files .\BruceControl.txt .\BruceOutput.txt
	Compare-Files .\FredControl.txt .\FredOutput.txt
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json
	Compare-Files .\FredControl.qkr .\Fred.qkr.json

	"Total error count: $errorCount"
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
	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
}

function Compare-Files($control, $file)
{
	"Comparing: $control with $file"
	if ((((Compare-Object (Get-Content $control) (Get-Content $file))) | Measure-Object).Count -gt 0) {
		"Error: $file has unexpected output:"
		Compare-Object (Get-Content $control) (Get-Content $file)
		$errorCount++
	}
}
