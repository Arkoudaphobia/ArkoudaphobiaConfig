Param(
	[Switch]$QuarterlyClean,
	[Switch]$MonthlyClean
)

$BaseServerPath = "$($Env:RustOxideLocalDir)\server\ArkoudaphobiaModded"

#Files to be preserved between wipe cycles & quarterly cleanings
$ProtectedFiles = @("BetterChat.json","Oxide.Covalence.data","Oxide.Groups.Data","Oxide.lang.data","oxide.users.data")

If($QuarterlyClean)
{
	$DataDirBasePath = "$BaseServerPath\oxide\data"
	$BaseDataFiles = Get-ChildItem -Path $DataDirBasePath
	$BaseDataFiles | ?{$_.Mode -match 'a' -and $ProtectedFiles -notcontains $_.Name} | Remove-Item -Force -Confirm:$false -ErrorAction SilentlyContinue
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

	Remove-Item -Path "$BaseServerPath\oxide\config\Portals.json" -ErrorAction SilentlyContinue
	Write-Verbose -Message "Removed Last wipes portal config file"

	Get-ChildItem -Path "$BaseServerPath\cfg" | Remove-Item -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue
	Write-Verbose -Message "Removed config items in the cfg directory"

	Get-ChildItem -Path "$BaseServerPath\storage" | Remove-Item -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue

	Get-ChildItem -Path $BaseServerPath | ?{$_.Name -match '.map'} | Remove-Item -Force -Confirm:$false -ErrorAction SilentlyContinue
}

If($MonthlyClean)
{
	Remove-Item -Path "$BaseServerPath\oxide\config\Portals.json" -ErrorAction SilentlyContinue
	Remove-Item -Path "$BaseServerPath\oxide\data\ZLevelsCraftDetails.json" -ErrorAction SilentlyContinue
	Remove-Item -Path "$BaseServerPath\oxide\data\ZLevelsRemastered.json" -ErrorAction SilentlyContinue
	Write-Verbose -Message "Removed Last wipes portal config file"
}

$ServerConfigFiles = Get-ChildItem -Path .\ServerConfigFiles
[XML]$RustManafest = Get-Content .\VS\Manifest.xml

Foreach($Config in $RustManafest.ArkoudaphobiaConfig.ModConfigFiles.Config)
{
	Switch ($Config.remove)
	{
		$true
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
			break
		}
		$false
		{
			Copy-Item ".\ModConfigFiles\$($Config.Name)" -Destination $BaseServerPath\oxide\config -Force -Confirm:$false
			Write-Host "$($Config.Name) has been loaded / updated."
			break
		}
	}
}

Foreach($Mod in $RustManafest.ArkoudaphobiaConfig.ModFiles.Mod)
{
	try
	{
		If(((Get-Date $Mod.RequestReload.DateTime) -gt (Get-Date).AddHours(-2)) -and ((Get-Date $Mod.RequestReload.DateTime) -lt (Get-Date)))
		{
			Remove-Item -Path $BaseServerPath\Oxide\plugins\$($Mod.Name)
			Write-Host "$($Mod.Name) has been unloaded."
			Start-Sleep -Seconds 5
		}
	}
	catch [System.Management.Automation.ParameterBindingException]
	{
		Write-Host "$($Mod.Name) not set to be reloaded"
	}

	Switch ($Mod.enabled)
	{
		$true
		{
			Copy-Item ".\ModSourceFiles\$($Mod.Name)" -Destination $BaseServerPath\Oxide\plugins -Force -Confirm:$false
			Write-Host "$($Mod.Name) uas been loaded / updated."
			break
		}
		$false
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
			break
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

Copy-Item -Path .\VS\StartUpScript.ps1 -Destination "C:\RustTools" -Force -Confirm:$false