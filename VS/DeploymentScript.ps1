﻿Param(
	[Parameter(ParameterSetName="Quarterly")]
	[Switch]$QuarterlyClean,
	[Parameter(ParameterSetName="Monthly")]
	[Switch]$MonthlyClean
)

$BaseServerPath = "C:\RustServerOxide\server\ArkoudaphobiaModded"

If($QuarterlyClean)
{
	$DataDirBasePath = "$BaseServerPath\oxide\data"
	$BaseDataFiles = Get-ChildItem -Path $DataDirBasePath
	$BaseDataFiles | ?{$_.Mode -match 'a'} | Remove-Item -Force -Confirm:$false
	Write-Verbose -Message "Removed files in the data directory"
	foreach($Directory in ($BaseDataFiles | ?{$_.Mode -match 'd'}))
	{
		If($Directory.Name -ne 'PlayerDatabase')
		{
			try
			{
				$Directory | Get-ChildItem -Recurse | Remove-Item -Recurse -Force:$true -Confirm:$false
				Write-Verbose -Message "Removed files in $($Directory.Name)"
			}
			catch [System.Exception]
			{
				Write-Error -Message "An error occured removing files from the $(Directory.Name) directory"
			}
		}
	}

	Remove-Item -Path "$BaseServerPath\oxide\config\Portals.json"
	Write-Verbose -Message "Removed Last wipes portal config file"

	Get-ChildItem -Path "$BaseServerPath\cfg" | Remove-Item -Recurse -Force -Confirm:$false
	Write-Verbose -Message "Removed config items in the cfg directory"

	Get-ChildItem -Path "$BaseServerPath\save" | Remove-Item -Recurse -Force -Confirm:$false
	Write-Verbose -Message "Removed saves from the save directory"

	Remove-Item -Path "$BaseServerPath\Storage.db" -Confirm:$false -Force
	Write-Verbose -Message "Removed the storage database"

	Remove-Item -Path "$BaseServerPath\UserPersistence.db" -Confirm:$false -Force:$true
	Write-Verbose -Message "Removed the User Persistence Database"
}

If($MonthlyClean)
{
	Remove-Item -Path "$BaseServerPath\oxide\config\Portals.json"
	Write-Verbose -Message "Removed Last wipes portal config file"
}

$ServerConfigFiles = Get-ChildItem -Path .\ServerConfigFiles
[XML]$RustManafest = Get-Content .\VS\Manifest.xml

Foreach($Config in $RustManafest.ArkoudaphobiaConfig.ModConfigFiles.Config)
{
	If($Config.remove -eq $true)
	{
		If((Test-Path $BaseServerPath\oxide\config\$($Config.Name)) -eq $true)
		{
			Remove-Item -Path $BaseServerPath\Oxide\config\$($Config.Name) -Confirm:$false
			Write-Host "$($Config.Name) has been removed from the server."
		}
		else
		{
			Write-Host "$($Config.Name) does not exsist on the server.  It can be safely removed from the manafest file now."
		}
	}
	If($Config.remove -eq $false)
	{
		Copy-Item ".\ModConfigFiles\$($Config.Name)" -Destination $BaseServerPath\oxide\config -Force -Confirm:$false
		Write-Host "$($Config.Name) has been loaded / updated."
	}
}

Foreach($Mod in $RustManafest.ArkoudaphobiaConfig.ModFiles.Mod)
{
	If(($Mod.RequestReload.DateTime -gt (Get-Date).AddHours(-2)) -and ($Mod.RequestReload.DateTime -lt (Get-Date)))
	{
		Remove-Item -Path $BaseServerPath\Oxide\plugins\$($Mod.Name)
		Write-Host "$($Mod.Name) has been unloaded."
		Start-Sleep -Seconds 5
	}

	If($Mod.enabled -eq $true)
	{
		Copy-Item ".\ModSourceFiles\$($Mod.Name)" -Destination $BaseServerPath\Oxide\plugins -Force -Confirm:$false
		Write-Host "$($Mod.Name) uas been loaded / updated."
	}

	If($Mod.enabled -eq $false)
	{
		If((Test-Path -Path $BaseServerPath\Oxide\plugins\$($Mod.Name)) -eq $true)
		{
			Remove-Item -Path $BaseServerPath\Oxide\plugins\$($Mod.Name) -Force -Confirm:$false
			Write-Host "$($Mod.Name) has been removed from the server."
		}
		Else
		{
			Write-Host "$($Mod.Name) does not exsist on the server.  It can be safely removed from the manafest file now."
		}
	}
}

Foreach($File in $ServerConfigFiles)
{
	Push-Location .\ServerConfigFiles
	Copy-Item $File -Destination $BaseServerPath\cfg -Force -Confirm:$false
	Write-Host "$($File.Name) has been writen to the server."
	Pop-Location
}