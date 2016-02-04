$ModConfigFiles = Get-ChildItem -Path .\ModConfigFiles
$ModSourceFiles = Get-ChildItem -Path .\ModSourceFiles
$ServerConfigFiles = Get-ChildItem -Path .\ServerConfigFiles

Foreach($File in $ModConfigFiles)
{
    Push-Location .\ModConfigFiles
    Copy-Item $File -Destination .\oxide\config -Force
    Pop-Location
}

Foreach($File in $ModSourceFiles)
{
    Push-Location .\ModSourceFiles
    Copy-Item $File -Destination .\oxide\plugins -Force
    Pop-Location
}

Foreach($File in $ServerConfigFiles)
{
    Push-Location .\ServerConfigFiles
    Copy-Item $File -Destination .\cfg -Force
    Pop-Location
}