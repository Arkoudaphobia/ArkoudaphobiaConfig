Start-Transcript -Path ($Env:temp + "\" + [DateTime]::Now.ToFileTime().ToString() + ".log")

Add-Type -AssemblyName System.IO.Compression.FileSystem
$WebClient = New-Object System.Net.WebClient

cmd.exe /c "$($ENV:SteamCmdDir)\steamcmd.exe +login Anonymous +force_install_dir $($Env:RustOxideLocalDir) +app_update 258550 validate +quit"

if((Test-Path $Env:temp\Oxide-Rust.zip) -eq $true)
{
    Remove-Item -Path $Env:temp\Oxide-Rust.zip -Force -Confirm:$false
}

$url = "https://github.com/OxideMod/Snapshots/blob/master/Oxide-Rust.zip?raw=true"

$file = "$Env:Temp\Oxide-Rust.zip"

$WebClient.DownloadFile($url,$file)

$OxideTemp = "$Env:temp\RustOxideTempDir"

Get-ChildItem -Path $OxideTemp -Recurse | Remove-Item -recurse -Force -Confirm:$false

[System.IO.Compression.Zipfile]::ExtractToDirectory($file,$OxideTemp)

Copy-Item (Get-ChildItem -Path $OxideTemp | ?{$_.PSIsContainer -eq $false}) -Destination $Env:RustOxideLocalDir -Force -Confirm:$false

Copy-Item (Get-ChildItem -Path $OxideTemp\RustDedicated_Data\Managed | ?{$_.PSIsContainer -eq $False}) -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed -Force -Confirm:$false

Copy-Item (Get-ChildItem -Path $OxideTemp\RustDedicated_Data\Managed\x86) -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed\x86 -Force -Confirm:$false

Copy-Item (Get-ChildItem -Path $OxideTemp\RustDedicated_Data\Managed\x64) -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed\x64 -Force -Confirm:$false

Stop-Transcript