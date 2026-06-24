# Rotation des secrets

## Principe

Un secret publié, envoyé dans un canal non maîtrisé ou collé dans un outil
externe doit être considéré compromis. La rotation doit être immédiate, suivie
d'une vérification des logs, sessions et accès.

Les anciennes et nouvelles valeurs ne doivent jamais être écrites dans Git,
les tickets, les captures d'écran ou les commandes conservées dans
l'historique.

## Mot de passe MariaDB de développement

1. Réaliser une sauvegarde.
2. Se connecter avec un compte autorisé à gérer l'utilisateur applicatif.
3. Adapter la partie hôte à la définition réelle du compte.
4. Exécuter :

```sql
ALTER USER '<SQL_USERNAME>'@'%' IDENTIFIED BY '<NEW_STRONG_PASSWORD>';
FLUSH PRIVILEGES;
```

`@'%'` est un exemple. Utiliser l'hôte réellement limité à API-INTERNAL.

5. Mettre à jour `SQL_PASSWORD` dans le gestionnaire de secrets local.
6. Redémarrer API-INTERNAL.
7. Vérifier `/health/ready` et les tests MariaDB opt-in.
8. Vérifier que l'ancien mot de passe ne fonctionne plus.

## Comptes démo client et admin

Ces comptes existent uniquement en `Development`.

1. Générer deux mots de passe forts distincts hors dépôt.
2. Injecter `DEMO_PORTAL_PASSWORD` et
   `DEMO_INTERNAL_ADMIN_PASSWORD` dans le processus de migration.
3. Exécuter explicitement :

```powershell
dotnet run --project .\apps\api-internal\Kermaria.ApiInternal.csproj -- --apply-migrations --seed-demo-data
```

4. Révoquer les sessions existantes des comptes concernés :

```sql
UPDATE portal_sessions
SET revoked_at = UTC_TIMESTAMP(6)
WHERE user_id IN (
  SELECT id
  FROM portal_users
  WHERE email IN ('<DEMO_CLIENT_EMAIL>', '<DEMO_ADMIN_EMAIL>')
)
AND revoked_at IS NULL;
```

5. Vérifier les deux logins et le contrôle des rôles.

Ne jamais utiliser ce seed en Production.

## Token interservice

`SERVICE_AUTH_TOKEN` protège les routes `/internal/*` dans tout environnement
non `Development`.

1. Générer une nouvelle valeur forte hors dépôt.
2. Mettre à jour le secret API-INTERNAL.
3. Mettre à jour le même secret WEBPORTAL.
4. Redémarrer d'abord API-INTERNAL puis WEBPORTAL dans une fenêtre courte.
5. Vérifier `/health/ready` et `/api/health/ready`.
6. Vérifier login, lecture client et lecture admin.
7. Supprimer l'ancienne valeur du gestionnaire de secrets.

Le token n'est jamais renvoyé au navigateur ni loggé.

## Refresh token BPCE

`BPCE_REFRESH_TOKEN` est un JWT long-lived délivré par le dashboard BPCE
(`https://www.gestion-factures.banquepopulaire.fr`). Il permet d'obtenir
des access tokens courts pour appeler l'API facturation. Sa compromission
permet à un tiers d'émettre de vraies factures fiscales au nom de
l'entreprise tant que `BPCE_INTEGRATION_MODE=live`.

1. Se connecter au dashboard BPCE avec le compte propriétaire.
2. Révoquer le refresh token compromis depuis l'écran "API / Clés".
3. Générer une nouvelle clé (label rappelant l'usage, ex. `RDC-07`).
4. Mettre à jour `BPCE_REFRESH_TOKEN` dans le gestionnaire de secrets local
   (jamais dans `.env.example` ni dans le dépôt).
5. Redémarrer API-INTERNAL.
6. Vérifier `--verify-bpce-sender` : la commande doit lister les profils
   sans erreur 401.
7. Confirmer qu'aucune émission ne se fait avec l'ancien token
   (`BPCE_INTEGRATION_MODE=disabled` ou `mock` tant que la rotation n'est
   pas validée).

Le refresh token n'est jamais renvoyé au navigateur, jamais loggé et
jamais inséré dans une réponse d'erreur.

## Credentials PayPal

`PAYPAL_CLIENT_ID` et `PAYPAL_CLIENT_SECRET` proviennent du dashboard
PayPal Developer. La compromission du secret permet à un tiers de créer
des ordres et captures au nom de l'app.

1. Se connecter à `https://developer.paypal.com`.
2. Sur l'app concernée, régénérer le `Client Secret` (révoque l'ancien).
3. Mettre à jour `PAYPAL_CLIENT_SECRET` dans le gestionnaire de secrets
   local.
4. Redémarrer WEBPORTAL.
5. Vérifier qu'un paiement sandbox de bout en bout fonctionne avant tout
   passage en `PAYPAL_MODE=live`.
6. Si l'ancien secret a été exposé en clair (canal non maîtrisé), inspecter
   l'activité de l'app dans le dashboard PayPal pour repérer toute
   transaction non autorisée.

Le `PAYPAL_CLIENT_SECRET` n'est jamais renvoyé au navigateur et le cache
mémoire de l'access token est purgé au redémarrage du processus.

## Sessions

Après compromission d'un mot de passe utilisateur ou d'un hôte, révoquer les
sessions concernées. En cas de doute global :

```sql
UPDATE portal_sessions
SET revoked_at = UTC_TIMESTAMP(6)
WHERE revoked_at IS NULL;
```

Cette opération déconnecte tous les utilisateurs. Elle doit être auditée et
réalisée dans une fenêtre annoncée.

## Vérification Git

```powershell
npm.cmd run check:secrets
git status --short
git diff --check
git grep -n -I -E "SQL_PASSWORD=|SERVICE_AUTH_TOKEN=|DEMO_.*_PASSWORD=|BPCE_REFRESH_TOKEN=|PAYPAL_CLIENT_SECRET="
```

Les résultats légitimes doivent être des placeholders ou de la documentation.
Le scan est un garde-fou simple, pas un substitut à un outil de détection de
secrets côté forge.

## Validation après rotation

1. `npm run validate`.
2. API `/health/live` et `/health/ready` à 200.
3. WEBPORTAL `/api/health/live` et `/api/health/ready` à 200.
4. Login client et admin.
5. Refus admin pour `client_user`.
6. Demandes support/service avec `persisted:true`.
7. Absence de secret dans les logs.
