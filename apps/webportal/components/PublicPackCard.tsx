"use client";

import Link from "next/link";
import { useState } from "react";

import type {
  CommercialOfferPaymentMode,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";
import { getPublicPackBackupPolicySummary } from "@kermaria/shared";

import { AddRecurringCheckoutButton } from "@/components/AddRecurringCheckoutButton";
import {
  formatCommercialAmountFromCents,
  formatFiscalMention,
} from "@/lib/fiscal-formatters";
import {
  buildPublicPackSelectionBaseFingerprint,
  isPackSelectionUnavailable,
  normalizeCommitmentMonths,
  resolvePublicPackCardSelection,
  type PublicPackSelectionInput,
  type PublicPackSelectionOverride,
  selectionToContactQueryString,
  selectionToQueryString,
} from "@/lib/public-packs";

type PublicPackCardProps = {
  pack: ResolvedPublicPackManifest;
  mode: "signup" | "subscribe";
  signupEnabled?: boolean;
  initialSelection?: PublicPackSelectionInput | null;
  highlightLabel?: string | null;
};

export function PublicPackCard(props: PublicPackCardProps) {
  const baseFingerprint = buildPublicPackSelectionBaseFingerprint(
    props.pack,
    props.initialSelection ?? null,
  );

  return <StatefulPublicPackCard {...props} key={baseFingerprint} />;
}

function StatefulPublicPackCard({
  pack,
  mode,
  signupEnabled = true,
  initialSelection = null,
  highlightLabel = null,
}: PublicPackCardProps) {
  const [selectionOverride, setSelectionOverride] =
    useState<PublicPackSelectionOverride | null>(null);
  const cardSelection = resolvePublicPackCardSelection(
    pack,
    initialSelection,
    selectionOverride,
  );
  const { commitmentMonths, paymentMode } = cardSelection.selection;
  const backupPolicy = getPublicPackBackupPolicySummary(pack);

  const variantGroup = pack.variantsByCommitment[commitmentMonths];
  const variant = isPackSelectionUnavailable(pack, cardSelection.selection)
    ? null
    : paymentMode === "upfront"
      ? variantGroup.upfront
      : variantGroup.monthly;
  const selection = cardSelection.selection;

  return (
    <article className="public-pack-card">
      <header className="public-pack-header">
        <div className="public-pack-header-copy">
          <p className="card-kicker">Pack grand public</p>
          <h2>{pack.label}</h2>
          <p className="public-pack-audience">{pack.audience}</p>
        </div>
        {highlightLabel ? (
          <span className="status-badge status-badge-info">{highlightLabel}</span>
        ) : null}
      </header>

      <p className="public-pack-headline">{pack.headline}</p>
      <p className="public-pack-description">{pack.description}</p>

      <div className="public-pack-controls">
        <label>
          <span className="public-pack-control-label">Engagement</span>
          <select
            onChange={(event) => {
              const nextCommitmentMonths = normalizeCommitmentMonths(
                event.target.value,
              );
              if (!nextCommitmentMonths) {
                return;
              }
              setSelectionOverride({
                baseFingerprint: cardSelection.baseFingerprint,
                packKey: pack.key,
                commitmentMonths: nextCommitmentMonths,
                paymentMode:
                  nextCommitmentMonths === 1
                  || !pack.variantsByCommitment[nextCommitmentMonths].upfront
                    ? "monthly"
                    : paymentMode,
              });
            }}
            value={String(commitmentMonths)}
          >
            <option value="1">1 mois</option>
            <option value="6">6 mois</option>
            <option value="12">12 mois</option>
          </select>
        </label>

        {!variant ? (
          <div className="public-pack-fixed-choice">
            <span className="public-pack-control-label">Paiement</span>
            <strong>Comptant (indisponible)</strong>
          </div>
        ) : commitmentMonths > 1 && variantGroup.upfront ? (
          <label>
            <span className="public-pack-control-label">Paiement</span>
            <select
              onChange={(event) => {
                setSelectionOverride({
                  baseFingerprint: cardSelection.baseFingerprint,
                  packKey: pack.key,
                  commitmentMonths,
                  paymentMode:
                    event.target.value as CommercialOfferPaymentMode,
                });
              }}
              value={paymentMode}
            >
              <option value="monthly">Mensuel</option>
              <option value="upfront">Comptant</option>
            </select>
          </label>
        ) : (
          <div className="public-pack-fixed-choice">
            <span className="public-pack-control-label">Paiement</span>
            <strong>
              Mensuel
              {commitmentMonths > 1 ? " (comptant indisponible)" : ""}
            </strong>
          </div>
        )}
      </div>

      {!variant ? (
        <div className="public-pack-pricing">
          <div className="public-pack-price-main">
            <strong>Indisponible</strong>
            <span>La sélection comptant n&apos;est plus proposée.</span>
          </div>
          <button
            className="button button-secondary"
            onClick={() => {
              setSelectionOverride({
                baseFingerprint: cardSelection.baseFingerprint,
                packKey: pack.key,
                commitmentMonths,
                paymentMode: "monthly",
              });
            }}
            type="button"
          >
            Passer au mensuel
          </button>
        </div>
      ) : (
        <div className="public-pack-pricing">
        <div className="public-pack-price-main">
          <strong>
            {formatCommercialAmountFromCents(
              variant.monthlyPriceAmountCents,
              {
                fiscalRegime: variant.offer.fiscalRegime,
                suffix: " / mois",
              },
            )}
          </strong>
          <span>
            {formatFiscalMention(
              variant.offer.fiscalRegime,
              variant.offer.fiscalMention,
            )}
          </span>
        </div>
        <span className="public-pack-discount">
          {variant.discountPercent > 0
            ? `Remise ${variant.discountPercent}%`
            : "Sans remise"}
        </span>
        </div>
      )}

      {!variant ? null : (
        <dl className="public-pack-facts">
        <div>
          <dt>Mise en service</dt>
          <dd>
            {formatCommercialAmountFromCents(variant.setupFeeAmountCents, {
              fiscalRegime: variant.offer.fiscalRegime,
            })}
          </dd>
        </div>
        <div>
          <dt>Facturation</dt>
          <dd>
            {paymentMode === "upfront"
              ? `${formatCommercialAmountFromCents(
                  variant.billingPriceAmountCents,
                  { fiscalRegime: variant.offer.fiscalRegime },
                )} tous les ${variant.billingIntervalMonths} mois`
              : formatCommercialAmountFromCents(
                  variant.billingPriceAmountCents,
                  {
                    fiscalRegime: variant.offer.fiscalRegime,
                    suffix: " / mois",
                  },
                )}
          </dd>
        </div>
        <div>
          <dt>Total initial estimé</dt>
          <dd>
            {formatCommercialAmountFromCents(variant.firstChargeAmountCents, {
              fiscalRegime: variant.offer.fiscalRegime,
            })}
          </dd>
        </div>
        </dl>
      )}

      <div className="public-pack-columns">
        <div>
          <h3>Inclus</h3>
          <ul>
            {pack.included.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
        <div>
          <h3>Différences clés</h3>
          <ul>
            {pack.highlights.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      </div>

      <div className="public-pack-policy">
        <p className="public-pack-policy-kicker">
          {backupPolicy.included ? "Sauvegarde incluse" : "Sauvegarde en option"}
        </p>
        <p className="public-pack-policy-text">{backupPolicy.summary}</p>
        <Link className="text-link" href={backupPolicy.detailsHref}>
          {backupPolicy.detailsLabel}
        </Link>
      </div>

      <div className="public-pack-cta">
        {!variant ? null : mode === "signup" ? (
          signupEnabled ? (
            <Link
              className="button"
              href={`/signup?${selectionToQueryString(selection)}`}
            >
              Choisir ce pack
            </Link>
          ) : (
            <Link
              className="button"
              href={`/contact?${selectionToContactQueryString(
                selection,
                variant.offer.id,
              )}`}
            >
              Demander ce pack
            </Link>
          )
        ) : (
          <AddRecurringCheckoutButton
            label="Ajouter au panier"
            offerId={variant.offer.id}
          />
        )}
        {!variant ? null : mode === "signup" ? (
          <Link
            className="button button-secondary"
            href={`/configurer?${selectionToQueryString(selection)}`}
          >
            Personnaliser
          </Link>
        ) : null}
        <Link className="text-link" href={`/offres/${pack.slug}`}>
          Voir la fiche technique
        </Link>
      </div>
    </article>
  );
}
