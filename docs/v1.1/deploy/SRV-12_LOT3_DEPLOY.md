# Déploiement SRV-12 — webportal V1.1

> ⚠️ **Fiche entièrement réécrite le 2026-08-03.** La version précédente décrivait
> une procédure **Windows** (`C:\apps\webportal\`, `Stop-Service`, archive `.zip`) :
> elle était **fausse de bout en bout**. SRV-12 est un **Ubuntu 26.04** piloté par
> systemd. Suivre uniquement cette version.

## 0. Résumé

| | |
|---|---|
| **Cible** | `kermaria-srv-12` — **Ubuntu 26.04**, hors domaine |
| **Accès** | **SSH par clé uniquement** (port 22). Pas de WinRM, pas de SMB (445 fermé). |
| **Service** | `kermaria-webportal.service` (systemd), utilisateur `kermaria-web` |
| **Application** | `/opt/kermaria/webportal` → **lien symbolique** vers `/opt/kermaria/releases/<horodatage>-<version>` |
| **Runtime** | `/opt/kermaria/node/bin/node` |
| **Environnement** | `/etc/kermaria/webportal.env` |
| **Écoute** | `192.168.100.212:3000` (**pas** `localhost` — un `curl localhost:3000` échoue en exit 7) |
| **Format de paquet** | **`.tar.gz` obligatoire** (voir §1) |
| **Privilèges** | `sudo` exige un mot de passe : les étapes privilégiées reviennent à l'exploitant |

## 1. Pourquoi `.tar.gz` et jamais `.zip`

Un zip fabriqué sous Windows porte des séparateurs `\` dans ses entrées. Sous
Linux, `python3 -m zipfile -e` les prend pour des **noms de fichiers littéraux** :
l'extraction produit des milliers de fichiers **à plat** nommés
`apps\webportal\.next\BUILD_ID` au lieu d'une arborescence.

Conséquence observée en production le 2026-08-03 :
`apps/webportal/.next/cache` n'existe pas, or le service le déclare en
`ReadWritePaths=` ; systemd échoue alors à monter son espace de noms
(**`status=226/NAMESPACE`**), boucle sur les redémarrages, et nginx renvoie
**502 Bad Gateway**. Le diagnostic pointe à tort vers le reverse proxy SRV-11.

> **Diagnostic rapide** : `ls -la` sur le répertoire de release. Un **compte de
> liens égal à 2** signale l'absence totale de sous-répertoire, donc ce défaut.

`tar` préserve en outre les permissions et les liens symboliques, que le zip perd.

Fabrication du paquet, depuis le poste de build :

```bash
cd out/webportal-vXXX && tar -czf ../kermaria-webportal-vX.Y.Z.tar.gz apps node_modules
```

Contrôle avant transfert — les entrées doivent utiliser `/` :

```bash
tar -tzf kermaria-webportal-vX.Y.Z.tar.gz | head -3
```

## 2. Composition du paquet

Le layout `standalone` de Next.js **n'inclut jamais** `.next/static` ni `public`.
Le répertoire de préparation doit donc être composé ainsi :

```
<staging>/
├── apps/webportal/          (depuis .next/standalone/, + .next/static + public)
└── node_modules/            (depuis .next/standalone/)
```

## 3. Transfert

```bash
scp -i ~/.ssh/kermaria_srv12 kermaria-webportal-vX.Y.Z.tar.gz zhounsah@kermaria-srv-12:~/
ssh -i ~/.ssh/kermaria_srv12 zhounsah@kermaria-srv-12 'sha256sum ~/kermaria-webportal-vX.Y.Z.tar.gz'
```

Comparer la somme à celle calculée au build **avant** de poursuivre.

## 4. Déploiement (étapes privilégiées)

```bash
REL=/opt/kermaria/releases/$(date +%Y%m%d-%H%M%S)-vX.Y.Z
sudo mkdir -p "$REL"
sudo tar -xzf ~/kermaria-webportal-vX.Y.Z.tar.gz -C "$REL"
sudo chown -R root:root "$REL"
sudo install -d -o kermaria-web -g kermaria-web -m 750 "$REL/apps/webportal/.next/cache"
sudo ln -sfn "$REL" /opt/kermaria/webportal
sudo systemctl restart kermaria-webportal
systemctl is-active kermaria-webportal
```

> ⚠️ **La ligne `install -d` n'est pas optionnelle.** `.next/cache` n'est pas dans
> l'archive : il est créé au déploiement et doit appartenir à `kermaria-web`,
> puisque le service le déclare en `ReadWritePaths=`. L'omettre provoque le même
> `226/NAMESPACE` qu'au §1, **même avec une archive saine**.

## 5. Contrôles

```bash
systemctl is-active kermaria-webportal
curl -s -o /dev/null -w "%{http_code}\n" http://192.168.100.212:3000/
curl -s http://192.168.100.212:3000/ | grep -o "Version v[0-9.]*" | head -1
ls -ld /opt/kermaria/webportal/apps/webportal/.next/cache
```

Attendu : `active`, `200`, la version déployée, et un `cache` appartenant à
`kermaria-web` en mode `750`.

> **Le pied de page est le contrôle le plus fiable d'un déploiement obsolète.**
> Il affiche `Version v${version}` lue dans le `package.json` **racine au moment
> du build**. Corollaire : **toujours bumper la version avant de reconstruire**,
> sinon le libellé ment. Le nom du répertoire de release, lui, peut mentir — un
> dossier `…-v1.1.0` a déjà contenu un build `1.0.0.6`.

## 6. Retour arrière

Le déploiement par lien symbolique rend le repli immédiat : il suffit de
repointer vers la release précédente, toujours présente sur disque.

```bash
ls -la /opt/kermaria/releases/
sudo ln -sfn /opt/kermaria/releases/<release-precedente> /opt/kermaria/webportal
sudo systemctl restart kermaria-webportal
```

## 7. Diagnostic

```bash
systemctl status kermaria-webportal --no-pager | head -20
journalctl -u kermaria-webportal -n 50 --no-pager
sudo tail -50 /var/log/kermaria/webportal.stderr.log
```

| Symptôme | Cause probable |
|---|---|
| `status=226/NAMESPACE` | `.next/cache` absent — archive à plat (§1) ou `install -d` oublié (§4) |
| **502** nginx | Le service boucle sur les redémarrages ; la configuration SRV-11 est rarement en cause |
| Pied de page à l'ancienne version | Version non bumpée avant le build, ou lien symbolique non basculé |
| `curl localhost:3000` en échec (exit 7) | Le service écoute sur `192.168.100.212:3000`, pas sur la boucle locale |

## 8. Ce que la V1.1 ajoute côté webportal

- `/admin/demo` — liste des comptes de démonstration (colonne **Statut**, actions
  **Convertir** et **Supprimer**) et formulaire de création. Les champs d'état
  civil (civilité, prénom, nom, date de naissance) n'apparaissent que pour un
  profil **`trial`** : une vitrine n'étant jamais exportée vers KoXo, la
  contrainte y serait sans objet.
- `/admin/demo/profiles` — CRUD du registre `demo_profiles`.
- Routes BFF `/api/admin/demo/accounts`, `/api/admin/demo/accounts/[reference]`
  (DELETE), `/api/admin/demo/accounts/[reference]/convert`,
  `/api/admin/demo/profiles`, `/api/admin/demo/profiles/[key]`.

Aucune variable d'environnement nouvelle : le BFF utilise la configuration déjà
en place.

## 9. Dépendance

Les écrans appellent l'API interne : **déployer SRV-13 d'abord**
(voir [`SRV-13_LOT3_DEPLOY.md`](SRV-13_LOT3_DEPLOY.md)). Une liste de profils
vide signale que l'API ne répond pas ou n'est pas à jour. Le bandeau `MockNotice`
ne doit **pas** apparaître : sa présence indiquerait une réponse en mode mock au
lieu de la base réelle.
