function Test1($runNew)
{
    "Copying Old MiaUnmerged.qkr.json and New MiaNewDevice.qkr.json"
    Copy-Item .\Old\Tests\MiaUnmerged.qkr.json .\Old\Mia.qkr.json
    Copy-Item .\New\Tests\MiaNewDevice.qkr.json .\New\Mia.qkr.json
    "`nRun as necessary: Import-Module -Force ..\..\..\Tests\QKRTests.psm1 -DisableNameChecking"
    "Run Start-Chat .\New Bruce in another console...`n"
    if ($runNew) {
        Start-Chat .\New\ Mia
    }
    else {
        Start-Chat .\Old\ Mia
    }
}
