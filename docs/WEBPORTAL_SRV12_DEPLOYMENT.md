# Redeploiement WEBPORTAL sur SRV-12

Ce runbook couvre le redeploiement du front `apps/webportal` sur `KERMARIA-SRV-12`
sans toucher `api-internal`.

Objectif principal : ne plus basculer un artefact dont le contenu public ne
correspond pas a la ref Git attendue.

Mapping canonique valide en production le 1er aout 2026 :

- `www.zacharyhounsa.ovh` = vitrine publique canonique
- `dashboard.zacharyhounsa.ovh` = portail client canonique + endpoints webhook
- `administration.zacharyhounsa.ovh` = portail admin canonique
- `www.home.bzh` = alias public, redirige vers `www.zacharyhounsa.ovh`
- `dashboard.home.bzh` et `portail.home.bzh` = aliases client, redirigent vers `dashboard.zacharyhounsa.ovh`
- `administration.home.bzh` = alias admin, redirige vers `administration.zacharyhounsa.ovh`

## 1. Principe

Le redeploiement doit toujours valider trois points avant la bascule :

1. la ref Git resolue est explicite ;
2. la source contient bien le hero attendu ;
3. la page servie apres restart contient bien ce meme hero et n'affiche pas
   l'ancien.

Le garde-fou permanent est :

- `scripts/pack-webportal-release.ps1`
- `scripts/assert-webportal-home.mjs`

## 2. Exemple : vitrine "sinistre"

Pour empaqueter une ref qui doit contenir le discours "Un sinistre..." :

```powershell
npm run pack:webportal:release -- `
  -GitRef origin/main `
  -ReleaseName webportal-sinistre `
  -ExpectedSourceText "Un sinistre peut" `
  -ForbiddenSourceText "Informatique claire et utile."
```

Le script :

- rafraichit d'abord les refs et tags du remote ;
- resout ensuite la ref Git en commit exact ;
- cree un worktree propre ;
- refuse le build si `apps/webportal/app/page.tsx` ne contient pas le texte
  attendu ;
- construit le standalone ;
- produit une archive et un manifest dans `C:\Users\zhounsah\Documents\Dev\_artifacts\`.

## 3. Ref historique : verifier le commit, pas le souvenir

Incident constate le 31 juillet 2026 :

- un ancien worktree local a ete deploye ;
- il ne correspondait pas a la ref Git reelle attendue ;
- le tag `v0.40.0.1` resolvait en realite vers le commit
  `1e3131507875546cdb3cc2d6ecf7a9d626ee5f0e`, qui contient bien le hero
  "Un sinistre peut...".

Regle definitive :

- ne jamais deployer depuis un dossier de release local existant ;
- toujours empaqueter depuis une ref Git explicite ;
- toujours controler le `git_commit` du manifest avant upload.

## 4. Verifier le manifest avant upload

Le fichier `*.manifest.json` doit etre controle avant envoi :

- `git_ref`
- `git_commit`
- `expected_source_text`
- `forbidden_source_text`

Si un de ces champs est faux, ne pas deployer l'archive.

## 5. Upload et bascule serveur

Depuis la machine locale :

```bash
scp C:/Users/zhounsah/Documents/Dev/_artifacts/webportal-sinistre.tar.gz <user>@KERMARIA-SRV-12.home.bzh:/tmp/webportal-release.tar.gz
```

Sur `SRV-12` :

```bash
set -euo pipefail

release="$(date -u +%Y%m%d-%H%M%S)-manual-webportal"
release_dir="/opt/kermaria/releases/$release"
current_target="$(readlink -f /opt/kermaria/webportal)"

sudo mkdir -p "$release_dir"
sudo tar -xzf /tmp/webportal-release.tar.gz -C "$release_dir"
sudo mkdir -p "$release_dir/apps/webportal/.next/cache"
sudo chown -R root:root "$release_dir"
sudo chown -R kermaria-web:kermaria-web "$release_dir/apps/webportal/.next/cache"
sudo chmod 750 "$release_dir/apps/webportal/.next/cache"

sudo ln -sfn "$release_dir" /opt/kermaria/webportal
sudo systemctl restart kermaria-webportal
sleep 3
systemctl is-active kermaria-webportal

echo "PREVIOUS=$current_target"
echo "CURRENT=$release_dir"
readlink -f /opt/kermaria/webportal
```

## 6. Verification post-deploiement

Verifier la sante :

```powershell
curl.exe -k https://dashboard.zacharyhounsa.ovh/api/health/ready
```

Verifier le hero public attendu :

```powershell
npm run assert:webportal:home -- `
  --url https://www.zacharyhounsa.ovh/ `
  --must-match "Un sinistre peut" `
  --must-not-match "Informatique claire et utile\\."
```

Verifier aussi les redirections d'alias :

```powershell
curl.exe -k -I https://www.home.bzh/
curl.exe -k -I https://dashboard.home.bzh/login
curl.exe -k -I https://administration.home.bzh/admin
```

Le script normalise le HTML :

- suppression des commentaires React `<!-- -->`
- reduction des espaces
- verification regex des patterns attendus / interdits

## 7. Regle de bascule

La bascule n'est validee que si :

- la ref Git du manifest est celle attendue ;
- `assert-webportal-home.mjs` passe sur l'URL publique canonique ;
- les aliases `*.home.bzh` redirigent bien vers les hosts `*.zacharyhounsa.ovh` attendus ;
- le marqueur precedent est absent.

Si une de ces trois conditions echoue, rollback immediat.
