# run Import-Module QKRTests.psm1 to load these functions, Remove-Module QKRTests 
# to unload them: 

# target for cofiguring QKR with json racer files
$global:qkrLocalState = "C:\Users\bryan\AppData\Local\Packages\380534de-923a-4fc1-8a77-eb331c3f02d7_exfef3wb0y8fm\LocalState\"

function Get-Descriptions($number)
{
    if ($null -eq $number)
    {
        Get-ChildItem -Recurse QKRTest.psm1 | ForEach-Object {
            Import-Module -DisableNameChecking -Force $_
            Get-Description
        }
    }
    else
    {
        Load-Test $number $true
        Get-Description
    }
}

function Load-Test($number, $discard)
{
    if ($number -lt 10) { $number = "0$number" }
	$testPath = Join-Path ".\Test-$number" QKRTest.psm1
	"Loading $testPath"
    if ($null -eq $discard -or $discard -ne $true)
    {
        Import-Module -DisableNameChecking -Global -Force $testPath
    }
    else
    {
        Import-Module -DisableNameChecking -Force $testPath
    }
}

function Reset-Tests
{
    $testCount = 0
    Get-ChildItem .\Test-* -Directory | ForEach-Object {
        $testCount++
        Import-Module -DisableNameChecking -Force (Join-Path $_ QKRTest.psm1)
        "`nResetting test from: $test"
        Reset-Test
    }

    "Reset $testCount tests."
}

function Run-Tests
{
    $global:totalWarningCount = 0
    $global:totalErrorCount = 0
    $testCount = 0
    Get-ChildItem .\Test-* -Directory | ForEach-Object {
        $testCount++
        Import-Module -DisableNameChecking -Force (Join-Path $_ QKRTest.psm1)
        "`nRunning test from: $test"
        Reset-Test
        Run-Test
        $global:totalWarningCount += $global:warningCount
        $global:totalErrorCount += $global:errorCount
    }

    "`nTotal warning count across $testCount tests: $global:totalWarningCount"
    "Total error count across $testCount tests: $global:totalErrorCount"
}

function Print-Files($inputFiles)
{
	Push-Location $global:test
	if ($null -ne $inputFiles)
	{
		"Input files for $($global:test):"
        Get-ChildItem *.qkr -Exclude *control.qkr | ForEach-Object {
            $_.Name
            Get-Content $_
            ""
        }
        
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

function Start-Chat
{
    if ($null -eq $args[0])
    {
        "Launching dotnet.exe .\SignalRConsole.dll"
        dotnet.exe .\SignalRConsole.dll
    }
    elseif (Test-Path $args[0])
    {
        "Launching dotnet.exe .\SignalRConsole.dll $args"
        dotnet.exe .\SignalRConsole.dll $args
    }
    else
    {
        "Launching dotnet.exe .\SignalRConsole.dll $global:test $args"
        dotnet.exe .\SignalRConsole.dll $global:test $args
    }
}
