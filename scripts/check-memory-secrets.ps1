# Verifie que la memoire partagee `.ai/` ne contient aucun secret.
#
# `.ai/` est versionne dans un depot public : tout ce qui y entre est publie.
# La politique (`.ai/SHARED_MEMORY_POLICY.md`, regle 7) interdit d'y ecrire un
# mot de passe, un token, une cle API, une cle privee, un cookie de session ou
# une chaine de connexion portant un secret.
#
# Ce script est le garde-fou mecanique de cette regle, appele en fin de tache
# par AGENTS.md. Il ne remplace pas `scripts/check-secrets.mjs`, qui couvre le
# reste du depot.
#
# Fichier volontairement en ASCII pur : Windows PowerShell 5.1 relit un script
# UTF-8 sans BOM en ANSI, et un simple tiret cadratin suffit a casser l'analyse.
#
# Sortie : 0 si rien n'est trouve, 1 sinon.

[CmdletBinding()]
param(
    [string] $Path
)

$ErrorActionPreference = 'Stop'

# `$PSScriptRoot` n'est pas encore renseigne quand PowerShell 5.1 evalue la
# valeur par defaut d'un parametre : le calculer ici, pas dans le bloc param.
if (-not $Path) {
    $Path = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\.ai'
}

$root = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
if (-not $root) {
    Write-Host "Aucune memoire partagee a verifier ($Path absent)."
    exit 0
}

# Chaque motif decrit une valeur reellement secrete, pas la simple mention du
# nom d'une variable : la memoire a le droit de dire que STRIPE_SECRET_KEY
# commence par sk_live_, elle n'a pas le droit de porter la cle.
$patterns = @(
    @{ Label = 'cle secrete Stripe';       Pattern = 'sk_(live|test)_[A-Za-z0-9]{16,}' },
    @{ Label = 'cle publiable Stripe';     Pattern = 'pk_(live|test)_[A-Za-z0-9]{16,}' },
    @{ Label = 'secret de webhook Stripe'; Pattern = 'whsec_[A-Za-z0-9]{16,}' },
    @{ Label = "cle d'acces AWS";          Pattern = 'AKIA[0-9A-Z]{16}' },
    @{ Label = 'cle privee';               Pattern = '-----BEGIN (RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----' },
    @{ Label = 'jeton JWT';                Pattern = 'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.' },
    @{ Label = 'mot de passe de test en clair'; Pattern = 'Test12345!' },
    @{ Label = 'jeton local faible';       Pattern = 'dev-local-token' },
    # Le mot `Password` doit demarrer le jeton et porter une vraie valeur :
    # sans cela, les drapeaux de configuration KoXo (`PurifyImportedPassword=0`,
    # `CannotChangePassword=True`) declenchent l'alerte a chaque lecture.
    @{ Label = 'chaine de connexion avec mot de passe'; Pattern = '(^|[;"''\s])(Password|Pwd)\s*=\s*[^;\s\[<"'']{6,}' },
    @{ Label = 'URL avec identifiants';    Pattern = '[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]+@' },
    @{ Label = 'affectation de secret';    Pattern = '(SQL_PASSWORD|SERVICE_AUTH_TOKEN|BPCE_REFRESH_TOKEN|PAYPAL_CLIENT_SECRET|STRIPE_SECRET_KEY|HCAPTCHA_SECRET|SMTP_PASSWORD|AD_SERVICE_ACCOUNT_PASSWORD|DEMO_[A-Z_]*PASSWORD)\s*[:=]\s*[^\s\[<"''|]+' }
)

# Une valeur explicitement expurgee ou un simple gabarit ne sont pas des fuites.
$placeholder = 'REDACTED|REPLACE_WITH|INJECTER_LOCALEMENT|EXPURGE|<[^>]+>|\$\{[^}]+\}|\.\.\.|xxx+|\*\*\*'

$findings = @()
$files = Get-ChildItem -LiteralPath $root -Recurse -File -Include *.md, *.txt, *.json, *.yml, *.yaml
foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if ([string]::IsNullOrEmpty($content)) { continue }

    foreach ($rule in $patterns) {
        $matched = [regex]::Matches($content, $rule.Pattern, 'IgnoreCase')
        foreach ($hit in $matched) {
            if ($hit.Value -match $placeholder) { continue }

            $line = ($content.Substring(0, $hit.Index) -split "`n").Count
            $relative = $file.FullName.Substring($root.Path.Length).TrimStart('\', '/')
            $label = $rule.Label
            # On rapporte l'emplacement et la nature, jamais la valeur : un
            # journal de CI ne doit pas devenir le second endroit ou le secret
            # se trouve.
            $findings += "$relative : ligne $line - $label"
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host 'Secrets potentiels dans la memoire partagee :' -ForegroundColor Red
    $findings | Sort-Object -Unique | ForEach-Object { Write-Host "  - $_" }
    Write-Host ''
    Write-Host 'Retirer la valeur, la remplacer par [REDACTED], et la considerer comme compromise.'
    exit 1
}

Write-Host 'Memoire partagee verifiee : aucun secret detecte.'
exit 0
