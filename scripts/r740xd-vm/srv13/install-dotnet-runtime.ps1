[CmdletBinding()]
param(
    [string]$Version = "10.0.10"
)

$ErrorActionPreference = "Stop"
$installRoot = "C:\Windows\Temp\KermariaDotNetInstall"
$packages = @(
    [pscustomobject]@{
        Name = "Desktop"
        FileName = "windowsdesktop-runtime-$Version-win-x64.exe"
        Url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$Version/windowsdesktop-runtime-$Version-win-x64.exe"
    },
    [pscustomobject]@{
        Name = "AspNetCore"
        FileName = "aspnetcore-runtime-$Version-win-x64.exe"
        Url = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/$Version/aspnetcore-runtime-$Version-win-x64.exe"
    }
)

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null

try {
    $results = foreach ($package in $packages) {
        $installerPath = Join-Path $installRoot $package.FileName
        Invoke-WebRequest -Uri $package.Url -OutFile $installerPath -UseBasicParsing

        $signature = Get-AuthenticodeSignature -FilePath $installerPath
        if ($signature.Status -ne "Valid" -or
            $signature.SignerCertificate.Subject -notmatch "Microsoft") {
            throw "Signature Authenticode invalide pour $($package.Name)."
        }

        $sha512 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA512).Hash
        $process = Start-Process -FilePath $installerPath `
            -ArgumentList "/install", "/quiet", "/norestart" `
            -Wait `
            -PassThru
        if ($process.ExitCode -notin 0, 3010) {
            throw "Installation $($package.Name) echouee : $($process.ExitCode)."
        }

        [pscustomobject]@{
            Package = $package.Name
            ExitCode = $process.ExitCode
            Signature = $signature.Status.ToString()
            SHA512 = $sha512
        }
    }

    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
    if (-not (Test-Path -LiteralPath $dotnet)) {
        throw "dotnet.exe reste introuvable apres installation."
    }

    $runtimes = @(& $dotnet --list-runtimes | Where-Object { $_ -match [regex]::Escape($Version) })
    foreach ($required in "Microsoft.NETCore.App", "Microsoft.AspNetCore.App", "Microsoft.WindowsDesktop.App") {
        if (-not ($runtimes | Where-Object { $_ -like "$required $Version*" })) {
            throw "Runtime requis absent apres installation : $required $Version."
        }
    }

    [pscustomobject]@{
        Packages = @($results)
        Runtimes = $runtimes
        RebootRequired = [bool](@($results).ExitCode -contains 3010)
    }
} finally {
    if (Test-Path -LiteralPath $installRoot) {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }
}
