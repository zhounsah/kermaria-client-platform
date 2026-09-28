# Prochain E2E DEV — abonnement et droit AD

Statut : **STOP — aucune option sûre n'est actuellement exécutable.**

Ce document prépare le prochain scénario sans créer de souscription, modifier
la base, appeler un provider, lancer KoXo ou écrire dans Active Directory.

## Objectif futur

Valider, pour un nouveau client DEV distinct de Melis Rochedune et des Run 1/2 :

```text
portal_user → subscription active → desired state par identité
→ identité AD strictement liée → appartenance AD attendue
```

Le résultat doit rester borné au namespace `CLIENTS DEV` et à son client
secondaire `DEV-CLI-*`. Il ne doit ni réutiliser les objets existants, ni
toucher `CLIENTS`, `CLIENTS DÉMO`, `GG_VPN`, `GG_RDS` ou FS-01 sans une
autorisation dédiée.

## Éligibilité analysée

| Candidat | État désiré généré | Pourquoi il n'est pas le prochain E2E sûr |
| --- | --- | --- |
| `ACCES-VPN` | groupe `GG_VPN` | Groupe parent-domain hors `CLIENTS DEV`, explicitement hors périmètre. |
| `ACCES-RDS` | groupe `GG_RDS` | Même dépendance parent-domain, non résolue. |
| Stockage personnel/partagé | quota KoXo, puis prérequis des accès | `Repair* Type="Storage"` headless non validé ; aucune écriture FS-01 autorisée. |
| `USER-ADDITIONAL` | entitlement de place | Autorise une place commerciale ; il ne crée pas un droit de groupe pour l'identité principale. |
| `SUPPORT-PLUS` / onboarding | entitlement reconnu | Aucun groupe AD n'est porté par le desired state. |
| VPS | revue et fulfillment spécifiques | N'est pas un scénario minimal de groupe AD et introduit une décision technique supplémentaire. |

Le runtime de provisioning établit que les accès AD user-scoped exigent déjà un
lien `customer_ad_links` unique, et qu'un accès VPN/RDS sans stockage personnel
est refusé. La revue de readiness est donc nécessaire mais ne rend pas ces
deux dépendances sûres par elle-même.

## Plan automatisable une fois une option autorisée

1. **Préflight lecture seule.** Pour le nouveau client DEV choisi, appeler
   uniquement le preview admin de readiness V2. Il doit confirmer : identité
   AD unique, règles de catalogue résolues, groupe cible configuré, aucune
   incompatibilité de scope et provider de stockage non requis pour le service
   retenu. Le preview ne crée ni readiness, ni audit, ni objet externe.
2. **Décision explicite.** Ne poursuivre que si Zachary choisit l'une des
   options ci-dessous et autorise les prérequis d'infrastructure associés.
3. **Souscription manuelle contrôlée.** Créer un nouveau client DEV et une
   seule souscription test, puis attendre son état `active`. Aucun partage
   d'identité avec Melis, aucun service additionnel.
4. **Convergence observée.** Vérifier le desired state, l'identité
   `customer_ad_links`, puis l'unique appartenance du compte AD cible. Le
   contrôle est add-only : aucun retrait de droit ne doit être attendu au
   premier passage.
5. **Stop immédiat.** Stopper sur absence de lien AD, règles non résolues,
   readiness non validée, groupe hors namespace autorisé, tentative KoXo/FS-01
   non prévue, ou toute modification de `CLIENTS` / `CLIENTS DÉMO` / `GG_*`.

## Décision attendue de Zachary

Choisir une seule voie avant tout E2E runtime :

1. **Créer et mapper un groupe de service strictement DEV** sous `CLIENTS DEV`,
   avec une règle de catalogue et un DN DEV revus. Cela demande une décision de
   modèle et une écriture AD explicitement autorisée.
2. **Autoriser un test borné sur `GG_VPN` ou `GG_RDS`** après résolution des
   groupes parent-domain et de leurs délégations. Cette voie sort du périmètre
   DEV-only actuel.
3. **Lever d'abord la dette stockage headless** avec un test KoXo fournisseur
   autorisé ; seulement ensuite évaluer un scénario stockage, qui ne prouve
   toutefois pas à lui seul une appartenance AD de service.

Sans cette décision, le plan s'arrête au préflight lecture seule.
