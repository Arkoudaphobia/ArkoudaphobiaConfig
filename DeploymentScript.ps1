$ModConfigFiles = Get-ChildItem -Path .\ModConfigFiles
$ModSourceFiles = Get-ChildItem -Path .\ModSourceFiles
$ServerConfigFiles = Get-ChildItem -Path .\ServerConfigFiles
$BaseServerPath = "C:\RustServerOxide\server\ArkoudaphobiaModded"

Foreach($File in $ModConfigFiles)
{
    Push-Location .\ModConfigFiles
    Copy-Item $File -Destination $BaseServerPath\oxide\config -Force
    Pop-Location
}

Foreach($File in $ModSourceFiles)
{
    Push-Location .\ModSourceFiles
	Get-ChildItem | Remove-Item -Confirm:$False -Force
    Copy-Item $File -Destination $BaseServerPath\oxide\plugins
    Pop-Location
}

Foreach($File in $ServerConfigFiles)
{
    Push-Location .\ServerConfigFiles
    Copy-Item $File -Destination $BaseServerPath\cfg -Force
    Pop-Location
}