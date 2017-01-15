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

[System.IO.Compression.Zipfile]::ExtractToDirectory($file,$OxideTemp)

$ExtractedFiles = Get-ChildItem -Path $OxideTemp -Recurse

$ExtractedFiles | Copy-Item -Destination {If($_.PSIsContainer) {Join-Path $Env:RustOxideLocalDir $_.FullName.Substring($OxideTemp.length)} else {join-Path $Env:RustOxideLocalDir $_.FullName.Substring($OxideTemp.length)}} -Force -Confirm:$false -Verbose