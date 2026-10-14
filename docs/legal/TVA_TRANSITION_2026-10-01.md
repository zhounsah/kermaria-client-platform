# Zachary IT — transition TVA au 1er octobre 2026

## Autorité et portée

Le mémento fiscal DGFiP communiqué par le propriétaire indique : Zachary
HOUNSA-HOUNKPA EI, nom commercial Zachary IT, SIREN 105111520, SIRET
10511152000018, numéro de TVA FR85105111520, option pour le régime réel normal
à compter du **01/10/2026**, CA3 trimestrielles et micro-BNC inchangé. Ce
document rapporte les faits fournis ; le mémento lui-même n'est pas versionné
dans le dépôt.

Le code applicatif et les contenus ont été préparés et vérifiés sur DEV.
**Aucun déploiement PROD et aucune mutation Stripe Live n'ont eu lieu.** Les
pages publiques de production restent à traiter lors d'une livraison
distincte autorisée par le propriétaire.

Le propriétaire a confirmé le 14/10/2026 qu'aucun client réel n'existe :
les clients, abonnements et factures connus ont été créés pour les tests.
Aucune migration commerciale ni communication client n'est à organiser.
Les pièces fiscales réellement émises restent conservées même lorsqu'elles
documentent un essai.

La liste exacte des 107 fichiers modifiés ou ajoutés pour cette tâche figure
dans `docs/legal/TVA_FILES_2026-10-14.md`. Les fichiers Veeam préexistants
restent hors de ce périmètre.

## Inventaire des mentions et règles corrigées

| Surface courante | Correction |
| --- | --- |
| `SeedContent/cgv.md`, `SeedContent/mentions-legales.md`, `managed_content_entries` DEV, repli `mock-data.ts` | Régime réel normal depuis le 01/10/2026, TVA intracommunautaire et micro-entreprise/micro-BNC maintenus. CGV version du 14/10/2026. |
| `fiscal-formatters.ts`, `FiscalPolicy.cs`, centre admin fiscal, JSON-LD SEO | La franchise n'est plus inférée d'un taux `NULL` ou 0 après la date d'effet. Le numéro de TVA est exposé dans le balisage public. |
| `BillingV2StripeRail.cs`, `BillingV2RenewalLifecycle.cs`, devis publics et Cart, document issuer | TTC contractuel préservé ; ventilation HT/TVA à 20 % par opération française taxable et par groupe de lignes Stripe. Traitement fiscal figé sur les nouvelles lignes. |
| Checkout Stripe, vérification session/facture et chemin Price ID | Tax Rate inclusif obligatoire pour un nouveau Checkout taxable ; total et TVA relus avant confirmation. Ancien chemin Price ID fermé aux nouvelles créations non qualifiées. |
| Pages tarifs, offres, formules, documents, abonnements et administration | TTC explicite, HT indicatif français sur le catalogue et détail HT/TVA/TTC au devis ; les pièces anciennes à qualifier sont signalées sans changer leur texte d'origine. |

Les références à l'article 293 B dans la migration historique `043`, la
documentation ancienne et l'archive des CGV du 03/08/2026 sont conservées en
tant qu'historique, sans être présentées comme le régime actuel.

## DEV : migration, contenus et fournisseurs

- La migration `102_vat_transition` a été appliquée uniquement à
  `kermaria_dev` après sauvegarde. Les nouvelles contraintes imposent
  `prix TTC - remise TTC = total TTC = HT + TVA`. La migration ajoute les
  instantanés de traitement fiscal et la table immuable des versions légales.
- La sauvegarde vérifiée se trouve hors Git dans
  `C:\Users\zhounsah\Backups\Kermaria\kermaria_dev_pre102_20261013_234718.sql`
  (1 365 422 octets ; SHA-256
  `7e602a29869a0cde6cd9af227f74b7a7114790a5c4a7b02d84673fc3df162507`).
  Le compte DEV ne pouvait exporter ni la vue
  `billing_v2_subscription_item_effective_price_components` ni ses triggers ;
  leurs définitions versionnées restent dans la migration `066`.
- Les contenus DEV `legal:cgv` et `legal:mentions-legales` portent désormais
  la version du 14/10/2026. La table de versions conserve aussi les textes
  précédents. Le script `scripts/dev-env/Sync-VatLegalContentDev.ps1` ne cible
  que la base DEV et contrôle les empreintes avant et après la mise à jour.
- Le compte Stripe Test comporte un taux manuel FR 20 % inclusif
  `txr_1UQDalPjVmQIehZaMl0py06U`, vérifié `livemode=false`. Deux sessions
  Test **non payées**, en modes `payment` et `subscription`, ont chacune
  retourné 12 000 centimes TTC dont 2 000 centimes de TVA. La configuration
  API DEV référence ce taux ; son JSON a été sauvegardé avant modification.
- BPCE reste l'émetteur de la facture fiscale. La configuration BPCE du
  service DEV est `live` : les tests d'émission et de PDF utilisent un
  fournisseur mock, sans créer de nouvelle facture BPCE réelle. Le garde
  `BPCE_SELLER_VAT_NUMBER_CONFIRMED` et
  `BPCE_SELLER_EI_MENTION_CONFIRMED` restent faux. Les PDF BPCE DEV existants
  portent le numéro de TVA, mais omettent la mention EI après le nom du
  vendeur. Aucun nouveau Checkout taxable ne doit être ouvert dans cet état.

## Opérations à examiner sans réécriture

La lecture DEV avant et après migration a trouvé deux BillingEvents réglés
depuis le 01/10/2026, de **31,70 €** et **41,60 €**, TVA nulle, et deux
factures BPCE payées correspondantes avec numéro fiscal. Leur total cumulé
reste **73,30 €** après migration. Stripe Test contient trois factures payées
depuis le changement, dont une autre de **5,90 €** au 01/10/2026 à rapprocher
des métadonnées et de l'historique local. Aucun de ces documents n'a été
modifié.

Stripe Live a été consulté en lecture seule : un abonnement actif de 0,50 €
par mois, créé en août, dispose de deux factures payées avant octobre ; aucune
session Checkout ni facture Live d'octobre n'a été trouvée dans l'inventaire
consulté. Aucun abonnement Live, Price, Tax Rate ou paramètre Live n'a été
modifié.

Pour chaque pièce concernée, réunir contrat, période de prestation,
encaissements et PDF original avant de décider s'il faut un avoir ou une
facture rectificative liée au numéro et à la date d'origine. Le code ne
recalcule pas les factures émises. Les deux PDF BPCE d'octobre ont depuis été
récupérés par GET sans nouvelle émission : leurs SHA-256 correspondent aux
empreintes en base. Le numéro `FR85105111520` y apparaît, mais la TVA reste
à 0 % pour les prestations françaises postérieures au changement et le nom
du vendeur omet `EI` ou `Entrepreneur individuel`. Les originaux et l'analyse
sont consignés dans `docs/releases/TVA_PROD_PREPARATION_2026-10-14.md`.
Leur régularisation, ainsi qu'une éventuelle option distincte sur les
débits, restent à décider avec le propriétaire ou son conseil.

## Limites volontaires

Le Checkout automatique refuse un pays client non qualifié ou hors France ;
une autoliquidation, une exonération ou une opération hors champ exige une
revue manuelle avant émission BPCE. Les abonnements existants ne sont pas
repricés ni modifiés : un renouvellement sans instantané fiscal et taux
Stripe cohérents s'arrête en revue, en conservant les droits selon la
politique de grâce existante. Une activation PROD demandera une vérification
du taux Stripe Live, des documents BPCE et des contrats concernés, ainsi
qu'une livraison contrôlée distincte.

## Validation exécutée sur DEV

- `npm run validate` : réussi après les corrections principales ; les suites
  API et les contrats web ont aussi été relancés sur les chemins Billing V2
  concernés après les derniers ajustements.
- `npm run check:web`, puis les vérifications finales de type, lint et build
  Next.js : réussies. Le lint conserve trois avertissements préexistants et
  aucune erreur.
- Tests Pester de configuration DEV : **19/19 réussis**. Les suites API
  Billing V2 couvrent notamment les calculs HT/TTC, la franchise historique,
  la nouvelle facture française, l'abonnement existant, l'autoliquidation,
  le PDF simulé et les contrôles de montants
  Checkout/événement/facture Stripe.
- Contrôle navigateur sur le WebPortal DEV : `/tarifs`, `/formules`, une fiche
  formule, `/offres`, `/cgv` et `/mentions-legales` répondent 200, avec contrôle
  mobile, filtre de tarifs et récapitulatif HT/TVA/TTC. Le devis contrôlé
  affiche **11,90 € TTC = 9,92 € HT + 1,98 € de TVA** et bloque le paiement
  tant que la preuve BPCE manque.
- L'API DEV et le WebPortal DEV ont été livrés et leurs points de santé
  vérifiés. Aucune opération payée de bout en bout avec un PDF BPCE réel n'a
  été exécutée ; la concordance sur une facture Stripe effectivement payée
  reste donc une preuve à obtenir après validation du fournisseur.
