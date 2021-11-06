if (Test-Path release)
{
    Remove-Item release\* -Recurse -Force
}
else
{
    New-Item .\release -ItemType Directory | Out-Null
}

if ((Test-Path .\release\saltychat) -eq $false)
{
    New-Item .\release\saltychat -ItemType Directory | Out-Null
}

Copy-Item .\saltychat\NUI -Recurse -Destination .\release\saltychat
Copy-Item .\saltychat\config.json -Destination .\release\saltychat
Copy-Item .\saltychat\Newtonsoft.Json.dll -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyClient\bin\Release\SaltyClient.net.dll -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyClient\bin\Release\SaltyClient.net.pdb -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyServer\bin\Release\netstandard2.0\SaltyServer.net.dll -Destination .\release\saltychat
Copy-Item .\saltychat\SaltyServer\bin\Release\netstandard2.0\SaltyServer.net.pdb -Destination .\release\saltychat

$scFxmanifest = Get-Content .\saltychat\fxmanifest.lua
$scFxmanifest = $scFxmanifest -replace 'Salty(Client|Server)\/bin\/Debug\/.*Salty(Client|Server).net.(dll|pdb)', 'Salty$2.net.$3'
$scFxmanifest | Set-Content .\release\saltychat\fxmanifest.lua

Compress-Archive .\release\saltychat\* -DestinationPath .\release\saltychat-redm.zip -CompressionLevel Optimal
