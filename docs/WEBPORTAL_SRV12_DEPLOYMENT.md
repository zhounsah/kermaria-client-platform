#
> Current deployed example - 2026-08-28: `v2.0.0.6` / `9f47f25`, active release `/opt/kermaria/releases/20260828-104050-v2.0.0.6-9f47f25`. The generic procedure below remains valid.
 Redeploiement WEBPORTAL sur SRV-12

Ce runbook couvre le redeploiement du front `apps/webportal` sur `KERMARIA-SRV-12`
sans toucher `api-internal`.

Objectif principal : ne plus basculer un artefact dont le contenu public ne
correspond pas a la ref Git attendue.

Mapping canonique valide en production le 20 aout 2026 :

- `zachary-it.fr` = vitrine publique canonique
- `dashboard.zachary-it.fr` = portail client canonique + endpoints webhook
- `administration.zachary-it.fr` = portail admin canonique
- `www.home.bzh` = alias public, redirige vers `zachary-it.fr`
- `dashboard.home.bzh` et `portail.home.bzh` = aliases client, redirigent vers `dashboard.zachary-it.fr`
- `administration.home.bzh` = alias admin, redirige vers `administration.zachary-it.fr`

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

Sur `SRV-12`. **Passer le bloc a `bash` via un heredoc, ne jamais le coller
directement dans la session** — voir l'avertissement juste apres :

```bash
bash <<'DEPLOY'
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
DEPLOY
```

> **`set -euo pipefail` colle dans une session interactive ferme PuTTY.**
> Vecu le 2026-08-06 lors de la bascule `v1.1.12`.
>
> `set -e` fait quitter le shell des qu'une commande renvoie un code non
> nul. Dans un shell de **login**, « quitter » veut dire fermer la session :
> la fenetre PuTTY disparait, sans message, et ca ressemble a un plantage du
> client. `set -u` fait la meme chose au premier `$VARIABLE` non definie —
> une completion ou un prompt suffit. La session reste armee **apres** la
> bascule, donc c'est souvent la commande *suivante*, anodine, qui la tue.
>
> Le heredoc ci-dessus resout le probleme sans rien perdre : les options
> restent actives pour le bloc, dans un `bash` fils, et la session de login
> n'est jamais concernee.
>
> Si une session a deja ete armee, `set +e +u +o pipefail` la desarme. Une
> fois la fenetre fermee, il n'y a qu'a se reconnecter — aucune bascule
> n'est laissee a moitie faite, le symlink n'etant bascule qu'apres
> extraction complete.

## 6. Verification post-deploiement

> **Toute cette section s'execute depuis le poste local, pas sur SRV-12.**
> Se deconnecter de la session SSH avant de continuer. `SRV-12` ne porte que
> le build standalone : ni `package.json` racine, ni
> `scripts/assert-webportal-home.mjs`, ni npm. Lancer `npm run` la-bas sort
> en code non nul — et si la session est encore armee par `set -e`, elle se
> ferme (cf. l'avertissement du paragraphe 5).

Verifier la sante :

```powershell
curl.exe -k https://dashboard.zachary-it.fr/api/health/ready
```

Verifier le hero public attendu :

```powershell
npm run assert:webportal:home -- `
  --url https://zachary-it.fr/ `
  --must-match "Un sinistre peut" `
  --must-not-match "Informatique claire et utile\\."
```

Puis les six controles SEO livres en `v1.1.12` — canonical unique par page,
`h1` unique, `og:image`, routes hors index, sitemap : section « Canonical,
balisage schema.org et hors-index » d'[`OPERATIONS.md`](OPERATIONS.md).

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
