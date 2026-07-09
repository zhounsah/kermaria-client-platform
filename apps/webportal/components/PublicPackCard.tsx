"use client";

import Link from "next/link";
import { useMemo, useState } from "react";

import type {
  CommercialOfferPaymentMode,
  PublicPackCommitmentMonths,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

import { AddRecurringCheckoutButton } from "@/components/AddRecurringCheckoutButton";
import { formatCurrencyFromCents } from "@/lib/formatters";
import {
  type PublicPackSelectionInput,
  selectionToQueryString,
} from "@/lib/public-packs";

type PublicPackCardProps = {
  pack: ResolvedPublicPackManifest;
  mode: "signup" | "subscribe";
  signupEnabled?: boolean;
  initialSelection?: PublicPackSelectionInput | null;
  highlightLabel?: string | null;
};

export function PublicPackCard({
  pack,
  mode,
  signupEnabled = true,
  initialSelection = null,
  highlightLabel = null,
}: PublicPackCardProps) {
  const [commitmentMonths, setCommitmentMonths] =
    useState<PublicPackCommitmentMonths>(
      initialSelection?.packKey === pack.key
        ? initialSelection.commitmentMonths
        : 1,
    );
  const [paymentMode, setPaymentMode] = useState<CommercialOfferPaymentMode>(
    initialSelection?.packKey === pack.key
      ? initialSelection.paymentMode
      : "monthly",
  );
  const rawPaymentMode = commitmentMonths === 1 ? "monthly" : paymentMode;

  const variantGroup = pack.variantsByCommitment[commitmentMonths];
  const variant =
    rawPaymentMode === "upfront" && variantGroup.upfront
      ? variantGroup.upfront
      : variantGroup.monthly;
  const effectivePaymentMode =
    rawPaymentMode === "upfront" && variantGroup.upfront ? "upfront" : "monthly";

  const selection = useMemo<PublicPackSelectionInput>(
    () => ({
      packKey: pack.key,
      commitmentMonths,
      paymentMode: effectivePaymentMode,
    }),
    [commitmentMonths, effectivePaymentMode, pack.key],
  );

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
              const nextCommitmentMonths = Number.parseInt(
                event.target.value,
                10,
              ) as PublicPackCommitmentMonths;
              setCommitmentMonths(nextCommitmentMonths);
              if (nextCommitmentMonths === 1) {
                setPaymentMode("monthly");
              }
            }}
            value={String(commitmentMonths)}
          >
            <option value="1">1 mois</option>
            <option value="6">6 mois</option>
            <option value="12">12 mois</option>
          </select>
        </label>

        {commitmentMonths > 1 ? (
          <label>
            <span className="public-pack-control-label">Paiement</span>
            <select
              onChange={(event) =>
                setPaymentMode(
                  event.target.value as CommercialOfferPaymentMode,
                )
              }
              value={effectivePaymentMode}
            >
              <option value="monthly">Mensuel</option>
              <option value="upfront">Comptant</option>
            </select>
          </label>
        ) : (
          <div className="public-pack-fixed-choice">
            <span className="public-pack-control-label">Paiement</span>
            <strong>Mensuel</strong>
          </div>
        )}
      </div>

      <div className="public-pack-pricing">
        <div className="public-pack-price-main">
          <strong>{formatCurrencyFromCents(variant.monthlyPriceAmountCents)}</strong>
          <span>HT / mois</span>
        </div>
        <span className="public-pack-discount">
          {variant.discountPercent > 0
            ? `Remise ${variant.discountPercent}%`
            : "Sans remise"}
        </span>
      </div>

      <dl className="public-pack-facts">
        <div>
          <dt>Mise en service</dt>
          <dd>{formatCurrencyFromCents(variant.setupFeeAmountCents)} HT</dd>
        </div>
        <div>
          <dt>Facturation</dt>
          <dd>
            {effectivePaymentMode === "upfront"
              ? `${formatCurrencyFromCents(variant.billingPriceAmountCents)} HT tous les ${variant.billingIntervalMonths} mois`
              : `${formatCurrencyFromCents(variant.billingPriceAmountCents)} HT/mois`}
          </dd>
        </div>
        <div>
          <dt>Première échéance</dt>
          <dd>{formatCurrencyFromCents(variant.firstChargeAmountCents)} HT</dd>
        </div>
      </dl>

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

      <div className="public-pack-cta">
        {mode === "signup" ? (
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
              href={`/contact?offer=${encodeURIComponent(variant.offer.id)}`}
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
        <Link className="text-link" href={`/offres/${pack.slug}`}>
          Voir la fiche technique
        </Link>
      </div>
    </article>
  );
}
