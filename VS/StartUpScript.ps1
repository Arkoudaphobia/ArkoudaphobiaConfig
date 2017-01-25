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

$ExtractedFiles = Get-ChildItem -Path $OxideTemp -Recurse

Write-Host $ExtractedFiles

$ExtractedFiles | Copy-Item -Destination {If($_.PSIsContainer) {Join-Path $Env:RustOxideLocalDir $_.FullName.Substring($OxideTemp.length + 5)} else {join-Path $Env:RustOxideLocalDir $_.FullName.Substring($OxideTemp.length + 5)}} -Force -Confirm:$false -Recurse -Verbose

Stop-Transcript