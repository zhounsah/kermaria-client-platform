# Reprise et vidage du panier Billing V2

Un panier `open` est unique par propriétaire et devise. Un panier vide peut
rester `open` et conserver son identifiant.

Après authentification, `claim_current` transfère le panier anonyme si le
client ne possède aucun panier `open` dans cette devise. Si le client possède
déjà un panier `open`, celui-ci gagne : le panier anonyme passe à `expired`
dans la transaction du claim, sans fusion ni suppression de ses lignes.
L'API renvoie `CART_CLAIM_RESUMED` avec le panier client. Le BFF supprime alors
le cookie de possession anonyme ; l'accès au panier client repose sur la
session authentifiée. Une ancienne URL de souscription pointant vers le panier
anonyme est réalignée sur l'identifiant du panier client.

`clear` exige l'identifiant et la version courante du panier `open`. La
transaction supprime ses lignes, remet à `NULL` le preset source, l'engagement
et le mode de paiement, puis incrémente la version. Le statut et l'identifiant
restent inchangés. Les anciens devis sont conservés comme historique, mais
leur `cart_version` ne correspond plus : ils ne peuvent plus être acceptés au
checkout. Aucun prix n'est calculé côté navigateur pour l'état vide.

Les collisions de clé unique et les deadlocks du claim déclenchent au plus
trois tentatives complètes. Les index de slots `open` restent l'arbitre final
de l'unicité. Le test MariaDB de concurrence est opt-in sur une base jetable
explicitement autorisée ; les tests contractuels locaux ne prouvent pas seuls
le comportement InnoDB.

## Preuves MariaDB (`--billing-v2-cart-schema`)

Validé le 2026-09-26 sur une base jetable SRV-06 (MariaDB 11.8.6), puis
détruite. Le test couvre :

- le claim concurrent quand un panier client existe (`CART_CLAIM_RESUMED`) ;
- le claim concurrent sans panier client : un seul `CART_CLAIMED`, les
  autres appels renvoient `CART_NOTHING_TO_CLAIM`, sans exception ;
- `claim` explicite : une version périmée est refusée sans effet, puis le
  chemin `RESUMED` s'applique sans fusion ;
- `clear` sur un panier réellement rempli, avec lignes, preset, engagement et
  mode de paiement, puis refus de l'ancienne version.

Chaque garde a été retirée une à une (mutants) ; le test échoue alors.

Limites :

- la borne de trois tentatives n'est pas forcée, car on ne provoque pas un
  deadlock de façon déterministe ;
- le `FOR UPDATE` de la lecture du panier anonyme est redondant avec le
  verrou déjà posé par l'expiration ciblée qui le précède. Retirer seulement
  le `FOR UPDATE` ne casse rien ; retirer les deux donne des exceptions
  concurrentes.
