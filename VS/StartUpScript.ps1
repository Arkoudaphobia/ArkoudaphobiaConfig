#Generate New Log File Name
$CurrentLogFilePath = ($Env:temp + "\" + "ArkoudaphobiaStartup." + [DateTime]::Now.ToFileTime().ToString() + ".log")

#Begin logging procedures
Start-Transcript -Path  $CurrentLogFilePath

#Clean up old log files
Write "Cleaning up log files older then 30 days"
Get-ChildItem -Path $ENV:Temp | ?{$_.PSIsContainer -eq $false -and $_.CreationTime -lt $((Get-Date).AddDays(-14)) -and $_.Name -match "ArkoudaphobiaStartup"} | Remove-Item -Confirm:$false -force:$true -Verbose:$true

#Load requied assemblies to perform web based file download operations & create a new object to perform the required tasks
Write "Adding required assemblies and objects to perform our tasks"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$WebClient = New-Object System.Net.WebClient

#Install latest version of the rust dedicated server
Write "Beginning Update of Rust Dedicated Server"
cmd.exe /c "$($ENV:SteamCmdDir)\steamcmd.exe +login Anonymous +force_install_dir $($Env:RustOxideLocalDir) +app_update 258550 validate +quit"

#Test if we have an exsisting Oxide-Rust.zip file in the temp directory and delete it if we do
Write "Cleaning up old oxide zip files"
if((Test-Path $Env:temp\Oxide-Rust.zip) -eq $true)
{
    Remove-Item -Path $Env:temp\Oxide-Rust.zip -Force -Confirm:$false
}

#Setup and perform required file downloads for both Oxide and RustIO (Live Map)
Write "Preparing to download latest  Oxide and RustIO files"

$apiResponse = Invoke-RestMethod -Method Get -uri https://ci.appveyor.com/api/projects/oxidemod/oxide

$LocalVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$($Env:RustOxideLocalDir)\RustDedicated_Data\Managed\Oxide.Core.dll")

$file = "$Env:Temp\Oxide-Rust.zip"

If($LocalVersionInfo.FileBuildPart -ne $apiResponse.build.buildNumber)
{
    $ShouldCopy = $true
    Write "An update has been found, Local Version is: $($LocalVersionInfo.FileBuildPart) Remote Version is: $($apiResponse.build.version) proceeding with update"    
    $url = "https://dl.bintray.com/oxidemod/builds/Oxide-Rust.zip"
    $WebClient.DownloadFile($url,$file)
}
else
{
    $ShouldCopy = $false    
}

Write "Downloading RustIO"

$rustIOUrl = "http://playrust.io/latest/oxide"

$rustIOTarget = "$ENV:Temp\Oxide.Ext.RustIO.dll"

$WebClient.DownloadFile($rustIOUrl,$rustIOTarget)

#Set-up the enviroment to extract the contents of the Oxide zip file and clean out the target directory we'll be extracting into.
Write "Cleaning Up Oxide Temp Directory"
$OxideTemp = "$Env:temp\RustOxideTempDir"

Get-ChildItem -Path $OxideTemp -Recurse | Remove-Item -recurse -Force -Confirm:$false

#Perform extraction of Oxide files to temp directory
If($ShouldCopy -eq $true)
{
    Write "Extracting Latest Oxide files to Oxide Temp Directory"
    try
    {
        [System.IO.Compression.Zipfile]::ExtractToDirectory($file,$OxideTemp)
    }
    catch
    {
        Write "An error occured during zipe file download aborting oxide update process"
        $ShouldCopy = $false
    }
}

#Copy Files from the temp directory to the dedicated server directory
Write "Deploying latest Oxide files and RustIO to Rust Dedicated Server Directory"

If($ShouldCopy -eq $true)
{
    (Get-ChildItem -Path $OxideTemp | ?{$_.PSIsContainer -eq $false}) | Copy-Item -Destination $Env:RustOxideLocalDir -Force -Confirm:$false -Verbose

    (Get-ChildItem -Path $OxideTemp\RustDedicated_Data\Managed | ?{$_.PSIsContainer -eq $False}) | Copy-Item -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed -Force -Confirm:$false -Verbose

    (Get-ChildItem -Path $OxideTemp\RustDedicated_Data\Managed\x86) | Copy-Item -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed\x86 -Force -Confirm:$false -Verbose

    (Get-ChildItem -Path $OxideTemp\RustDedicated_Data\Managed\x64) | Copy-Item -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed\x64 -Force -Confirm:$false -Verbose
}
else
{
    Write "Skipping Oxide file copy process ... no updates are avalible"    
}

Copy-Item -Path $rustIOTarget -Destination $Env:RustOxideLocalDir\RustDedicated_Data\Managed -Force -Confirm:$false -Verbose

#Done! Proceed to end the log and start the server
Write "FIN!"
Stop-Transcript
