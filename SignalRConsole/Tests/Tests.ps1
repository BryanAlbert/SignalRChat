$global:totalErrorCount = 0

function Run-Tests
{
    $global:totalErrorCount = 0
    $testCount = 0
    Get-ChildItem Test* -Directory | ForEach-Object {
        $testCount++
        . (Join-Path $_ Tests.ps1)
        "`nRunning tests fromn: $tests"
        Reset-Test
        Run-Test
        $global:totalErrorCount += $global:errorCount
    }

    "`nTotal error count across $testCount tests: $global:totalErrorCount"
}