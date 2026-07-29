[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ServiceName = "KermariaApiInternal",
    [string]$DisplayName = "Kermaria API Internal",
    [string]$BinaryPath = "C:\apps\api-internal\Kermaria.ApiInternal.exe",
    [string]$AppRoot = "C:\apps\api-internal",
    [string]$LogsPath = "C:\apps\api-internal\logs",
    [string]$ConfigPath = "C:\ProgramData\Kermaria\api-internal.config.json",
    [string]$ListenUrl = "http://192.168.100.213:5000",
    [string]$EnvironmentName = "Production",
    [string]$ServiceAccount = "LocalSystem",
    [securestring]$ServicePassword,
    [switch]$GrantLogOnAsService,
    [switch]$StartService
)

$ErrorActionPreference = "Stop"

function Convert-ToPlainText {
    param([securestring]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    } finally {
        if ($ptr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
        }
    }
}

function Grant-LogOnAsServiceRight {
    param([string]$Account)

    if (-not ("Kermaria.LsaRights" -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Kermaria
{
    public static class LsaRights
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LsaObjectAttributes
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public int Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LsaUnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Buffer;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern uint LsaOpenPolicy(
            IntPtr systemName,
            ref LsaObjectAttributes objectAttributes,
            int desiredAccess,
            out IntPtr policyHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern uint LsaAddAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            LsaUnicodeString[] userRights,
            uint countOfRights);

        [DllImport("advapi32.dll")]
        private static extern uint LsaNtStatusToWinError(uint status);

        [DllImport("advapi32.dll")]
        private static extern uint LsaClose(IntPtr policyHandle);

        public static void Add(string accountName, string rightName)
        {
            const int PolicyCreateAccount = 0x10;
            const int PolicyLookupNames = 0x800;
            var attributes = new LsaObjectAttributes();
            attributes.Length = Marshal.SizeOf(attributes);
            IntPtr policy;
            uint status = LsaOpenPolicy(
                IntPtr.Zero,
                ref attributes,
                PolicyCreateAccount | PolicyLookupNames,
                out policy);
            ThrowIfError(status);

            try
            {
                var sid = (SecurityIdentifier)new NTAccount(accountName).Translate(
                    typeof(SecurityIdentifier));
                var sidBytes = new byte[sid.BinaryLength];
                sid.GetBinaryForm(sidBytes, 0);
                var pinnedSid = GCHandle.Alloc(sidBytes, GCHandleType.Pinned);
                try
                {
                    var right = new LsaUnicodeString
                    {
                        Buffer = rightName,
                        Length = checked((ushort)(rightName.Length * 2)),
                        MaximumLength = checked((ushort)((rightName.Length + 1) * 2))
                    };
                    status = LsaAddAccountRights(
                        policy,
                        pinnedSid.AddrOfPinnedObject(),
                        new[] { right },
                        1);
                    ThrowIfError(status);
                }
                finally
                {
                    pinnedSid.Free();
                }
            }
            finally
            {
                LsaClose(policy);
            }
        }

        private static void ThrowIfError(uint status)
        {
            if (status != 0)
            {
                throw new Win32Exception((int)LsaNtStatusToWinError(status));
            }
        }
    }
}
"@
    }

    [Kermaria.LsaRights]::Add($Account, "SeServiceLogonRight")
}

if (-not (Test-Path -LiteralPath $BinaryPath)) {
    throw "Binaire introuvable : $BinaryPath"
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config JSON introuvable : $ConfigPath"
}

$serviceCommand = "`"$BinaryPath`" --environment $EnvironmentName --urls $ListenUrl"
$serviceExists = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$plainPassword = Convert-ToPlainText $ServicePassword

if ($GrantLogOnAsService -and $ServiceAccount -ne "LocalSystem") {
    Grant-LogOnAsServiceRight -Account $ServiceAccount
}

if ($PSCmdlet.ShouldProcess($AppRoot, "Creer les dossiers applicatifs")) {
    foreach ($path in @($AppRoot, $LogsPath, (Split-Path -Parent $ConfigPath))) {
        if (-not (Test-Path -LiteralPath $path)) {
            New-Item -ItemType Directory -Force -Path $path | Out-Null
        }
    }
}

if ($PSCmdlet.ShouldProcess($AppRoot, "Appliquer les ACL minimales")) {
    icacls $AppRoot /inheritance:r /grant:r "*S-1-5-32-544:(OI)(CI)F" /grant:r "*S-1-5-18:(OI)(CI)F" | Out-Null
    icacls $LogsPath /inheritance:r /grant:r "*S-1-5-32-544:(OI)(CI)F" /grant:r "*S-1-5-18:(OI)(CI)F" | Out-Null
    if ($ServiceAccount -ne "LocalSystem") {
        icacls $AppRoot /grant:r "${ServiceAccount}:(OI)(CI)RX" | Out-Null
        icacls $LogsPath /grant:r "${ServiceAccount}:(OI)(CI)M" | Out-Null
        icacls $ConfigPath /inheritance:r /grant:r "*S-1-5-32-544:F" /grant:r "*S-1-5-18:F" /grant:r "${ServiceAccount}:R" | Out-Null
    } else {
        icacls $ConfigPath /inheritance:r /grant:r "*S-1-5-32-544:F" /grant:r "*S-1-5-18:F" | Out-Null
    }
}

if ($serviceExists) {
    if ($PSCmdlet.ShouldProcess($ServiceName, "Mettre a jour le service Windows")) {
        sc.exe config $ServiceName binPath= $serviceCommand start= auto | Out-Null
        if ($ServiceAccount -ne "LocalSystem") {
            sc.exe config $ServiceName obj= $ServiceAccount password= $plainPassword | Out-Null
        }
        sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
    }
} else {
    if ($PSCmdlet.ShouldProcess($ServiceName, "Creer le service Windows")) {
        if ($ServiceAccount -eq "LocalSystem") {
            sc.exe create $ServiceName binPath= $serviceCommand start= auto DisplayName= $DisplayName | Out-Null
        } else {
            sc.exe create $ServiceName binPath= $serviceCommand start= auto DisplayName= $DisplayName obj= $ServiceAccount password= $plainPassword | Out-Null
        }
        sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
    }
}

if ($StartService -and $PSCmdlet.ShouldProcess($ServiceName, "Demarrer le service")) {
    if ((Get-Service -Name $ServiceName).Status -ne "Running") {
        Start-Service -Name $ServiceName
    } else {
        Restart-Service -Name $ServiceName
    }
}

Write-Host "Service cible : $ServiceName"
Write-Host "Commande      : $serviceCommand"
Write-Host "Config        : $ConfigPath"
Write-Host "Logs          : $LogsPath"
if ($StartService) {
    Write-Host "Verifier ensuite : curl.exe $ListenUrl/health/ready"
}
