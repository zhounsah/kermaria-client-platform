Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'KoxoQualities.Common.psm1') -Force
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'KoxoSync.Common.psm1') -Force -DisableNameChecking

Describe 'KoXo DEV qualities proof' {
    It 'ships the proof reader with the receiver' {
        $deploy=Join-Path (Split-Path -Parent $PSScriptRoot) 'Deploy-KoxoScripts.ps1'
        $names=@(& $deploy -ListOnly | ForEach-Object {$_.Name})
        $names -contains 'KoxoQualities.Common.psm1' | Should Be $true
    }
    function New-QualityFixture([string]$Groups='GG_VPN_E2E_DEV') {
        $root=Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $folder=Join-Path $root 'Users\CLIENTS DEV\DEV-CLI-PROBE'
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $csv=Join-Path $root 'clients.csv'
        $user=[pscustomobject]@{civilite='M.';nom='Test';prenom='Probe';dateNaissance='1990-01-01';identifiantUnique='CLI-D999903';groupeSecondaire='DEV-CLI-PROBE';email='probe@example.invalid';motDePasse='SECRET-NOT-RETURNED';qualitesSupplementaires=$Groups}
        Write-KoxoTextFile -Path $csv -Content (ConvertTo-KoxoCsvContent @($user)) -EncodingName utf8bom
        $file=Join-Path $folder 'probe.xml'
        [IO.File]::WriteAllText($file, '<User><UniqueID>CLI-D999903</UniqueID><UserId>probe</UserId><Password>NEVER-RETURN</Password><AdditionalQualities><AdditionalQuality><SAMAccountName>GG_VPN_E2E_DEV</SAMAccountName></AdditionalQuality></AdditionalQualities></User>')
        @{DataRoot=$root;CsvPath=$csv;SecondaryGroup='DEV-CLI-PROBE';UniqueId='CLI-D999903';ExpectedGroups=@('GG_VPN_E2E_DEV')}
    }

    It 'requires matching CSV and stored qualities without returning secrets' {
        $p=New-QualityFixture
        $r=Get-KoxoDevQualityProof @p
        $r.CsvVerified | Should Be $true
        $r.KoxoVerified | Should Be $true
        ($r | ConvertTo-Json) | Should Not Match 'SECRET|NEVER-RETURN'
    }
    It 'does not acknowledge a removal before XML has converged' {
        $p=New-QualityFixture -Groups ''
        $p.ExpectedGroups=@()
        $r=Get-KoxoDevQualityProof @p
        $r.CsvVerified | Should Be $true
        $r.KoxoVerified | Should Be $false
    }
    It 'rejects an identity claimed by multiple XML files' {
        $p=New-QualityFixture
        $folder=Join-Path $p.DataRoot 'Users\CLIENTS DEV\DEV-CLI-PROBE'
        Copy-Item (Join-Path $folder 'probe.xml') (Join-Path $folder 'duplicate.xml')
        (Get-KoxoDevQualityProof @p).KoxoVerified | Should Be $false
    }
    It 'does not accept a display name when the stored SAM name is corrupted' {
        $p=New-QualityFixture
        $file=Join-Path $p.DataRoot 'Users\CLIENTS DEV\DEV-CLI-PROBE\probe.xml'
        $xml=[IO.File]::ReadAllText($file).Replace('<SAMAccountName>GG_VPN_E2E_DEV</SAMAccountName>', '<Group>GG_VPN_E2E_DEV</Group><SAMAccountName> membership managed by Billing V2</SAMAccountName>')
        [IO.File]::WriteAllText($file,$xml)
        $r=Get-KoxoDevQualityProof @p
        $r.CsvVerified | Should Be $true
        $r.KoxoVerified | Should Be $false
    }
    It 'rejects production identities and path traversal' {
        $p=New-QualityFixture
        $p.UniqueId='CLI-000001'
        {Get-KoxoDevQualityProof @p} | Should Throw 'OUTSIDE_DEV'
        $p.UniqueId='CLI-D999903';$p.SecondaryGroup='..'
        {Get-KoxoDevQualityProof @p} | Should Throw 'OUTSIDE_DEV'
    }
}
