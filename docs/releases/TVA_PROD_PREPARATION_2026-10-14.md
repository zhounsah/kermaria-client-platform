# Préparation de la livraison TVA — décision PROD en attente

**État au 14 octobre 2026.** Cette fiche prépare une livraison distincte ;
elle n'autorise ni mutation Stripe Live, ni paiement, ni migration ou bascule
PROD. Aucun client réel n'existe actuellement. Les pièces fiscales créées au
cours des tests sont conservées et ne sont jamais réécrites par SQL.
La version candidate retenue est **`v2.1.1`**, sans commit ni tag à ce stade.

## 1. Preuves fiscales et blocage BPCE

Les PDF originaux BPCE `FC-TEST-2026-025` et `FC-TEST-2026-026` ont été
récupérés par **GET** auprès du fournisseur et conservés hors Git dans
`C:\Users\zhounsah\Backups\Kermaria\bpce-audit-20261014`. Les SHA-256
correspondent aux empreintes de `kermaria_dev` :

| Numéro | Émission | Période indiquée | TTC | HT affiché | TVA affichée | SHA-256 |
| --- | --- | --- | ---: | ---: | ---: | --- |
| FC-TEST-2026-025 | 04/10/2026 | 04/10–04/11 | 31,70 € | 31,70 € | 0,00 € | `0ee90dcc5c1743a4d5b41d99d76b8cc8751dca96510104c28065bc59ff772c86` |
| FC-TEST-2026-026 | 12/10/2026 | 12/10–12/11 | 41,60 € | 41,60 € | 0,00 € | `387db678d8fff3dbb515fe09c5797ccf0a78a3e2b723555477dd1004466bb36c` |

Le vendeur et les deux clients sont situés en France dans les enregistrements
DEV. Les PDF portent `TVA: FR85105111520`, le SIREN et une numérotation
fiscale, mais appliquent **0 %** à toutes les prestations postérieures au
01/10/2026. Le nom du vendeur est rendu sans `EI` ni `Entrepreneur individuel`.
La [fiche officielle des mentions d'une facture de micro-entreprise](https://entreprendre.service-public.gouv.fr/vosdroits/F36008)
demande la désignation EI, le taux applicable et la ventilation HT/TTC.
Ces deux factures ne prouvent donc **pas** la conformité de la nouvelle
facturation taxable. Les montants théoriques, sous réserve de qualification
fiscale des pièces, seraient respectivement 26,42 € HT + 5,28 € TVA et
34,67 € HT + 6,93 € TVA, à TTC conservé.

Conserver les PDF et demander un avis comptable sur un avoir/facture
rectificative référant chaque original. Ne pas faire d'`UPDATE` sur les
factures, événements ni PDF. L'absence de client réel dispense d'une
communication commerciale, pas de la conservation des pièces émises.

Le numéro de TVA est vérifié sur les PDF existants. La mention EI doit être
corrigée dans le profil émetteur BPCE puis constatée sur un **nouveau PDF**.
La lecture seule de l'API BPCE retrouve dans les configurations DEV et PROD
le **même** profil émetteur `774259`, `name=HOUNSA-HOUNKPA ZACHARY`,
`profile_name=PHASE_TESTS` : sa correction toucherait les futures factures
des deux environnements. Elle exige une validation explicite et ne modifie
pas les PDF historiques.
Les deux flags `BPCE_SELLER_VAT_NUMBER_CONFIRMED` et
`BPCE_SELLER_EI_MENTION_CONFIRMED` restent faux jusqu'au contrôle final ;
le second empêche d'interpréter le premier comme une approbation générale.
La vérification d'un PDF taxable à 20 % requiert une émission contrôlée
après validation explicite du propriétaire.

**Classement des constats :** VALIDE pour la présence du numéro de TVA,
l'absence de `EI` et les taux 0 % visibles sur les deux PDF ; VALIDE pour
l'identité du profil émetteur commun DEV/PROD. INCERTAIN pour le traitement
comptable définitif de ces opérations fictives : il dépend des pièces et de
l'avis fiscal avant tout avoir ou facture rectificative.

**Conservation complémentaire :** les six PDF fiscaux `FC-TEST-2026-021`
à `026` connus dans `kermaria_dev` ont été récupérés par GET et sauvegardés
dans ce même dossier, avec un `manifest.sha256`. En PROD, les trois PDF
fiscaux `018` à `020` ont été récupérés et vérifiés contre leurs empreintes
SQL, avec un `manifest-prod.sha256`. Un quatrième enregistrement PROD est
un artefact `MOCK-*`, sans PDF BPCE réel. Aucun contenu de facture n'a été
modifié. Les copies hors Git sont accessibles au compte local, aux
administrateurs et à SYSTEM.

**Défaut confirmé :** `ConfirmPaymentAsync` utilisait
`UpdateInvoiceValidatedAsync` avec un PDF nul ; l'implémentation MariaDB
remplaçait alors `pdf_content` par `NULL`. Cela explique les six PDF absents
du cache DEV et le PDF fiscal `019` absent du cache PROD malgré leurs
empreintes conservées. Le code préparé sépare désormais la mise à jour du
seul statut `paid` et refuse une récupération PDF dont le hash diffère de
celui déjà enregistré. Les lignes historiques n'ont pas été mises à jour ;
une réhydratation éventuelle du cache devra être tracée séparément après
approbation, avec comparaison des empreintes avant et après.

## 2. Stripe : observations en lecture seule

- **Test** : le taux `txr_1UQDalPjVmQIehZaMl0py06U` est inclusif à 20 %,
  pays FR, `livemode=false`. Deux Checkout non payés (`payment` et
  `subscription`) ont conservé 120,00 € TTC et affiché 20,00 € de taxe.
  Trois factures Test payées en octobre (5,90 €, 31,70 €, 41,60 €) n'ont
  pas de taxe ; celle de 5,90 € couvre le 01/10–01/11 et a été réglée le
  04/10. Aucun PDF BPCE correspondant à 5,90 € n'a été trouvé en DEV.
- **Live** : un abonnement de test actif `sub_1U69iaPeE7Jrh43pmzJ5vAnD`
  facture 0,50 € par mois, `automatic_tax=false`, sans `default_tax_rates` ;
  son Price a `tax_behavior=unspecified`. Sa période actuelle finit le
  **19/10/2026 à 15 h 39, heure de Paris**. Ses deux factures payées sont
  antérieures au 01/10. Il y a 30 Price récurrents actifs au comportement
  fiscal non défini, **aucun Tax Rate Live** (GET `/v1/tax_rates`) et aucune
  immatriculation Stripe Tax enregistrée. Aucun
  de ces objets n'a été modifié.

Le compte Live exige **son propre** Tax Rate FR 20 % `inclusive` avant
l'ouverture d'un nouveau Checkout taxable. Garder `automatic_tax=false`
dans le modèle à taux manuel, et appliquer le taux aux lignes du Checkout
et à `subscription_data.default_tax_rates`. Ne pas réutiliser l'identifiant
Test, modifier les anciens Price IDs ou repricer l'abonnement existant.
La [documentation Stripe des Tax Rates manuels](https://docs.stripe.com/payments/checkout/use-manual-tax-rates?payment-ui=stripe-hosted)
précise les paramètres du Checkout et le caractère inclusif du taux.

Décider explicitement du sort de l'abonnement Live de test **avant le 19/10** :
résiliation à échéance, maintien avec qualification fiscale, ou autre action
documentée. La nouvelle application ne peut pas empêcher Stripe de créer
seul une facture récurrente sur cet ancien abonnement.

## 3. Pré-état PROD vérifié

| Composant | État lu le 14/10/2026 |
| --- | --- |
| Git | `main`, corrections TVA non commitées ; documentation Veeam validee a inclure dans le futur commit selectif |
| MariaDB SRV-06 | `kermaria`, MariaDB 11.8.6 ; migration 101 présente, 102 absente ; 0 facture BPCE datée d'octobre, 2 souscriptions Billing V2 actives ; 4 enregistrements BPCE dont 2 caches PDF manquants (1 mock, 1 réel) |
| CGV PROD | `Version du : 15 août 2026`, SHA-256 `d699a8db41bc4fa5a6fd5df16c6f541ac512107326ca5409fc5525361873b424` |
| Mentions PROD | `Dernière mise à jour : 07 juillet 2026`, SHA-256 `c2ab966ed1e824bfb1824c346cc90c34c8cda2deeadbd6ae7fa1d31607fee7b6` |
| API SRV-13 | service actif ; DLL SHA-256 `5EFE1DCBEC34A0BC821AA427D03CC291C48998D621513C4258421291A5B391CE` ; config SHA-256 `4B508512533650BE494649977B459388D435A3B7267CC61174E42A145AE620FA` |
| Web SRV-12 | service actif ; lien `/opt/kermaria/webportal` vers `/opt/kermaria/releases/v2.1.0-hotfix.1-0f7d652-prod` ; env SHA-256 `422a8fca52dddb9c9657b972eacb689edf826e5d5af59bd83770c60522ba507f` |
| Intégrations | API PROD : BPCE `live`, Stripe `live`, Checkout V2 et outbox actifs ; Tax Rate FR Live et confirmations BPCE absents |

L'inspection reproductible est `pwsh -NoProfile -File
scripts/prod/Inspect-VatProdReadiness.ps1`. Le script de contenu légal
`scripts/prod/Sync-VatLegalContentProd.ps1` est en **inspection seule par
défaut** ; `-Apply` n'est permis qu'après la migration 102 et l'approbation
de la livraison.

Le 14/10, `npm run validate` a réussi sur le worktree de préparation :
secrets, lint (trois avertissements préexistants, zéro erreur), types,
builds, smoke tests et contrats Web/API dont Billing V2, BPCE et Stripe.
`git diff --check` et le contrôle des secrets de `.ai/` passent. Ce worktree
contient encore des modifications non commitées : **aucun artefact PROD
provenant d'un tag propre n'a été créé**.

## 4. Ordre d'exécution proposé après validation finale

1. Choisir une **version/tag exact** et constituer un commit sélectif des
   fichiers TVA après revue du diff. Inclure seulement la documentation Veeam verifiee, sans code Veeam. Synchroniser
   README, ROADMAP, note de version et `.ai/` avant ce commit. Exécuter
   `npm run validate`, les tests Billing V2 et `git diff --check` sur un
   checkout propre de ce tag. Construire les paquets API Windows `win-x64`
   avec apphost et Web Linux avec `sharp`/`libvips` depuis le même commit ;
   consigner leurs SHA-256 et manifestes avant toute copie.
2. Réinspecter SRV-12, SRV-13, SRV-06 et Stripe Live, puis fermer les
   nouveaux Checkout V2 par le drapeau API
   `BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED=false`, en vérifiant le refus
   BFF/API. Vérifier les autres rails de paiement exposés avant la fenêtre ;
   s'ils restent utilisables, bloquer temporairement leurs routes au proxy.
   Les webhooks des paiements déjà en cours restent traités et audités.
3. Sauvegarder `kermaria` **avant migration**, hors Git, avec les droits
   nécessaires pour tables, blobs de factures, triggers et vues valides.
   Le dump standard du précédent déploiement a dû exclure une vue invalide ;
   consigner toute exclusion, le code de sortie, la taille, le marqueur final
   et SHA-256, puis éprouver la restauration sur une base isolée. Sauvegarder
   séparément les configurations externes et relever les répertoires API/Web
   actuellement actifs. Aucun dump incomplet ne justifie la bascule.
4. Appliquer **uniquement** `102_vat_transition.sql` avec le migrateur de la
   release, `--project` absolu et cible `kermaria` vérifiée. Contrôler
   `schema_migrations`, les deux contraintes modifiées, les colonnes et la
   table de versions légales. Recompter et comparer factures, abonnements,
   totaux et empreintes PDF : la migration ne doit pas les changer.
5. Exécuter `Sync-VatLegalContentProd.ps1` en mode inspection, puis `-Apply`
   dans la fenêtre approuvée. Il exige les deux versions/empreintes PROD lues
   en pré-état, archive les corps existants et publie les versions du
   14/10/2026. Relire les empreintes et les anciennes versions conservées.
6. Déployer l'API depuis le paquet/tag exact sur SRV-13 **avant** le Web.
   Conserver le dossier binaire précédent et les valeurs externes non visées.
   Poser le futur identifiant de Tax Rate Live dans
   `STRIPE_FR_VAT20_INCLUSIVE_TAX_RATE_ID` et le numéro vendeur
   `BPCE_SELLER_VAT_NUMBER=FR85105111520` ; maintenir les confirmations
   BPCE à `false` jusqu'aux preuves PDF. Vérifier `KERMARIA_CONFIG_AUTHORITATIVE`
   et la priorité des variables Machine avant restart. Contrôler le hash du
   binaire, le service et `/health/ready`.
7. Déployer le WebPortal depuis son `.tar.gz` Linux sur SRV-12, créer le
   cache Next.js avec les droits `kermaria-web`, basculer le lien symbolique,
   redémarrer uniquement son service. Préserver `/etc/kermaria/webportal.env`.
   Vérifier `/`, `/tarifs`, `/formules`, `/cgv`, `/mentions-legales`, BFF health,
   assets/image optimisée, admin et client, SEO `vatID`, versions CGV et
   absence des anciennes mentions courantes. Garder l'achat fermé.
8. Après correction du profil BPCE, vérifier d'abord le profil vendeur et,
   si BPCE le permet, un PDF d'aperçu non fiscal portant `EI` et le numéro
   de TVA. Si aucun aperçu n'est disponible, l'activation des confirmations
   BPCE pour le seul essai suivant est une **dérogation provisoire**, tracée
   et approuvée expressément ; les remettre à `false` au moindre écart du
   PDF fiscal réellement émis. Préparer alors un achat **contrôlé**
   par le propriétaire : proposer une formule mensuelle de **5,90 € TTC**
   au taux français 20 %, soit **4,92 € HT + 0,98 € TVA**. Utiliser uniquement
   son propre compte de test identifié, sans client tiers, ouvrir le Checkout
   pendant une fenêtre surveillée, et confirmer lui-même le paiement Stripe
   Live. Vérifier session, facture Stripe, webhook, événement et base,
   abonnement, document BPCE et PDF original : même total TTC, 20 % inclusif,
   vendeur avec `EI` et TVA FR, périodes et numéro fiscal. Conserver la
   facture même si l'achat est annulé ensuite ; toute annulation/remboursement
   et la résiliation avant renouvellement sont des décisions explicites.
9. N'ouvrir le Checkout général qu'après cette preuve complète, le traitement
   décidé de l'abonnement Live de 0,50 €, et un nouvel accord opérationnel.

## 5. Critères d'arrêt et retour arrière

**Arrêter sans ouverture des achats** si le backup/restauration, la migration,
les empreintes légales, la disponibilité API/Web, la configuration Stripe
Live ou le PDF BPCE ne correspondent pas aux attentes. Une réussite des tests
mock et des sessions Stripe Test ne remplace pas le PDF fiscal taxable réel.

- Avant tout paiement : garder les flags de Checkout/API fermés ; remettre si
  nécessaire le binaire API précédent et le lien Web précédent, après
  vérification de leurs empreintes. Les contenus légaux corrigés et
  l'archive CGV restent publiés. L'ancien code pouvant encore annoncer une
  TVA nulle, **ne pas rouvrir les paiements** sur le binaire précédent.
- Après un paiement ou une facture : conserver événements, abonnements,
  pièces BPCE/Stripe, numéros et PDF. Couper les nouveaux Checkout et traiter
  la commande en revue. Ne pas annuler automatiquement l'abonnement, le
  paiement ni le droit provisionné, et ne pas restaurer le dump sur la base
  courante : une restauration écraserait ces preuves. Décider séparément un
  avoir, une facture rectificative ou un remboursement.
- La migration 102 est additive et la logique ancienne écrit encore des
  montants satisfaisant les nouvelles contraintes. Ne pas `DROP` ses colonnes
  ou restaurer la base pour un simple retour applicatif. Si une restauration
  devient indispensable, la préparer sur une base isolée puis rapprocher les
  écritures créées depuis le backup avant toute décision.

Les étapes ci-dessus demeurent **préparatoires**. La décision finale doit
préciser la version exacte, la fenêtre, la correction du profil BPCE, le sort
de l'abonnement Live de test et l'autorisation séparée du paiement contrôlé.

## 6. Recette DEV de la candidate v2.1.1

Les derniers correctifs API ont été installés sur **KermariaApiInternalDev**
uniquement. La DLL locale et la DLL DEV portent le SHA-256
`3FFC1641A61A755EBAE40C1FEB94D1579FCC075844836AC1799AE0D3C8499525`.
Le JSON DEV conserve son SHA-256
`85BD80D22B21F6E76DA7BD251BC73E447F91CA9538BECEBB00FB2755A81C350F` ;
la DLL API PROD garde son empreinte précédente. `/health/ready` répond 200.

Le WebPortal DEV actif pointe vers
`/opt/kermaria/releases-dev/vat-dev-v2.1.1-20261014-rc1`, archive SHA-256
`BE9889900579519D16478F7A8960C678C720DAA7BEA30469D6FCB40FB7F36FCD`,
build ID `mzPX8pzh-L4j6p8Ae0rgO`. Le paquet inclut `sharp` 0.35.5 et
`libvips` validés sur Linux aux empreintes du lockfile. L'environnement Web
DEV conserve son SHA-256
`8cc1ff4ed17deb2623fbca6ae8ef33a3507969d897899ea7d7d28ea1e8f5b185`.
Le service et les pages DEV répondent 200 ; le service et le lien PROD sont
restés intacts. Une première vérification HTTP trop précoce a déclenché le
rollback automatique DEV ; une relance avec attente de disponibilité a réussi.

`npm run validate` sur la candidate 2.1.1 a réussi intégralement. Le test
navigateur a revérifié `/tarifs`, `/formules`, une fiche formule, `/offres`,
`/cgv` et `/mentions-legales` en vue bureau/mobile : 200, aucun débordement,
aucune erreur console. Le devis de 11,90 € TTC détaille 9,92 € HT et
1,98 € de TVA ; le paiement reste bloqué par le garde BPCE.
L'index Git prepare 107 fichiers TVA et 3 fichiers documentaires Veeam supplementaires (110 fichiers uniques). La mise a jour Veeam de `.ai/MEMORY.md` est egalement indexee.
Aucun commit, tag ou push n'a eu lieu.

## 7. Décision GO/NO-GO et autorisations distinctes

**GO technique pour la préparation et la fermeture contrôlée des nouveaux
Checkout PROD après accord. NO-GO pour l'ouverture générale des paiements
taxables et pour la bascule PROD aujourd'hui.** Le profil BPCE n'affiche pas
`EI`, le Tax Rate Live n'existe pas, la migration 102 et les CGV courantes
ne sont pas en PROD, et aucun PDF BPCE taxable payé n'a été vérifié.

| Action préparée, non exécutée | Vérification préalable | Autorisation explicite requise |
| --- | --- | --- |
| Fermer immédiatement les nouveaux Checkout V2 PROD | `Close-BillingV2CheckoutProd.ps1` en inspection, hash JSON exact, aucune variable Machine concurrente ; après application, refus de création et health 200 | Oui, pour `-Apply` et le redémarrage d'API PROD |
| Corriger le profil BPCE `774259` commun DEV/PROD en `Zachary HOUNSA-HOUNKPA EI` | Archiver état vendeur avant/après et vérifier que `FR85105111520` reste sur le PDF | Oui, mutation du profil BPCE Live |
| Résilier immédiatement `sub_1U69iaPeE7Jrh43pmzJ5vAnD` | Métadonnées Stripe liées à la souscription et au BillingEvent PROD de 0,50 € ; deux factures payées, aucune facture ouverte et aucun invoice item en attente ; relecture juste avant action | Oui, suppression Stripe Live avec `invoice_now=false`, `prorate=false`, sans remboursement automatique |
| Créer le Tax Rate Stripe Live FR manuel 20 % inclusif | Compte Live exact, aucun Tax Rate existant ; `automatic_tax=false` | Oui, création d'objet Stripe Live, puis configuration de son ID dans l'API PROD |
| Commit sélectif, tag `v2.1.1`, push | Index TVA avec documentation Veeam seulement, diff et validations de la candidate propres | Oui, actions Git interdites sans demande explicite |
| Migrer et déployer PROD | Backup complet et restauration testée, migration 102, archive CGV, hashes et rollback prêts | Oui, pour SQL PROD, contenus légaux, configuration et services API/Web |
| Paiement contrôlé de 5,90 € TTC | Profil BPCE corrigé, taux Live, Checkout borné et suivi temps réel prêts | Oui, autorisation distincte du débit réel et de la gestion ultérieure de l'abonnement de test |
| Régulariser les factures de test antérieures | Avis fiscal sur la réalité économique et pièces originales préservées | Oui, décision comptable distincte ; aucun `UPDATE` de facture |

Les opérations techniques exactes à soumettre au propriétaire sont :

```text
Checkout PROD : pwsh -NoProfile -File scripts/prod/Close-BillingV2CheckoutProd.ps1 -Apply -ExpectedConfigSha256 <empreinte relue juste avant>
BPCE Live : profil émetteur 774259 ; nom cible "Zachary HOUNSA-HOUNKPA EI" ; vérifier TVA FR85105111520 avant/après.
Stripe Live : DELETE /v1/subscriptions/sub_1U69iaPeE7Jrh43pmzJ5vAnD ; invoice_now=false ; prorate=false ; aucun remboursement automatique.
Stripe Live : POST /v1/tax_rates ; display_name=TVA ; percentage=20 ; inclusive=true ; country=FR ; automatic_tax demeure false.
Git : commit sélectif indexé, tag annoté v2.1.1, push de la branche et du tag uniquement après revue du commit exact.
PROD : backup vérifié, migration 102, Sync-VatLegalContentProd.ps1 -Apply, API SRV-13 puis Web SRV-12.
Paiement : un seul achat contrôlé de 5,90 EUR TTC, confirmé par le propriétaire dans Checkout Live après autorisation distincte.
```

L'abonnement Live de 0,50 € est relié par ses métadonnées à la souscription
PROD `aa4bc2b5-b9bc-450b-aba2-b07b3cdfcc56` et au BillingEvent
`b6847dba-c247-4e9e-91d2-a331b14bf1ab`, finalisé et réglé à 50 centimes.
Il n'a ni changement programmé ni invoice item en attente. En l'absence
d'intervention, Stripe prévoit toujours sa prochaine période au 19/10/2026.
