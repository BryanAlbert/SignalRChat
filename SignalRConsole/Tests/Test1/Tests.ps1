# Bruce online, waits for Fred, Fred online waits for Bruce to leave, 
# Bruce lists friends and goes offline, Fred lists friends and goes offline. 

$Global:tests = "Test1"
$global:errorCount = 0

function Reset-Test
{
	"Resetting $tests"
	Push-Location $tests
	Copy-Item .\BruceFriends.qkr .\Bruce.qkr.json
	Copy-Item .\FredFriends.qkr .\Fred.qkr.json
	if (Test-Path .\BruceOutput.txt) { Remove-Item .\BruceOutput.txt }
	if (Test-Path .\FredOutput.txt) { Remove-Item .\FredOutput.txt }
	Pop-Location
}

function Run-Test
{
	$script = Join-Path $tests "Test.txt"
	"Running script $script"
	dotnet.exe .\SignalRConsole.dll $script
	Push-Location $tests
	$global:errorCount = 0
	Compare-Files .\BruceControl.txt .\BruceOutput.txt
	Compare-Files .\FredControl.txt .\FredOutput.txt
	Compare-Files .\BruceControl.qkr .\Bruce.qkr.json
	Compare-Files .\FredControl.qkr .\Fred.qkr.json

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
	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
}


function Compare-Files($control, $file)
{
	"Comparing: $control with $file"
	if (((Compare-Object (Get-Content $control) (Get-Content $file)) | Measure-Object).Count -gt 0) {
		"Error: $file has unexpected output:"
		Compare-Object (Get-Content $control) (Get-Content $file)
		$global:errorCount++
	}
}
