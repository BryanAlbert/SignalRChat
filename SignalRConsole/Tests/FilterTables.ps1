param(
    [Parameter(Mandatory = $true, HelpMessage = "The json file to filter")] [string] $InputFileName,
    [Parameter(Mandatory = $true, HelpMessage = "The number of tables to export")] [int] $TableCount,
    [Parameter(HelpMessage = "`$true to set Quizzed, Correct, etc. to 0")] [switch] $ResetCounts,
    [Parameter(HelpMessage = "The name of the file to write the json to")] [string] $OutputFileName)

# Reads from $InputFileName json containing a Tables array, writes $TableCount Table objects (each 
# containing the Base and its Cards array) to $OutputFileName with each Cards array containg the 
# source array's first $TableCount elements. If $ResetCounts is specified, each Card's Quizzed,
# Correct, TotalTime and BestTime properties are set to 0. 

if ($OutputFileName.Length -eq 0) {
    $OutputFileName = "FilteredTables.json"
}

Write-Host "Reading json from $InputFileName..."
$json = Get-Content $InputFileName | ConvertFrom-Json
"Filtering " + $json.Tables.Count + " tables down to $tableCount..."
$newJson = "{ `"Tables`": [] }" | ConvertFrom-Json -Depth 5
$json.Tables | Where-Object { $_.Base -le $tableCount} | ForEach-Object {
    $newTable = "{ `"Base`": $($_.Base), `"Cards`": [] }" | ConvertFrom-Json -Depth 5
    for ($i = 0; $i -lt $tableCount; $i++) {
        $newTable.Cards += $_.Cards[$i]
        if ($true -eq $resetCounts) {
            $newTable.Cards[$i].Quizzed = 0
            $newTable.Cards[$i].Correct = 0
            $newTable.Cards[$i].TotalTime = 0
            $newTable.Cards[$i].BestTime = 0        
        }
    }

    $newJson.Tables += $newTable
}

Write-Host "Writing json to $OutputFileName"
$newJson | ConvertTo-Json -Depth 5 | Out-File $OutputFileName
