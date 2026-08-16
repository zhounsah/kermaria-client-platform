"use client";

import type {
  BillingV2PublicCatalog,
  BillingV2PublicPreset,
  BillingV2PublicQuote,
  BillingV2PublicSelection,
} from "@kermaria/shared";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  SERVICE_CODES,
  buildBaselineSelection,
  describeCheckoutReason,
  findService,
  selectableTiers,
} from "@/lib/billing-v2-formules";
import { formatCurrencyFromCents } from "@/lib/formatters";

type Props = {
  preset: BillingV2PublicPreset;
  catalog: BillingV2PublicCatalog;
};

/**
 * Configurateur public.
 *
 * Le composant n'additionne jamais de prix : a chaque changement il envoie la
 * selection (des codes catalogue) a `/api/formules/devis` et affiche le devis
 * renvoye. Tant que la reponse n'est pas arrivee, le prix precedent reste
 * visible mais marque comme en cours de recalcul.
 */
export function BillingV2FormuleConfigurator({ preset, catalog }: Props) {
  const initialSelection = useMemo(
    () => buildBaselineSelection(preset, defaultCommitment(catalog)),
    [preset, catalog],
  );
  const [selection, setSelection] =
    useState<BillingV2PublicSelection>(initialSelection);
  const [quote, setQuote] = useState<BillingV2PublicQuote | null>(null);
  const [pending, setPending] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const requestSequence = useRef(0);

  useEffect(() => {
    const sequence = requestSequence.current + 1;
    requestSequence.current = sequence;
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
          // Une reponse arrivee dans le desordre ne doit jamais ecraser un
          // devis plus recent : le prix affiche resterait faux a l'ecran.
          if (requestSequence.current !== sequence) {
            return;
          }

          setQuote(payload);
          setError(null);
          setPending(false);
        })
        .catch((reason: unknown) => {
          if (controller.signal.aborted
            || requestSequence.current !== sequence) {
            return;
          }

          setError(
            reason instanceof Error && reason.message === "400"
              ? "Cette combinaison d'options n'est pas disponible."
              : "Le prix n'a pas pu être recalculé. Réessayez dans un instant.",
          );
          setPending(false);
        });
    }, 180);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [selection]);

  const update = useCallback(
    (patch: Partial<BillingV2PublicSelection>) => {
      setSubmitError(null);
      // Le drapeau est pose ici, dans le gestionnaire d'evenement, et non
      // dans l'effet : un setState synchrone en corps d'effet declenche un
      // rendu en cascade. L'etat initial vaut deja `true` pour le premier
      // devis.
      setPending(true);
      setSelection((current) => {
        const next = { ...current, ...patch };
        // Dependance du catalogue : sans espace partage, il n'y a rien a
        // sauvegarder cote partage.
        if (!next.storageSharedTierCode) {
          next.backupShared = false;
        }

        return next;
      });
    },
    [],
  );

  const storageTiers = selectableTiers(catalog, SERVICE_CODES.storagePersonal);
  const sharedTiers = selectableTiers(catalog, SERVICE_CODES.storageShared);
  const vpnTiers = selectableTiers(catalog, SERVICE_CODES.vpn);
  const remoteDesktop = findService(catalog, SERVICE_CODES.remoteDesktop);
  const additionalUser = findService(catalog, SERVICE_CODES.additionalUser);
  const supportPlus = findService(catalog, SERVICE_CODES.supportPlus);
  const backupPersonal = findService(catalog, SERVICE_CODES.backupPersonal);
  const backupShared = findService(catalog, SERVICE_CODES.backupShared);

  async function submit() {
    if (!quote?.checkoutAvailable || !quote.checkoutLegacyOfferId) {
      return;
    }

    setSubmitting(true);
    setSubmitError(null);

    try {
      const response = await fetch("/api/subscriptions/create", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": crypto.randomUUID(),
        },
        body: JSON.stringify({
          offerId: quote.checkoutLegacyOfferId,
          rail: "stripe",
        }),
      });

      if (response.status === 401 || response.status === 403) {
        window.location.href =
          `/login?next=${encodeURIComponent(`/formules/${preset.code}`)}`;
        return;
      }

      const payload = (await response.json()) as {
        approveUrl?: string;
        message?: string;
      };

      if (response.ok && payload.approveUrl) {
        window.location.href = payload.approveUrl;
        return;
      }

      setSubmitError(
        payload.message
          ?? "La souscription n'a pas pu être initialisée. Réessayez ou contactez-nous.",
      );
    } catch {
      setSubmitError(
        "La souscription n'a pas pu être initialisée. Réessayez ou contactez-nous.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="formule-configurator">
      <div className="formule-options">
        <fieldset className="formule-fieldset">
          <legend>Stockage personnel</legend>
          <p className="formule-hint">
            L&apos;espace attribué à l&apos;utilisateur principal.
          </p>
          <div className="formule-choices">
            {storageTiers.map((tier) => (
              <label className="formule-choice" key={tier.code}>
                <input
                  type="radio"
                  name="storage-personal"
                  value={tier.code}
                  checked={selection.storagePersonalTierCode === tier.code}
                  onChange={() =>
                    update({ storagePersonalTierCode: tier.code })}
                />
                <span className="formule-choice-label">{tier.label}</span>
                <span className="formule-choice-price">
                  {formatCurrencyFromCents(tier.monthlyAmountCents)} / mois
                </span>
              </label>
            ))}
          </div>

          <label className="formule-toggle">
            <input
              type="checkbox"
              checked={selection.backupPersonal}
              onChange={(event) =>
                update({ backupPersonal: event.target.checked })}
            />
            <span>
              {backupPersonal?.name ?? "Sauvegarde du stockage personnel"}
              <em className="formule-toggle-note">
                Le volume protégé suit automatiquement la capacité choisie.
              </em>
            </span>
          </label>
        </fieldset>

        <fieldset className="formule-fieldset">
          <legend>Espace partagé</legend>
          <p className="formule-hint">
            Un espace commun à toute la structure, indépendant des comptes.
          </p>
          <div className="formule-choices">
            <label className="formule-choice">
              <input
                type="radio"
                name="storage-shared"
                checked={selection.storageSharedTierCode === null}
                onChange={() => update({ storageSharedTierCode: null })}
              />
              <span className="formule-choice-label">Sans espace partagé</span>
              <span className="formule-choice-price">Inclus</span>
            </label>
            {sharedTiers.map((tier) => (
              <label className="formule-choice" key={tier.code}>
                <input
                  type="radio"
                  name="storage-shared"
                  value={tier.code}
                  checked={selection.storageSharedTierCode === tier.code}
                  onChange={() =>
                    update({ storageSharedTierCode: tier.code })}
                />
                <span className="formule-choice-label">{tier.label}</span>
                <span className="formule-choice-price">
                  {formatCurrencyFromCents(tier.monthlyAmountCents)} / mois
                </span>
              </label>
            ))}
          </div>

          <label className="formule-toggle">
            <input
              type="checkbox"
              checked={selection.backupShared}
              disabled={selection.storageSharedTierCode === null}
              onChange={(event) =>
                update({ backupShared: event.target.checked })}
            />
            <span>
              {backupShared?.name ?? "Sauvegarde de l'espace partagé"}
              <em className="formule-toggle-note">
                Disponible dès qu&apos;un espace partagé est retenu.
              </em>
            </span>
          </label>
        </fieldset>

        <fieldset className="formule-fieldset">
          <legend>Accès à distance</legend>
          <div className="formule-choices">
            <label className="formule-choice">
              <input
                type="radio"
                name="vpn"
                checked={selection.vpnTierCode === null}
                onChange={() => update({ vpnTierCode: null })}
              />
              <span className="formule-choice-label">Sans accès VPN</span>
              <span className="formule-choice-price">Inclus</span>
            </label>
            {vpnTiers.map((tier) => (
              <label className="formule-choice" key={tier.code}>
                <input
                  type="radio"
                  name="vpn"
                  value={tier.code}
                  checked={selection.vpnTierCode === tier.code}
                  onChange={() => update({ vpnTierCode: tier.code })}
                />
                <span className="formule-choice-label">
                  {tier.label}
                  {tier.description ? (
                    <em className="formule-choice-note">{tier.description}</em>
                  ) : null}
                </span>
                <span className="formule-choice-price">
                  {formatCurrencyFromCents(tier.monthlyAmountCents)} / mois
                </span>
              </label>
            ))}
          </div>

          <label className="formule-toggle">
            <input
              type="checkbox"
              checked={selection.remoteDesktop}
              onChange={(event) =>
                update({ remoteDesktop: event.target.checked })}
            />
            <span>
              {remoteDesktop?.name ?? "Accès bureau distant"}
              {remoteDesktop?.flatMonthlyAmountCents ? (
                <em className="formule-toggle-note">
                  {formatCurrencyFromCents(
                    remoteDesktop.flatMonthlyAmountCents,
                  )}{" "}
                  / mois
                </em>
              ) : null}
            </span>
          </label>
        </fieldset>

        <fieldset className="formule-fieldset">
          <legend>Équipe et support</legend>
          <label className="formule-number">
            <span>Utilisateurs supplémentaires</span>
            <input
              type="number"
              min={0}
              max={10}
              step={1}
              value={selection.additionalUsers}
              onChange={(event) =>
                update({
                  additionalUsers: clamp(Number(event.target.value)),
                })}
            />
            {additionalUser?.flatMonthlyAmountCents ? (
              <em className="formule-toggle-note">
                {formatCurrencyFromCents(
                  additionalUser.flatMonthlyAmountCents,
                )}{" "}
                / mois et par utilisateur
              </em>
            ) : null}
          </label>

          <label className="formule-toggle">
            <input
              type="checkbox"
              checked={selection.supportPlus}
              onChange={(event) =>
                update({ supportPlus: event.target.checked })}
            />
            <span>
              {supportPlus?.name ?? "Support Plus"}
              {supportPlus?.flatMonthlyAmountCents ? (
                <em className="formule-toggle-note">
                  {formatCurrencyFromCents(
                    supportPlus.flatMonthlyAmountCents,
                  )}{" "}
                  / mois
                </em>
              ) : null}
            </span>
          </label>
        </fieldset>

        <fieldset className="formule-fieldset">
          <legend>Durée d&apos;engagement</legend>
          <p className="formule-hint">
            Toutes les durées sont payées au mois. La remise s&apos;applique
            au montant mensuel.
          </p>
          <div className="formule-choices">
            {catalog.commitments.map((commitment) => (
              <label className="formule-choice" key={commitment.code}>
                <input
                  type="radio"
                  name="commitment"
                  value={commitment.code}
                  checked={selection.commitmentCode === commitment.code}
                  onChange={() =>
                    update({ commitmentCode: commitment.code })}
                />
                <span className="formule-choice-label">{commitment.name}</span>
                <span className="formule-choice-price">
                  {commitment.discountBasisPoints > 0
                    ? `−${commitment.discountBasisPoints / 100} %`
                    : "Prix de base"}
                </span>
              </label>
            ))}
          </div>
        </fieldset>
      </div>

      <aside
        className={`formule-summary${pending ? " is-pending" : ""}`}
        aria-live="polite"
      >
        <h2>Récapitulatif</h2>

        {error ? <p className="formule-summary-error">{error}</p> : null}

        {quote ? (
          <>
            <ul className="formule-summary-lines">
              {quote.lines.map((line) => (
                <li key={`${line.serviceCode}-${line.detail ?? "flat"}`}>
                  <span className="formule-summary-line-label">
                    {line.label}
                    {line.detail ? (
                      <em> — {line.detail}</em>
                    ) : null}
                    {line.quantity > 1 ? <em> × {line.quantity}</em> : null}
                  </span>
                  <span className="formule-summary-line-amount">
                    {formatCurrencyFromCents(line.amountCents)}
                  </span>
                </li>
              ))}
            </ul>

            <dl className="formule-summary-totals">
              {quote.monthlyDiscountCents > 0 ? (
                <>
                  <div>
                    <dt>Prix mensuel avant remise</dt>
                    <dd className="formule-summary-strike">
                      {formatCurrencyFromCents(
                        quote.monthlyBeforeDiscountCents,
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt>
                      Remise d&apos;engagement (
                      {quote.discountBasisPoints / 100} % sur{" "}
                      {quote.commitmentMonths} mois)
                    </dt>
                    <dd className="formule-summary-discount">
                      −{formatCurrencyFromCents(quote.monthlyDiscountCents)}
                    </dd>
                  </div>
                </>
              ) : null}
              <div className="formule-summary-final">
                <dt>Prix mensuel final</dt>
                <dd>
                  {formatCurrencyFromCents(quote.monthlyAfterDiscountCents)}
                  <span className="formule-summary-period"> / mois</span>
                </dd>
              </div>
            </dl>

            <p className="formule-summary-note">
              Montant calculé par le moteur de facturation Zachary IT, hors
              taxes applicables.
            </p>

            <button
              type="button"
              className="button button-primary formule-summary-action"
              disabled={!quote.checkoutAvailable || pending || submitting}
              onClick={submit}
            >
              {submitting ? "Redirection…" : "Souscrire"}
            </button>

            {quote.checkoutAvailable ? null : (
              <p className="formule-summary-blocked">
                {describeCheckoutReason(quote.checkoutReasonCode)}
              </p>
            )}

            {submitError ? (
              <p className="formule-summary-error">{submitError}</p>
            ) : null}
          </>
        ) : (
          <p className="formule-summary-loading">Calcul du prix en cours…</p>
        )}
      </aside>
    </div>
  );
}

function defaultCommitment(catalog: BillingV2PublicCatalog) {
  return catalog.commitments[0]?.code ?? "FLEX";
}

function clamp(value: number) {
  if (!Number.isFinite(value)) {
    return 0;
  }

  return Math.min(10, Math.max(0, Math.trunc(value)));
}
