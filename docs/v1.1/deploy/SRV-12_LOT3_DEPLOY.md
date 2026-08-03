# Déploiement SRV-12 — V1.1 Lot 3 (écran d'administration des comptes de démo)

> Fiche de passation destinée à l'opérateur du déploiement (Codex).
> Complément de [`SRV-13_LOT3_DEPLOY.md`](SRV-13_LOT3_DEPLOY.md) — **déployer SRV-13 d'abord**.

## 0. Résumé

| | |
|---|---|
| **Cible** | `KERMARIA-SRV-12` (webportal), dossier `C:\apps\webportal\` |
| **Paquet** | `kermaria-webportal-v1.1.0.zip` (6,36 Mo — 24 Mo décompressé) — tag **`v1.1.0`** |
| **SHA256** | `C500688598C4CE42EC76301D0E708CEF5E319EB62BDCCECBB8900B2466B6B451` |
| **Type** | Next.js `output: "standalone"`, layout monorepo-aware |
| **Dépendance** | ⚠️ Suppose l'API SRV-13 **déjà déployée** (les écrans appellent ses endpoints) |

> **Ce paquet contient aussi la remise à plat agentique** (ex-releases 1.0.0.7/1.0.0.8) :
> politique de sauvegarde affichée sur les packs publics (`PublicPackCard`), styles
> `globals.css`, données mock associées. Ce n'est donc pas une livraison « démo » seule.

## 1. Ce que ce paquet ajoute

- `/admin/demo` — liste des comptes de démo (avec colonne **Statut** : actif /
  expiré / révoqué) et formulaire de création (profil, nom, e-mail, mot de passe,
  durée, composition à la carte par cases à cocher).
- `/admin/demo/profiles` — CRUD du registre `demo_profiles`.
- Routes BFF `/api/admin/demo/accounts`, `/api/admin/demo/profiles`,
  `/api/admin/demo/profiles/[key]`.
- Entrée de navigation « Comptes démo » (section Relation client).

Aucune variable d'environnement **nouvelle** côté webportal : les écrans passent par
le BFF existant, qui parle à l'API interne avec la configuration déjà en place.

## 2. Contenu vérifié du paquet

Le layout standalone de Next.js **n'inclut jamais** `.next/static` ni `public` : ils ont
été réintégrés manuellement, conformément au runbook. Contrôles passés à la construction :

| Contrôle | Attendu |
|---|---|
| `apps\webportal\server.js` | présent |
| `apps\webportal\.next\static\` | présent (chunks client) |
| `apps\webportal\public\portfolio\` | présent (portfolio embarqué V0.27) |
| `node_modules\next\` | présent |
| `.next\server\app\admin\demo\**\page.js` | 2 pages compilées |

## 3. Déploiement

Copie en `-staging` puis bascule — jamais d'écrasement direct d'un déploiement vivant.

```powershell
Expand-Archive -Path .\kermaria-webportal-v1.1-lot3.zip -DestinationPath C:\apps\webportal-staging -Force
```

Puis arrêt, bascule, redémarrage du service :

```powershell
Stop-Service KermariaWebportal; Move-Item C:\apps\webportal C:\apps\webportal-old -Force; Move-Item C:\apps\webportal-staging C:\apps\webportal -Force; Start-Service KermariaWebportal
```

Réappliquer les ACL du runbook si le dossier a été remplacé.

## 4. Vérification

1. Le service démarre et le portail répond.
2. Connexion admin → l'entrée **« Comptes démo »** apparaît dans la navigation.
3. `/admin/demo` charge la liste **et** les listes déroulantes de profils
   (4 profils attendus : 3 `showcase` + `trial-ad-koxo`).
   Une liste de profils vide signale que l'API SRV-13 ne répond pas ou n'est pas à jour.
4. `/admin/demo/profiles` affiche le registre et permet l'édition.

> Le bandeau `MockNotice` ne doit **pas** apparaître : sa présence indiquerait que
> l'API répond en mode mock au lieu de la base réelle.

## 5. Recette fonctionnelle (après SRV-12 + SRV-13)

Dans cet ordre, impérativement :

1. **`showcase-tpe`** — inerte par garde-fou dur dans le code : aucun AD, aucun KoXo,
   aucun e-mail, aucun paiement, quelle que soit la configuration globale. Valide la
   création, le contenu semé et l'affichage sans le moindre effet réel.
2. **`trial-ad-koxo`** — durée de vie **très courte**. Vérifier l'ajout aux
   `GG_DEMO_*`, puis la révocation (retrait des groupes + désactivation du compte)
   et la purge au balayage suivant.

## 6. Retour arrière

```powershell
Stop-Service KermariaWebportal; Remove-Item C:\apps\webportal -Recurse -Force; Move-Item C:\apps\webportal-old C:\apps\webportal -Force; Start-Service KermariaWebportal
```

Sans cet écran, l'API reste fonctionnelle : les comptes de démo ne sont simplement
plus pilotables que par appel direct aux endpoints internes.
