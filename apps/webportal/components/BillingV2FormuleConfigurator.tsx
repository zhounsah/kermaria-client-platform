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
  formatCommitmentDurationLabel,
  formatDiscountPercent,
  resolveServicePublicLabel,
  selectableTiers,
} from "@/lib/billing-v2-formules";
import {
  MAX_ADDITIONAL_USERS,
  billingV2SelectionToSearchParams,
} from "@/lib/billing-v2-selection";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getPortalArea, resolvePortalAreaUrl } from "@/lib/public-route-config";

type Props = {
  preset: BillingV2PublicPreset;
  catalog: BillingV2PublicCatalog;
  initialSelection?: BillingV2PublicSelection | null;
};

/**
 * Configurateur public.
 *
 * Le composant n'additionne jamais de prix : a chaque changement il envoie la
 * selection (des codes catalogue) a `/api/formules/devis` et affiche le devis
 * renvoye. Tant que la reponse n'est pas arrivee, le prix precedent reste
 * visible mais marque comme en cours de recalcul.
 */
export function BillingV2FormuleConfigurator({
  preset,
  catalog,
  initialSelection: resumedSelection = null,
}: Props) {
  const initialSelection = useMemo(
    () => resumedSelection?.presetCode === preset.code
      ? resumedSelection
      : buildBaselineSelection(preset, defaultCommitment(catalog)),
    [preset, catalog, resumedSelection],
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

        // Le mode de reglement doit rester une option reellement ouverte par
        // l'engagement retenu : changer de duree ne doit jamais laisser un
        // "comptant" orphelin que le serveur refuserait ensuite.
        const options =
          catalog.commitments.find(
            (commitment) => commitment.code === next.commitmentCode,
          )?.paymentOptions ?? [];
        if (!options.some((option) => option.paymentMode === next.paymentMode)) {
          next.paymentMode = "monthly";
        }

        return next;
      });
    },
    [catalog],
  );

  const commitment = catalog.commitments.find(
    (item) => item.code === selection.commitmentCode,
  );
  const upfrontOption = commitment?.paymentOptions.find(
    (option) => option.paymentMode === "upfront",
  );
  const monthlyOption = commitment?.paymentOptions.find(
    (option) => option.paymentMode === "monthly",
  );
  const isUpfront = selection.paymentMode === "upfront";

  const storageTiers = selectableTiers(catalog, SERVICE_CODES.storagePersonal);
  const sharedTiers = selectableTiers(catalog, SERVICE_CODES.storageShared);
  const vpnTiers = selectableTiers(catalog, SERVICE_CODES.vpn);
  const remoteDesktop = findService(catalog, SERVICE_CODES.remoteDesktop);
  const additionalUser = findService(catalog, SERVICE_CODES.additionalUser);
  const supportPlus = findService(catalog, SERVICE_CODES.supportPlus);
  const backupPersonal = findService(catalog, SERVICE_CODES.backupPersonal);
  const backupShared = findService(catalog, SERVICE_CODES.backupShared);

  async function submit() {
    if (!quote?.checkoutAvailable) {
      return;
    }

    const currentArea = getPortalArea(window.location.origin);
    if (currentArea === "public") {
      const signupPath = `/signup?${billingV2SelectionToSearchParams(selection)}`;
      window.location.href = resolvePortalAreaUrl(
        window.location.origin,
        "public",
        signupPath,
      ) ?? signupPath;
      return;
    }

    setSubmitting(true);
    setSubmitError(null);

    try {
      // On renvoie la SELECTION, jamais le devis affiche : le serveur
      // revalide la configuration et recalcule integralement le montant. Un
      // prix altere dans le navigateur n'a donc aucun effet.
      const response = await fetch("/api/formules/souscrire", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": crypto.randomUUID(),
        },
        body: JSON.stringify({ ...selection, rail: "stripe" }),
      });

      if (response.status === 401 || response.status === 403) {
        const continuationPath = `/formules/${preset.code}`;
        const signupPath = `/signup?${billingV2SelectionToSearchParams(selection)}`;
        const currentArea = getPortalArea(window.location.origin);
        const target = currentArea === "client"
          ? resolvePortalAreaUrl(
              window.location.origin,
              "client",
              `/login?next=${encodeURIComponent(continuationPath)}`,
            )
          : resolvePortalAreaUrl(
              window.location.origin,
              "public",
              signupPath,
            );

        window.location.href = target ?? signupPath;
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
              {/*
                Le catalogue nomme ce service « Acces bureau distant RDS » :
                l'acronyme d'exploitation n'a pas sa place dans une page
                publique, la traduction commerciale est centralisee.
              */}
              {resolveServicePublicLabel(
                SERVICE_CODES.remoteDesktop,
                remoteDesktop?.name ?? "Bureau Windows à distance",
              )}
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
          <p className="formule-hint">
            Une place d&apos;utilisateur suppl&eacute;mentaire ajoute un compte nominatif
            de plus. Elle ne duplique pas automatiquement le stockage personnel,
            la sauvegarde personnelle, l&apos;acc&egrave;s s&eacute;curis&eacute; ni le bureau &agrave;
            distance du titulaire. Le v&ocirc;tre est d&eacute;j&agrave; inclus.
          </p>
          <div className="formule-stepper">
            <span className="formule-stepper-label">
              Utilisateurs supplémentaires
              {additionalUser?.flatMonthlyAmountCents ? (
                <em className="formule-toggle-note">
                  {formatCurrencyFromCents(
                    additionalUser.flatMonthlyAmountCents,
                  )}{" "}
                  / mois et par utilisateur
                </em>
              ) : null}
            </span>
            <span className="formule-stepper-controls">
              <button
                type="button"
                aria-label="Retirer un utilisateur"
                disabled={selection.additionalUsers <= 0}
                onClick={() =>
                  update({
                    additionalUsers: clamp(selection.additionalUsers - 1),
                  })}
              >
                −
              </button>
              <output aria-live="polite">{selection.additionalUsers}</output>
              <button
                type="button"
                aria-label="Ajouter un utilisateur"
                disabled={selection.additionalUsers >= MAX_ADDITIONAL_USERS}
                onClick={() =>
                  update({
                    additionalUsers: clamp(selection.additionalUsers + 1),
                  })}
              >
                +
              </button>
            </span>
          </div>

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
          <legend>Durée</legend>
          <p className="formule-hint">
            Plus la durée est longue, plus la remise est importante.
          </p>
          <div className="formule-choices">
            {catalog.commitments.map((item) => {
              const best = item.paymentOptions.reduce(
                (max, option) =>
                  Math.max(max, option.discountBasisPoints),
                0,
              );

              return (
                <label className="formule-choice" key={item.code}>
                  <input
                    type="radio"
                    name="commitment"
                    value={item.code}
                    checked={selection.commitmentCode === item.code}
                    onChange={() => update({ commitmentCode: item.code })}
                  />
                  <span className="formule-choice-label">
                    {formatCommitmentDurationLabel(item.months, item.name)}
                  </span>
                  <span className="formule-choice-price">
                    {best > 0
                      ? `jusqu'à −${formatDiscountPercent(best)} %`
                      : "Prix de base"}
                  </span>
                </label>
              );
            })}
          </div>
        </fieldset>

        {/*
          Le mode de reglement est toujours annonce, meme quand il n'y a rien a
          choisir : masquer entierement le bloc en « sans engagement » laissait
          croire que le paiement en une fois n'existe pas, alors qu'il suffit de
          prendre une duree.
        */}
        <fieldset className="formule-fieldset">
          <legend>Mode de paiement</legend>
          {upfrontOption ? (
            <div className="formule-choices">
              <label className="formule-choice">
                <input
                  type="radio"
                  name="payment-mode"
                  value="monthly"
                  checked={!isUpfront}
                  onChange={() => update({ paymentMode: "monthly" })}
                />
                <span className="formule-choice-label">
                  Mensuel
                  <em className="formule-choice-note">
                    Prélevé chaque mois pendant {commitment?.months} mois.
                  </em>
                </span>
                <span className="formule-choice-price">
                  {monthlyOption && monthlyOption.discountBasisPoints > 0
                    ? `−${formatDiscountPercent(
                        monthlyOption.discountBasisPoints,
                      )} %`
                    : "Prix de base"}
                </span>
              </label>
              <label className="formule-choice">
                <input
                  type="radio"
                  name="payment-mode"
                  value="upfront"
                  checked={isUpfront}
                  onChange={() => update({ paymentMode: "upfront" })}
                />
                <span className="formule-choice-label">
                  En une fois
                  <em className="formule-choice-note">
                    Un seul règlement couvrant les {commitment?.months} mois,
                    sans prélèvement ensuite.
                  </em>
                </span>
                <span className="formule-choice-price">
                  −{formatDiscountPercent(upfrontOption.discountBasisPoints)} %
                </span>
              </label>
            </div>
          ) : (
            <p className="formule-hint">
              Sans engagement, le règlement est mensuel et s&apos;arrête quand
              vous le décidez. Le paiement en une fois est proposé à partir
              d&apos;une durée de 6 mois.
            </p>
          )}
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
                <li key={`${line.serviceCode}-${line.tierCode ?? "flat"}`}>
                  <span className="formule-summary-line-label">
                    {resolveServicePublicLabel(line.serviceCode, line.label)}
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
              <div>
                <dt>Prix avant remise</dt>
                <dd
                  className={
                    quote.monthlyDiscountCents > 0
                      ? "formule-summary-strike"
                      : undefined
                  }
                >
                  {formatCurrencyFromCents(quote.monthlyBeforeDiscountCents)}
                  <span className="formule-summary-period"> / mois</span>
                </dd>
              </div>
              {quote.discountBasisPoints > 0 ? (
                <div>
                  <dt>
                    Remise (−{formatDiscountPercent(quote.discountBasisPoints)}{" "}
                    %)
                  </dt>
                  <dd className="formule-summary-discount">
                    −{formatCurrencyFromCents(quote.monthlyDiscountCents)}
                    <span className="formule-summary-period"> / mois</span>
                  </dd>
                </div>
              ) : null}
              {isUpfront ? (
                <>
                  <div className="formule-summary-final">
                    <dt>À régler aujourd&apos;hui</dt>
                    <dd>{formatCurrencyFromCents(quote.totalDueNowCents)}</dd>
                  </div>
                  <div>
                    <dt>Soit par mois</dt>
                    <dd>
                      {formatCurrencyFromCents(quote.monthlyAfterDiscountCents)}
                      <span className="formule-summary-period">
                        {" "}
                        / mois équivalent
                      </span>
                    </dd>
                  </div>
                </>
              ) : (
                <div className="formule-summary-final">
                  <dt>Prix final</dt>
                  <dd>
                    {formatCurrencyFromCents(quote.monthlyAfterDiscountCents)}
                    <span className="formule-summary-period"> / mois</span>
                  </dd>
                </div>
              )}
              {quote.commitmentSavingsCents > 0 ? (
                <div>
                  <dt>Économie totale sur {quote.commitmentMonths} mois</dt>
                  <dd className="formule-summary-discount">
                    {formatCurrencyFromCents(quote.commitmentSavingsCents)}
                  </dd>
                </div>
              ) : null}
            </dl>

            {/*
              Vocabulaire du contrat comptant : le client paie une fois, rien
              ne repart ensuite. Ne jamais y parler de prochaine facturation.
            */}
            {isUpfront ? (
              <p className="formule-summary-note">
                <strong>Paiement en une fois.</strong> Contrat de{" "}
                {quote.commitmentMonths} mois. Aucun prélèvement mensuel, aucun
                renouvellement automatique.
              </p>
            ) : (
              <p className="formule-summary-note">
                <strong>Prélèvement mensuel.</strong>{" "}
                {quote.commitmentMonths > 1
                  ? `Pendant ${quote.commitmentMonths} mois, puis libre.`
                  : "Sans engagement : vous arrêtez quand vous le souhaitez."}
              </p>
            )}

            <p className="formule-summary-note formule-summary-note-secondary">
              Ce montant correspond exactement à la configuration ci-dessus. Il
              est indiqué hors taxes applicables.
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

  return Math.min(MAX_ADDITIONAL_USERS, Math.max(0, Math.trunc(value)));
}
