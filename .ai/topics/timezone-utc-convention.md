---
name: timezone-utc-convention
description: "Convention horodatages (DB UTC / API ISO Z / affichage Paris), 3 récidives NOW(), garde-fou test:timezone, V0.35.1"
metadata: 
  node_type: memory
  type: project
  originSessionId: cf81b032-59b9-4e53-a1ef-4e7f66af8957
---

Convention horodatages du projet (verrouillée V0.23.2, durcie V0.35.1) :
- MariaDB stocke **tout en UTC** : `UTC_TIMESTAMP(6)` en SQL, `DateTime.UtcNow` en paramètre C#. **Jamais `NOW()`/`CURRENT_TIMESTAMP`** — les serveurs (SRV-02/07) sont réglés sur l'heure de Paris, NOW() y renvoie du +2h été.
- API : sérialisation ISO 8601 **avec suffixe Z** — MySqlConnector rend `Kind=Unspecified`, donc toujours `DateTime.SpecifyKind(..., Utc).ToString("O")` à la lecture (helpers ToUtcIso/ToIso par repo).
- Front : affichage exclusivement via `formatDate/formatDateTime` de `apps/webportal/lib/formatters.ts` (force `Europe/Paris`).
- Exception voulue : **dates fiscales/calendaires = jour de Paris** via `KermariaTimeZone.Now` (issue_date BPCE, titres « Echeance yyyy-MM », rotation des logs). Ne pas « corriger » en UtcNow.

**Why:** 3 récidives du même bug (V0.20 BPCE, V0.21 email log, V0.35.1 `MarkDocumentIssued/PaidAsync` + seeds 006/009 + cart) ; le fix V0.20 avait raté `commercial_documents`.

**How to apply:** `npm run test:timezone` contient depuis V0.35.1 un garde-fou statique qui échoue sur toute fonction SQL d'heure locale (hors commentaires) dans `apps/api-internal/{Data,Services,Migrations}` — le lancer sur tout code touchant aux horodatages. Recoupement de vérité en recette : `bpce_invoices.validated_at` et `schema_migrations.applied_at` sont de l'UTC fiable. Doc dédiée : docs/V0.35.1_TIMEZONE_UTC_FIX.md (règle de réparation -2h ciblée, journal SQL). Dump pré-réparation : `tmp/backup-avant-reparation-tz-20260709.sql`. Voir [[deployment-topology]], [[roadmap-current]].
