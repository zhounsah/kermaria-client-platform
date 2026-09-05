"use client";

import { useEffect, useMemo, useState } from "react";

import type {
  BillingV2PublicCatalog,
  BillingV2PublicPriceComponent,
  BillingV2PublicQuote,
  BillingV2PublicSelection,
  BillingV2PublicSelectionComponent,
  BillingV2PublicService,
} from "@kermaria/shared";

import { requestBffJson } from "@/lib/client-api";
import { formatCurrencyFromCents } from "@/lib/formatters";
import {
  describeTierAttributes,
  resolveServicePublicLabel,
} from "@/lib/billing-v2-formules";

/**
 * Souscription Billing V2 « directe » : sans formule.
 *
 * Le client compose une selection a partir des services que le catalogue
 * declare commandables en libre-service, puis souscrit. La selection envoyee
 * ne porte que des codes catalogue et des quantites : ni prix unitaire, ni
 * total, ni remise, ni identifiant de prix fournisseur. Le montant affiche
 * vient integralement de `/api/formules/devis`, qui appelle le meme moteur
 * tarifaire que le checkout — le navigateur ne peut donc pas devenir une
 * seconde autorite financiere.
 *
 * `presetCode` reste `null` : il n'existe aucune formule derriere cette
 * selection, et en forger une pour combler le vide recreerait exactement le
 * catalogue parallele que le modele V2 remplace.
 *
 * `commitmentCode` suit la meme discipline : il est deduit des composantes
 * tarifaires reellement selectionnees, jamais suppose.
 */
type Props = {
  catalog: BillingV2PublicCatalog;
};

type Draft = Map<string, { tierCode: string | null; quantity: number }>;

/**
 * Devis rattache a la selection qui l'a produit.
 *
 * Sans cette cle, une reponse arrivee en retard peut afficher le prix d'une
 * selection que le client vient de modifier. Rattacher le resultat rend cette
 * confusion impossible : un devis dont la cle ne correspond plus n'est
 * simplement pas affiche.
 */
type QuoteOutcome = {
  key: string;
  quote: BillingV2PublicQuote | null;
  error: string | null;
};

const QUOTE_DEBOUNCE_MS = 200;

/**
 * Ce qui suffit a qualifier une composante tarifaire ici : sa cadence et son
 * declencheur. Ni le montant ni la devise n'entrent dans la decision — le
 * navigateur ne doit tirer aucune conclusion d'un prix.
 */
type PriceShape = Pick<
  BillingV2PublicPriceComponent,
  "billingCadence" | "chargeTrigger"
>;

const MONTHLY_FALLBACK: PriceShape = {
  billingCadence: "monthly",
  chargeTrigger: "initial_subscription",
};

export function BillingV2DirectSubscribe({ catalog }: Props) {
  const services = useMemo(() => orderServices(catalog), [catalog]);
  const [draft, setDraft] = useState<Draft>(() => new Map());
  const [outcome, setOutcome] = useState<QuoteOutcome | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const components = useMemo(() => toComponents(draft), [draft]);
  const selectionKey = useMemo(
    () => (components.length > 0 ? JSON.stringify(components) : null),
    [components],
  );
  const selection = useMemo(
    () => (components.length > 0 ? buildSelection(catalog, components) : null),
    [catalog, components],
  );

  // Le resultat n'est retenu que s'il decrit la selection courante : rien a
  // remettre a zero quand elle change, donc rien qui puisse rester affiche a
  // tort.
  const current =
    outcome !== null && outcome.key === selectionKey ? outcome : null;
  const pending = selection !== null && current === null;

  useEffect(() => {
    if (!selection || selectionKey === null) {
      return;
    }

    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      fetch("/api/formules/devis", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(selection),
        signal: controller.signal,
      })
        .then(async (response) => {
          if (!response.ok) {
            throw new Error(String(response.status));
          }

          return (await response.json()) as BillingV2PublicQuote;
        })
        .then((payload) => {
          setOutcome({ key: selectionKey, quote: payload, error: null });
        })
        .catch((reason: unknown) => {
          if (controller.signal.aborted) {
            return;
          }

          setOutcome({
            key: selectionKey,
            quote: null,
            error:
              reason instanceof Error && reason.message === "400"
                ? "Cette combinaison de services n'est pas disponible."
                : "Le prix n'a pas pu être calculé. Réessayez dans un instant.",
          });
        });
    }, QUOTE_DEBOUNCE_MS);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [selection, selectionKey]);

  function toggle(service: BillingV2PublicService, checked: boolean) {
    setSubmitError(null);
    setDraft((previous) => {
      const next = new Map(previous);
      if (!checked) {
        next.delete(service.code);
        return next;
      }

      const firstTier = selectableTiers(service)[0] ?? null;
      next.set(service.code, {
        tierCode: firstTier ? firstTier.code : null,
        quantity: 1,
      });
      return next;
    });
  }

  function setTier(serviceCode: string, tierCode: string) {
    setSubmitError(null);
    setDraft((previous) => {
      const next = new Map(previous);
      const entry = next.get(serviceCode);
      if (entry) {
        next.set(serviceCode, { ...entry, tierCode });
      }
      return next;
    });
  }

  function setQuantity(serviceCode: string, quantity: number) {
    setSubmitError(null);
    setDraft((previous) => {
      const next = new Map(previous);
      const entry = next.get(serviceCode);
      if (entry) {
        next.set(serviceCode, {
          ...entry,
          quantity: Math.min(Math.max(quantity, 1), 100),
        });
      }
      return next;
    });
  }

  async function submit() {
    if (!selection || submitting) {
      return;
    }

    setSubmitting(true);
    setSubmitError(null);

    try {
      // On renvoie la SELECTION, jamais le devis affiche : le serveur
      // revalide la configuration et recalcule integralement le montant.
      const result = await requestBffJson<{
        approveUrl?: string;
        message?: string;
      }>("/api/formules/souscrire", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": crypto.randomUUID(),
        },
        body: JSON.stringify({ ...selection, rail: "stripe" }),
      });

      if (!result.ok) {
        if (result.status === 401 || result.status === 403) {
          window.location.href = "/login?next=%2Fsouscrire";
          return;
        }

        setSubmitError(result.error.message);
        return;
      }

      if (result.data.approveUrl) {
        window.location.href = result.data.approveUrl;
        return;
      }

      setSubmitError(
        result.data.message
          ?? "La souscription n'a pas pu \u00eatre initialis\u00e9e. R\u00e9essayez ou contactez-nous.",
      );
    } catch {
      setSubmitError(
        "La souscription n'a pas pu être initialisée. Réessayez ou contactez-nous.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (services.length === 0) {
    return (
      <p className="subscribe-empty">
        Aucun service n&apos;est actuellement commandable en libre-service. Les
        tarifs sont servis par l&apos;API interne : aucune valeur n&apos;est
        conservée dans le portail.
      </p>
    );
  }

  const byCategory = groupByCategory(services);

  return (
    <div className="subscribe-direct">
      <div className="subscribe-direct-services">
        {byCategory.map(([category, categoryServices]) => (
          <fieldset className="subscribe-direct-group" key={category}>
            <legend>{category}</legend>
            {categoryServices.map((service) => {
              const entry = draft.get(service.code);
              const tiers = selectableTiers(service);
              const selectedTier = entry?.tierCode
                ? tiers.find((tier) => tier.code === entry.tierCode)
                : undefined;
              const tierDescription = selectedTier
                ? describeTierAttributes(selectedTier).join(" · ")
                : "";
              return (
                <div className="subscribe-direct-service" key={service.code}>
                  <label className="subscribe-direct-toggle">
                    <input
                      checked={Boolean(entry)}
                      onChange={(event) =>
                        toggle(service, event.currentTarget.checked)
                      }
                      type="checkbox"
                    />
                    <span>
                      {resolveServicePublicLabel(service.code, service.name)}
                    </span>
                  </label>

                  {entry && tiers.length > 0 ? (
                    <label className="subscribe-direct-tier">
                      <span>Capacité</span>
                      <select
                        onChange={(event) =>
                          setTier(service.code, event.currentTarget.value)
                        }
                        value={entry.tierCode ?? ""}
                      >
                        {tiers.map((tier) => (
                          <option key={tier.code} value={tier.code}>
                            {tier.label}
                          </option>
                        ))}
                      </select>
                      {tierDescription ? (
                        <span className="subscribe-direct-tier-description">
                          {tierDescription}
                        </span>
                      ) : null}
                    </label>
                  ) : null}

                  {entry && service.scopeType === "user" ? (
                    <label className="subscribe-direct-quantity">
                      <span>Quantité</span>
                      <input
                        max={100}
                        min={1}
                        onChange={(event) =>
                          setQuantity(
                            service.code,
                            Number(event.currentTarget.value),
                          )
                        }
                        type="number"
                        value={entry.quantity}
                      />
                    </label>
                  ) : null}
                </div>
              );
            })}
          </fieldset>
        ))}
      </div>

      <aside className="subscribe-direct-summary" aria-live="polite">
        <h3>Votre sélection</h3>
        {!selection ? (
          <p>Sélectionnez au moins un service pour obtenir un tarif.</p>
        ) : current?.error ? (
          <p className="subscribe-direct-error">{current.error}</p>
        ) : pending || !current?.quote ? (
          <p>Calcul du tarif…</p>
        ) : (
          <>
            <ul className="subscribe-direct-lines">
              {/*
                Un meme couple service/palier produit desormais plusieurs
                lignes : un VPS facture 5,90 € par mois ET 19,90 € de mise en
                service. La cadence fait donc partie de l'identite de la ligne ;
                sans elle React voyait deux fois la meme cle et n'affichait
                qu'une des deux lignes.
              */}
              {current.quote.lines.map((line) => (
                <li
                  key={`${line.serviceCode}|${line.tierCode ?? "-"}|${line.billingCadence}`}
                >
                  <span>{line.label}</span>
                  <span>
                    {formatCurrencyFromCents(line.amountCents)}
                    {line.billingCadence === "monthly" ? " / mois" : ""}
                  </span>
                </li>
              ))}
            </ul>
            <p className="subscribe-direct-total">
              <strong>
                {formatCurrencyFromCents(
                  current.quote.monthlyAfterDiscountCents,
                )}
              </strong>
              {" par mois"}
            </p>
            {current.quote.oneTimeCents > 0 ? (
              <p className="subscribe-direct-onetime">
                {formatCurrencyFromCents(current.quote.oneTimeCents)} à la mise
                en service
              </p>
            ) : null}
          </>
        )}

        {submitError ? (
          <p className="subscribe-direct-error">{submitError}</p>
        ) : null}

        <button
          className="button"
          disabled={!selection || !current?.quote || submitting}
          onClick={() => void submit()}
          type="button"
        >
          {submitting ? "Redirection…" : "Souscrire"}
        </button>
        <p className="subscribe-direct-note">
          Sans engagement. Le montant est recalculé par nos serveurs au moment
          du paiement.
        </p>
      </aside>
    </div>
  );
}

/**
 * Ce que le catalogue autorise reellement a la vente en libre-service.
 *
 * Les deux drapeaux sont distincts et le restent : un service peut etre
 * visible publiquement sans etre commandable seul — parce qu'il n'a de sens
 * qu'au sein d'une formule, ou parce que son provisioning demande un arbitrage.
 */
function orderServices(catalog: BillingV2PublicCatalog) {
  return catalog.services
    .filter((service) => service.publicVisible && service.selfServiceOrderable)
    .filter(hasSellablePrice);
}

/**
 * Un service est vendable des qu'il porte au moins une composante tarifaire.
 *
 * Ce n'est pas la meme chose qu'un montant mensuel positif : une prestation
 * facturee uniquement a la mise en service a un mensuel nul, et se retrouvait
 * ecartee du libre-service alors que le catalogue la declarait commandable.
 */
function hasSellablePrice(service: BillingV2PublicService) {
  return (
    priceComponentsFor(service, null).length > 0
    || selectableTiers(service).some(
      (tier) => priceComponentsFor(service, tier.code).length > 0,
    )
  );
}

/**
 * Composantes tarifaires applicables a un service, avec ou sans palier.
 *
 * Le repli « montant mensuel declare vaut composante mensuelle unique »
 * reproduit celui de la projection serveur : un catalogue projete sans
 * composantes explicites (seed de repli, double de test) ne doit pas faire
 * passer un abonnement pour un achat ponctuel.
 */
function priceComponentsFor(
  service: BillingV2PublicService,
  tierCode: string | null,
): readonly PriceShape[] {
  if (tierCode === null) {
    if (service.flatPriceComponents && service.flatPriceComponents.length > 0) {
      return service.flatPriceComponents;
    }

    return service.flatMonthlyAmountCents === null ? [] : [MONTHLY_FALLBACK];
  }

  const tier = service.tiers.find((candidate) => candidate.code === tierCode);
  if (!tier) {
    return [];
  }

  return tier.priceComponents && tier.priceComponents.length > 0
    ? tier.priceComponents
    : [MONTHLY_FALLBACK];
}

function selectableTiers(service: BillingV2PublicService) {
  return service.tiers.filter((tier) => tier.publicSelectable);
}

function groupByCategory(services: readonly BillingV2PublicService[]) {
  const groups = new Map<string, BillingV2PublicService[]>();
  for (const service of services) {
    const key = service.category.trim() || "Services";
    const list = groups.get(key);
    if (list) {
      list.push(service);
    } else {
      groups.set(key, [service]);
    }
  }

  return [...groups.entries()];
}

function toComponents(draft: Draft): BillingV2PublicSelectionComponent[] {
  return [...draft.entries()].map(([serviceCode, entry]) => ({
    serviceCode,
    tierCode: entry.tierCode,
    quantity: entry.quantity,
  }));
}

/**
 * Selection directe : pas de formule, et un engagement seulement s'il a un
 * objet.
 *
 * Les champs historiques du contrat (`storagePersonalTierCode` et ses
 * compagnons) sont laisses a leur valeur neutre. Le resolver serveur, en
 * presence de `components`, ne les consulte pas ; les renseigner ici
 * introduirait une seconde description de la meme intention.
 */
function buildSelection(
  catalog: BillingV2PublicCatalog,
  components: BillingV2PublicSelectionComponent[],
): BillingV2PublicSelection {
  return {
    presetCode: null,
    commitmentCode: hasRecurringComponent(catalog, components)
      ? findNoCommitmentTermCode(catalog)
      : null,
    paymentMode: "monthly",
    storagePersonalTierCode: "",
    backupPersonal: false,
    storageSharedTierCode: null,
    backupShared: false,
    vpnTierCode: null,
    remoteDesktop: false,
    additionalUsers: 0,
    supportPlus: false,
    components,
  };
}

/**
 * La selection comporte-t-elle au moins une composante qui se reconduit ?
 *
 * La reponse vient des composantes tarifaires du catalogue, et d'elles seules.
 * Ni `billingType` — metadonnee d'affichage sans autorite —, ni le montant
 * affiche, ni une hypothese sur le type de service ne peuvent y repondre : un
 * service « recurring » peut n'avoir qu'une mise en service dans le palier
 * choisi, et l'inverse est vrai aussi.
 *
 * Une composante `subscription_change` ne compte pas : elle ne se declenche
 * qu'a une modification ulterieure, jamais a la souscription initiale.
 */
function hasRecurringComponent(
  catalog: BillingV2PublicCatalog,
  components: readonly BillingV2PublicSelectionComponent[],
) {
  return components.some((component) => {
    const service = catalog.services.find(
      (candidate) => candidate.code === component.serviceCode,
    );

    return (
      service !== undefined
      && priceComponentsFor(service, component.tierCode).some(
        (price) =>
          price.billingCadence === "monthly"
          && price.chargeTrigger === "initial_subscription",
      )
    );
  });
}

/**
 * Terme « sans engagement » du catalogue, s'il existe.
 *
 * Un achat ponctuel n'engage a rien et part sans terme. Un abonnement, lui,
 * doit porter le terme que le catalogue publie pour le sans-engagement — une
 * duree d'un mois reglable au mois, sans remise. Ce terme est cherche par sa
 * forme et non par son code : ecrire « FLEX » en dur ferait echouer la
 * souscription en ferme le jour ou le catalogue le renomme, et le serveur
 * refuse un `commitmentCode` inconnu. A defaut de terme correspondant, la
 * selection part sans engagement — le moteur traite alors l'absence comme un
 * mois sans remise, ce qui est exactement la meme chose.
 */
function findNoCommitmentTermCode(catalog: BillingV2PublicCatalog) {
  const term = catalog.commitments.find(
    (commitment) =>
      commitment.months === 1
      && commitment.paymentOptions.some(
        (option) =>
          option.paymentMode === "monthly"
          && option.discountBasisPoints === 0,
      ),
  );

  return term ? term.code : null;
}
