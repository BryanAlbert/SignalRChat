function Reset-Tests
{
    $testCount = 0
    Get-ChildItem Test* -Directory | ForEach-Object {
        $testCount++
        . (Join-Path $_ Tests.ps1)
        "`nResetting test fromn: $tests"
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
        . (Join-Path $_ Tests.ps1)
        "`nRunning test fromn: $tests"
        Reset-Test
        Run-Test
        $global:totalWarningCount += $global:warningCount
        $global:totalErrorCount += $global:errorCount
    }

    "`nTotal warning count across $testCount tests: $global:totalWarningCount"
    "Total error count across $testCount tests: $global:totalErrorCount"
}