Describe 'Manual qualities import launcher' {
    BeforeEach {
        $fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $fixture | Out-Null
        $launcher = Join-Path $fixture 'Invoke-KoxoSyncFromWebhook.ps1'
        Copy-Item (Join-Path (Split-Path -Parent $PSScriptRoot) 'Invoke-KoxoSyncFromWebhook.ps1') $launcher
        # Isolate the executable launcher from network, files and KoXo itself.
        # The real module's CSV publication/no-launch path is covered separately.
        $stub = @'
function Resolve-KoxoSyncLaunchPlan {
    param($InstanceConfigPath,$CsvTargetPath,$DemoCsvTargetPath,$WorkingDirectory,$KoxoExecutablePath,$KoxoWorkingDirectory,$KoxoSyncArgument,$DemoKoxoSyncArgument)
    [pscustomobject]@{ProcessEnvironment=@{};Profiles=@();WorkingDirectory='unused';Overrides=@{};KoxoExecutablePath='must-not-run.exe';KoxoWorkingDirectory='unused'}
}
function Invoke-KoxoSyncProfiles {
    param($Profiles,$WorkingDirectory,$Overrides,[switch]$LaunchKoxo,$KoxoExecutablePath,$KoxoWorkingDirectory)
    [pscustomobject]@{LaunchRequested=$LaunchKoxo.IsPresent;ExportRequested=$true}
}
Export-ModuleMember -Function Resolve-KoxoSyncLaunchPlan,Invoke-KoxoSyncProfiles
'@
        [IO.File]::WriteAllText((Join-Path $fixture 'KoxoSync.Common.psm1'),$stub)
    }
    It 'publishes CSV without invoking KoXo for isolated manual import' {
        $result = & $launcher -InstanceConfigPath 'isolated-fixture.json' -PublishCsvOnly
        $result.ExportRequested | Should Be $true
        $result.LaunchRequested | Should Be $false
    }
    It 'preserves the normal identity synchronization launch' {
        $result = & $launcher -InstanceConfigPath 'isolated-fixture.json'
        $result.LaunchRequested | Should Be $true
    }
    It 'rejects the manual import switch without an isolated instance' {
        { & $launcher -PublishCsvOnly } | Should Throw 'requires an isolated instance'
    }
}
