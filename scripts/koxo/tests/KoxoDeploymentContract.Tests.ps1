Describe 'KoXo deployment contract isolation' {
    $deployScript = Join-Path (Split-Path -Parent $PSScriptRoot) 'Deploy-KoxoScripts.ps1'
    It 'rejects v3 for either the production task or the normalized shared path' {
        $koxoRoot = Split-Path -Parent $PSScriptRoot
        . $deployScript -ListOnly -SourcePath $koxoRoot | Out-Null
        { Assert-KoxoDeploymentContract -Destination 'C:\isolated-dev' -ReceiverTask 'Kermaria-KoXoWebhookReceiver-8042' -Names @('KoxoSync.Common.psm1') } | Should Throw 'KOXO_PROD_CONTRACT_MISMATCH'
        { Assert-KoxoDeploymentContract -Destination 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\.\' -ReceiverTask 'DEV' -Names @('KoxoSync.Common.psm1') } | Should Throw 'KOXO_PROD_CONTRACT_MISMATCH'
    }

    It 'allows an isolated DEV target and a manifest without the incompatible module' {
        $koxoRoot = Split-Path -Parent $PSScriptRoot
        . $deployScript -ListOnly -SourcePath $koxoRoot | Out-Null
        { Assert-KoxoDeploymentContract -Destination 'C:\ProgramData\Kermaria\koxo-dev\app-qualities' -ReceiverTask 'Kermaria-KoXoWebhookReceiver-DEV-8043' -Names @('KoxoSync.Common.psm1') } | Should Not Throw
        { Assert-KoxoDeploymentContract -Destination 'C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro' -ReceiverTask 'Kermaria-KoXoWebhookReceiver-8042' -Names @('Test-KoxoAccentHandling.ps1') } | Should Not Throw
    }

    It 'refuses the default deployment before opening a remote session' {
        Mock New-PSSession { throw 'UNEXPECTED_REMOTE_SESSION' }
        { & $deployScript } | Should Throw 'KOXO_PROD_CONTRACT_MISMATCH'
        Assert-MockCalled New-PSSession -Times 0 -Exactly
    }

}
