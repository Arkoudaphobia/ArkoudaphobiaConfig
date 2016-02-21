$ModConfigFiles = Get-ChildItem -Path .\ModConfigFiles
$ModSourceFiles = Get-ChildItem -Path .\ModSourceFiles
$ServerConfigFiles = Get-ChildItem -Path .\ServerConfigFiles
[XML]$RustManafest = Get-Content .\VS\Manafest.xml
$BaseServerPath = "C:\RustServerOxide\server\ArkoudaphobiaModded"

Foreach($Mod in $RustManafest.ArkoudaphobiaConfig.ModFiles.Mod)
{
	If($Mod.enabled -eq $true)
	{
		Copy-Item ($ModConfigFiles + "\" + $Mod.Name) -Destination $BaseServerPath\Oxide\plugins
	}
}

Foreach($File in $ServerConfigFiles)
{
	Push-Location .\ServerConfigFiles
	Copy-Item $File -Destination $BaseServerPath\cfg -Force
	Pop-Location
}