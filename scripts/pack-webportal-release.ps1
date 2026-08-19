param(
  [string]$GitRef = "origin/main",
  [string]$ExpectedSourceText,
  [string]$ForbiddenSourceText,
  [string]$WorktreePath = "",
  [string]$ArtifactRoot = "C:\Users\zhounsah\Documents\Dev\_artifacts",
  [string]$ReleaseName = "webportal-release"
)

$ErrorActionPreference = "Stop"
function Invoke-Git {
  param(
    [string[]]$GitArguments
  )

$stdoutPath = [System.IO.Path]::GetTempFileName()
$stderrPath = [System.IO.Path]::GetTempFileName()

  try {
    $process = Start-Process `
      -FilePath "git.exe" `
      -ArgumentList $GitArguments `
      -NoNewWindow `
      -Wait `
      -PassThru `
      -RedirectStandardOutput $stdoutPath `
      -RedirectStandardError $stderrPath

    $stdout = Get-Content $stdoutPath -Raw -ErrorAction SilentlyContinue
    $stderr = Get-Content $stderrPath -Raw -ErrorAction SilentlyContinue

    if ($process.ExitCode -ne 0) {
      throw ("git {0}`n{1}{2}" -f $GitArguments, $stdout, $stderr)
    }

    return (@($stdout, $stderr) -join "").Trim()
  } finally {
    Remove-Item $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item $stderrPath -Force -ErrorAction SilentlyContinue
  }
}

function Assert-ContainsText {
  param(
    [string]$Content,
    [string]$Expected,
    [string]$Label
  )

  if ([string]::IsNullOrWhiteSpace($Expected)) {
    return
  }

  if (-not $Content.Contains($Expected)) {
    throw "$Label ne contient pas le texte attendu: $Expected"
  }
}

function Assert-DoesNotContainText {
  param(
    [string]$Content,
    [string]$Forbidden,
    [string]$Label
  )

  if ([string]::IsNullOrWhiteSpace($Forbidden)) {
    return
  }

  if ($Content.Contains($Forbidden)) {
    throw "$Label contient un texte interdit: $Forbidden"
  }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$repoRootPath = $repoRoot.Path

if ([string]::IsNullOrWhiteSpace($WorktreePath)) {
  $WorktreePath = Join-Path $env:TEMP ("webportal-release-" + [Guid]::NewGuid().ToString("N"))
}

$remoteName = "origin"
$gitRefParts = $GitRef.Split("/", 2)
if ($gitRefParts.Length -eq 2 -and -not [string]::IsNullOrWhiteSpace($gitRefParts[0])) {
  $remoteName = $gitRefParts[0]
}

Invoke-Git -GitArguments @("-C", $repoRootPath, "fetch", $remoteName, "--tags")

$resolvedCommit = Invoke-Git -GitArguments @("-C", $repoRootPath, "rev-list", "-n", "1", $GitRef)

if (-not $resolvedCommit) {
  throw "Impossible de resoudre la ref Git: $GitRef"
}

if (Test-Path $WorktreePath) {
  Remove-Item $WorktreePath -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $WorktreePath | Out-Null

$snapshotPath = Join-Path $env:TEMP ("webportal-release-" + [Guid]::NewGuid().ToString("N") + ".tar")
try {
  Invoke-Git -GitArguments @("-C", $repoRootPath, "archive", "--format=tar", "--output", $snapshotPath, $resolvedCommit) | Out-Null
  tar -xf $snapshotPath -C $WorktreePath
  if ($LASTEXITCODE -ne 0) {
    throw "Extraction git archive impossible pour $resolvedCommit"
  }
} finally {
  Remove-Item $snapshotPath -Force -ErrorAction SilentlyContinue
}

$pagePath = Join-Path $WorktreePath "apps\webportal\app\page.tsx"
if (-not (Test-Path $pagePath)) {
  throw "Fichier introuvable: $pagePath"
}

$pageContent = Get-Content $pagePath -Raw -Encoding UTF8
Assert-ContainsText -Content $pageContent -Expected $ExpectedSourceText -Label $pagePath
Assert-DoesNotContainText -Content $pageContent -Forbidden $ForbiddenSourceText -Label $pagePath

Push-Location $WorktreePath
try {
  npm ci
  if ($LASTEXITCODE -ne 0) {
    throw "npm ci a echoue."
  }

  npm run build --workspace @kermaria/webportal
  if ($LASTEXITCODE -ne 0) {
    throw "Le build webportal a echoue."
  }
} finally {
  Pop-Location
}

$releaseDir = Join-Path $ArtifactRoot $ReleaseName
$archivePath = Join-Path $ArtifactRoot ($ReleaseName + ".tar.gz")
$manifestPath = Join-Path $ArtifactRoot ($ReleaseName + ".manifest.json")

try {
  Remove-Item $releaseDir -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item $archivePath -Force -ErrorAction SilentlyContinue
  Remove-Item $manifestPath -Force -ErrorAction SilentlyContinue

  New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
  Copy-Item -Recurse -Force (Join-Path $WorktreePath "apps\webportal\.next\standalone\*") $releaseDir
  New-Item -ItemType Directory -Force -Path (Join-Path $releaseDir "apps\webportal\.next") | Out-Null
  # Next standalone writes its runtime cache here. Keep it empty in the artifact.
  New-Item -ItemType Directory -Force -Path (Join-Path $releaseDir "apps\webportal\.next\cache") | Out-Null
  Copy-Item -Recurse -Force (Join-Path $WorktreePath "apps\webportal\.next\static") (Join-Path $releaseDir "apps\webportal\.next\static")
  Copy-Item -Recurse -Force (Join-Path $WorktreePath "apps\webportal\public") (Join-Path $releaseDir "apps\webportal\public")

  tar -czf $archivePath -C $releaseDir .
  if ($LASTEXITCODE -ne 0) {
    throw "La creation de l'archive a echoue."
  }

  $archiveEntries = @(tar -tzf $archivePath)
  if ($LASTEXITCODE -ne 0) {
    throw "La verification de l'archive a echoue."
  }
  $cacheEntry = $archiveEntries | Where-Object {
    ($_.Replace('\', '/') -match '(^|/)apps/webportal/\.next/cache/?$')
  } | Select-Object -First 1
  if (-not $cacheEntry) {
    throw "Le package webportal ne contient pas apps/webportal/.next/cache/."
  }

  $manifest = [ordered]@{
    git_ref = $GitRef
    git_commit = $resolvedCommit
    expected_source_text = $ExpectedSourceText
    forbidden_source_text = $ForbiddenSourceText
    built_at_utc = [DateTime]::UtcNow.ToString("o")
    archive_path = $archivePath
    worktree_path = $WorktreePath
  }

  $manifest | ConvertTo-Json | Set-Content $manifestPath -Encoding UTF8

  Write-Output "Archive: $archivePath"
  Write-Output "Manifest: $manifestPath"
  Write-Output "Commit: $resolvedCommit"
} finally {
  Remove-Item $WorktreePath -Recurse -Force -ErrorAction SilentlyContinue
}
