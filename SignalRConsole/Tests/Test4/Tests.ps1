
# Bruce online, adds Fred, Fred online, Fred accepts, lists, goes offline, Bruce lists, goes offline

$Global:tests = "Test4"

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
	if (((Compare-Object (Get-Content .\BruceControl.txt) (Get-Content .\BruceOutput.txt)) | Measure-Object).Count -gt 0) {
		"Error: BruceOutput.txt has unexpected output."
		$errorCount++
	}
	if ((((Compare-Object (Get-Content .\FredControl.txt) (Get-Content .\FredOutput.txt))) | Measure-Object).Count -gt 0) {
		"Error: FredOutput.txt has unexpected output."
		$errorCount++
	}

	"Total error count: $errorCount"
	Pop-Location
}

function List-Results
{
	"Results for $tests"
	Push-Location $tests
	Get-ChildItem *.qkr.json | ForEach-Object { $_.Name; Get-Content $_; "" }
	"BruceOutput.txt:"
	Get-Content .\BruceOutput.txt
	"FredOutput.txt:"
	Get-Content .\FredOutput.txt
	Pop-Location
}

function Update-SignalRConsole
{
	Get-ChildItem ..\bin\Debug\netcoreapp3.1\* -File | Copy-Item -Destination .
}
