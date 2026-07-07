"use client";

import Link from "next/link";
import { useMemo, useState } from "react";

import type {
  CommercialOfferPaymentMode,
  PublicPackCatalogContent,
  PublicPackCommitmentMonths,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

import {
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatPaymentModeLabel,
} from "@/lib/formatters";
import { selectionToQueryString } from "@/lib/public-packs";

type PublicPackComparisonTableProps = {
  content: PublicPackCatalogContent;
  packs: readonly ResolvedPublicPackManifest[];
  signupEnabled: boolean;
};

type SelectedPackColumn = {
  pack: ResolvedPublicPackManifest;
  variant: ResolvedPublicPackManifest["variantsByCommitment"][1]["monthly"];
  isUpfront: boolean;
  highlightLabel: string | null;
  baseMonthlyAmountCents: number;
};

function IncludedIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 20 20">
      <path
        d="M4.5 10.5 8 14l7.5-8"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="2"
      />
    </svg>
  );
}

function ExcludedIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 20 20">
      <path
        d="m5 5 10 10M15 5 5 15"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="2"
      />
    </svg>
  );
}

export function PublicPackComparisonTable({
  content,
  packs,
  signupEnabled,
}: PublicPackComparisonTableProps) {
  const [commitmentMonths, setCommitmentMonths] =
    useState<PublicPackCommitmentMonths>(1);
  const [paymentMode, setPaymentMode] =
    useState<CommercialOfferPaymentMode>("monthly");
  const effectivePaymentMode =
    commitmentMonths === 1 ? "monthly" : paymentMode;

  const rows = useMemo(
    () =>
      content.comparisonRows
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder),
    [content.comparisonRows],
  );

  const orderedPacks = useMemo(
    () => packs.slice().sort((left, right) => left.order - right.order),
    [packs],
  );

  const presentationByPackCode = useMemo(
    () => new Map(content.packs.map((pack) => [pack.packCode, pack])),
    [content.packs],
  );

  const selectedColumns = useMemo<SelectedPackColumn[]>(
    () =>
      orderedPacks.map((pack) => {
        const selectedGroup = pack.variantsByCommitment[commitmentMonths];
        const variant =
          effectivePaymentMode === "upfront" && selectedGroup.upfront
            ? selectedGroup.upfront
            : selectedGroup.monthly;

        return {
          pack,
          variant,
          isUpfront:
            effectivePaymentMode === "upfront"
            && selectedGroup.upfront !== null,
          highlightLabel:
            presentationByPackCode.get(pack.key)?.highlightLabel ?? null,
          baseMonthlyAmountCents:
            pack.variantsByCommitment[1].monthly.monthlyPriceAmountCents,
        };
      }),
    [
      commitmentMonths,
      effectivePaymentMode,
      orderedPacks,
      presentationByPackCode,
    ],
  );

  return (
    <section className="public-pack-compare-section">
      <div className="public-pack-compare-toolbar">
        <label>
          <span className="public-pack-compare-label">Engagement</span>
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
            <span className="public-pack-compare-label">Paiement</span>
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
          <div className="public-pack-compare-fixed-choice">
            <span className="public-pack-compare-label">Paiement</span>
            <strong>Mensuel</strong>
          </div>
        )}

        <div className="public-pack-compare-fixed-choice public-pack-compare-fixed-choice-summary">
          <span className="public-pack-compare-label">Selection</span>
          <strong>
            {formatCommitmentMonths(commitmentMonths)}{" "}
            {commitmentMonths > 1
              ? `- ${formatPaymentModeLabel(effectivePaymentMode)}`
              : ""}
          </strong>
        </div>
      </div>

      <div className="public-pack-compare-wrap">
        <div
          className="public-pack-compare-table"
          style={{
            gridTemplateColumns: `minmax(270px, 0.95fr) repeat(${selectedColumns.length}, minmax(310px, 1fr))`,
          }}
        >
          <div className="public-pack-compare-feature-head">
            {content.pageEyebrow.trim() ? (
              <span className="public-pack-compare-overline">
                {content.pageEyebrow}
              </span>
            ) : null}
            <h2>{content.comparisonColumnLabel}</h2>
            <p>
              Comparez les differences utiles avant de choisir votre pack. Les
              prix, remises et premieres echeances s&apos;ajustent selon la duree
              d&apos;engagement choisie.
            </p>
            <div className="public-pack-compare-legend">
              <span>
                <IncludedIcon />
                Inclus
              </span>
              <span>
                <ExcludedIcon />
                Non inclus
              </span>
            </div>
          </div>

          {selectedColumns.map(
            ({ pack, variant, isUpfront, highlightLabel, baseMonthlyAmountCents }) => {
              const displayedPriceAmountCents = isUpfront
                ? variant.billingPriceAmountCents
                : variant.monthlyPriceAmountCents;
              const referencePriceAmountCents = isUpfront
                ? baseMonthlyAmountCents * commitmentMonths
                : baseMonthlyAmountCents;

              return (
                <article
                  className={`public-pack-compare-column${highlightLabel ? " is-featured" : ""}`}
                  key={pack.key}
                >
                  <div className="public-pack-compare-column-head">
                    <div className="public-pack-compare-badge-slot">
                      {highlightLabel ? (
                        <span className="public-pack-compare-badge">
                          {highlightLabel}
                        </span>
                      ) : (
                        <span
                          aria-hidden="true"
                          className="public-pack-compare-badge-spacer"
                        />
                      )}
                    </div>
                    <h3>{pack.label}</h3>
                    <p className="public-pack-compare-audience">{pack.audience}</p>
                    <p className="public-pack-compare-headline">{pack.headline}</p>
                  </div>

                  <div className="public-pack-compare-price">
                    <div className="public-pack-compare-strike">
                      {variant.discountPercent > 0 ? (
                        <>
                          <span className="public-pack-compare-old-price">
                            {formatCurrencyFromCents(referencePriceAmountCents)}
                          </span>
                          <span className="public-pack-compare-save">
                            -{variant.discountPercent}%
                          </span>
                        </>
                      ) : (
                        <span className="public-pack-compare-price-kicker">
                          Tarif public HT
                        </span>
                      )}
                    </div>
                    <strong>{formatCurrencyFromCents(displayedPriceAmountCents)}</strong>
                    <span className="public-pack-compare-price-caption">
                      {isUpfront
                        ? `HT / ${commitmentMonths} mois`
                        : "HT / mois"}
                    </span>
                  </div>

                  <p className="public-pack-compare-pricing-note">
                    {variant.discountPercent > 0
                      ? `Remise ${variant.discountPercent}%`
                      : "Sans remise"}
                    {" - "}
                    {isUpfront
                      ? `${formatCurrencyFromCents(variant.monthlyPriceAmountCents)} / mois equivalent`
                      : `${formatCurrencyFromCents(variant.billingPriceAmountCents)} par echeance`}
                  </p>

                  <ul className="public-pack-compare-highlights">
                    {pack.highlights.slice(0, 4).map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>

                  <dl className="public-pack-compare-metrics">
                    <div>
                      <dt>Engagement</dt>
                      <dd>{formatCommitmentMonths(commitmentMonths)}</dd>
                    </div>
                    <div>
                      <dt>Paiement</dt>
                      <dd>{formatPaymentModeLabel(isUpfront ? "upfront" : "monthly")}</dd>
                    </div>
                    <div>
                      <dt>Mise en service</dt>
                      <dd>{formatCurrencyFromCents(variant.setupFeeAmountCents)}</dd>
                    </div>
                    <div>
                      <dt>1re echeance</dt>
                      <dd>{formatCurrencyFromCents(variant.firstChargeAmountCents)}</dd>
                    </div>
                  </dl>

                  {signupEnabled ? (
                    <Link
                      className="button"
                      href={`/signup?${selectionToQueryString({
                        packKey: pack.key,
                        commitmentMonths,
                        paymentMode: isUpfront ? "upfront" : "monthly",
                      })}`}
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
                  )}
                  <Link className="text-link" href={`/offres/${pack.slug}`}>
                    Voir la fiche technique
                  </Link>
                </article>
              );
            },
          )}

          {rows.map((row, rowIndex) => (
            <div className="public-pack-compare-row" key={row.id}>
              <div
                className={`public-pack-compare-feature-cell ${rowIndex % 2 === 0 ? "is-even" : "is-odd"}`}
              >
                <span>{row.label}</span>
              </div>
              {selectedColumns.map(({ pack }) => {
                const value = row.values[pack.key];
                return (
                  <div
                    className={`public-pack-compare-value-cell ${rowIndex % 2 === 0 ? "is-even" : "is-odd"}`}
                    key={`${row.id}-${pack.key}`}
                  >
                    {value.kind === "included" ? (
                      <span
                        aria-label={`${row.label} inclus`}
                        className="public-pack-value public-pack-value-included"
                        title="Inclus"
                      >
                        <IncludedIcon />
                      </span>
                    ) : value.kind === "excluded" ? (
                      <span
                        aria-label={`${row.label} non inclus`}
                        className="public-pack-value public-pack-value-excluded"
                        title="Non inclus"
                      >
                        <ExcludedIcon />
                      </span>
                    ) : (
                      <span className="public-pack-value-text">
                        {value.text}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          ))}
        </div>
      </div>

      <aside className="offres-footnote">
        <p>{content.footnotePrimary}</p>
        {content.footnoteSecondary.trim() ? (
          <p>{content.footnoteSecondary}</p>
        ) : null}
      </aside>
    </section>
  );
}
