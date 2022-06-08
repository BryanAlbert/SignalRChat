# run Import-Module QKRTests.psm1 to load these functions, Remove-Module QKRTests 
# to unload them: 

function Describe-Tests($number)
{
    if ($null -eq $number)
    {
        Get-ChildItem -Recurse QKRTest.psm1 | ForEach-Object {
            Import-Module -DisableNameChecking -Force $_
            Describe-Test
        }
    }
    else
    {
        Load-Test $number $true
        Describe-Test
    }
}

function Load-Test($number, $discard)
{
    if ($number -lt 10) { $number = "0$number" }
	$testPath = Join-Path ".\Test$number" QKRTest.psm1
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
    Get-ChildItem Test* -Directory | ForEach-Object {
        $testCount++
        . (Join-Path $_ Test.ps1)
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
    Get-ChildItem Test* -Directory | ForEach-Object {
        $testCount++
        . (Join-Path $_ Test.ps1)
        "`nRunning test from: $test"
        Reset-Test
        Run-Test
        $global:totalWarningCount += $global:warningCount
        $global:totalErrorCount += $global:errorCount
    }

    "`nTotal warning count across $testCount tests: $global:totalWarningCount"
    "Total error count across $testCount tests: $global:totalErrorCount"
}

