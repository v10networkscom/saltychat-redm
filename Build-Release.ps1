# Find msbuild and use it later on
$msBuildPath = $null

if (Test-Path "C:\Program Files\Microsoft Visual Studio\2022\*\Msbuild\Current\Bin\MSBuild.exe")
{
    $msBuildPath = "C:\Program Files\Microsoft Visual Studio\2022\*\Msbuild\Current\Bin\MSBuild.exe"
}
else
{
    foreach($path in $env:Path.Split(";"))
    {
        if (Test-Path "$path\msbuild.exe")
        {
            $msBuildPath = "$path\msbuild.exe"

            break
        }
    }

    if ($msBuildPath -eq $null)
    {
        throw "Could not find msbuild"
    }
}

# Cleanup/Create release directory
if (Test-Path release)
{
    Remove-Item release\* -Recurse -Force
}
else
{
    New-Item .\release -ItemType Directory | Out-Null
}

## Salty Chat ##

# Create build directory for Salty Chat
if ((Test-Path .\release\saltychat) -eq $false)
{
    New-Item .\release\saltychat -ItemType Directory | Out-Null
}

# Build Salty Chat Solution
$buildOutput = (& $msBuildPath saltychat\SaltyChat-RedM.sln /property:Configuration=Release) -Join [System.Environment]::NewLine

if ($buildOutput -notmatch "Build succeeded.")
{
    throw $buildOutput
}

# Copy all necessary items to the release directory
Copy-Item .\saltychat\NUI -Recurse -Destination .\release\saltychat
Copy-Item .\saltychat\config.json -Destination .\release\saltychat
Copy-Item .\saltychat\Newtonsoft.Json.dll -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyClient\bin\Release\SaltyClient.net.dll -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyClient\bin\Release\SaltyClient.net.pdb -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyServer\bin\Release\netstandard2.0\SaltyServer.net.dll -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyServer\bin\Release\netstandard2.0\SaltyServer.net.pdb -Destination .\release\saltychat

# Adjust paths in fxmanifest
$scFxmanifest = Get-Content .\saltychat\fxmanifest.lua
$scFxmanifest = $scFxmanifest -replace 'Salty(Client|Server)\/bin\/Debug\/.*Salty(Client|Server).net.(dll|pdb)', 'Salty$2.net.$3'
$scFxmanifest | Set-Content .\release\saltychat\fxmanifest.lua

# Zip directory which will be used as release on GitHub
Compress-Archive .\release\saltychat\* -DestinationPath .\release\saltychat-redm.zip -CompressionLevel Optimal
