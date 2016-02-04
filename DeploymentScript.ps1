$ModConfigFiles = Get-ChildItem -Path .\ModConfigFiles
$ModSourceFiles = Get-ChildItem -Path .\ModSourceFiles
$ServerConfigFiles = Get-ChildItem -Path .\ServerConfigFiles

Foreach($File in $ModConfigFiles)
{
    Copy-Item $File -Destination .\oxide\config -Force
}

Foreach($File in $ModSourceFiles)
{
    Copy-Item $File -Destination .\oxide\plugins -Force
}

Foreach($File in $ServerConfigFiles)
{
    Copy-Item $File -Destination .\cfg -Force
}