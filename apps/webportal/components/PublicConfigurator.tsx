"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

import type {
  CatalogConfigurationInput,
  CatalogConfigurationResolution,
  CatalogConfigurationWarningCode,
  PublicPackCode,
  PublicPackCommitmentMonths,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

import { FormMessage } from "@/components/FormMessage";
import {
  formatCommercialAmountFromCents,
  formatFiscalMention,
  shouldShowVatBreakdown,
} from "@/lib/fiscal-formatters";
import { formatCurrencyFromCents } from "@/lib/formatters";
import {
  configurationToQueryString,
  packKeyFromMaybeString,
  updateConfigurationPaymentMode,
} from "@/lib/public-configurator";

type PublicConfiguratorProps = {
  packs: ResolvedPublicPackManifest[];
  initialConfiguration: CatalogConfigurationInput;
  initialResolution: CatalogConfigurationResolution | null;
  signupEnabled: boolean;
};

const WARNING_LABELS: Record<CatalogConfigurationWarningCode, string> = {
  storage_unknown:
    "Le volume reste a preciser. L'estimation part du pack choisi sans option inventee.",
  storage_not_standard:
    "Le volume demandé sort des variantes standards proposées en ligne.",
  windows_storage_not_standard:
    "Le volume demandé dépasse la variante standard du bureau Windows à distance.",
  users_not_standard:
    "Le nombre d'utilisateurs demande necessite un cadrage.",
  windows_team_not_standard:
    "Un bureau Windows distant pour plusieurs utilisateurs necessite un cadrage.",
  requested_pack_adjusted:
    "Le besoin demande correspond mieux a un autre pack standard.",
  variant_unavailable:
    "La variante d'engagement ou de paiement n'est plus disponible.",
};

export function PublicConfigurator({
  packs,
  initialConfiguration,
  initialResolution,
  signupEnabled,
}: PublicConfiguratorProps) {
  const [configuration, setConfiguration] = useState(initialConfiguration);
  const [resolution, setResolution] =
    useState<CatalogConfigurationResolution | null>(initialResolution);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const serializedConfiguration = useMemo(
    () => JSON.stringify(configuration),
    [configuration],
  );

  useEffect(() => {
    const controller = new AbortController();
    queueMicrotask(() => {
      if (!controller.signal.aborted) {
        setIsLoading(true);
        setError(null);
      }
    });

    fetch("/api/configurer/resolve", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: serializedConfiguration,
      signal: controller.signal,
    })
      .then(async (response) => {
        const payload = (await response.json()) as
          | CatalogConfigurationResolution
          | { message?: string };
        if (!response.ok) {
          throw new Error(
            "message" in payload && payload.message
              ? payload.message
              : "La configuration n'a pas pu etre resolue.",
          );
        }
        setResolution(payload as CatalogConfigurationResolution);
      })
      .catch((caught: unknown) => {
        if (controller.signal.aborted) {
          return;
        }
        setResolution(null);
        setError(caught instanceof Error ? caught.message : "Erreur inconnue.");
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [serializedConfiguration]);

  const selectedPack = packs.find((pack) => pack.key === configuration.packKey);
  const isVpnIncluded = selectedPack?.capabilities.supportsVpn === true;
  const isWindowsDesktopIncluded =
    selectedPack?.capabilities.supportsWindowsDesktop === true;
  const resolvedPack = resolution?.resolvedConfiguration
    ? packs.find((pack) => pack.key === resolution.resolvedConfiguration?.packKey)
    : selectedPack;
  const simulation = resolution?.priceSimulation ?? null;
  const signupHref =
    signupEnabled && resolution?.status === "ok" && resolution.resolvedConfiguration
      ? `/signup?${configurationToQueryString(resolution.resolvedConfiguration)}`
      : null;

  return (
    <div className="configurator-shell">
      <section className="configurator-controls" aria-label="Votre configuration">
        <div className="configurator-section-heading">
          <h2>Votre configuration</h2>
          <p>
            Personnalisez votre offre selon vos besoins. Le tarif est mis à
            jour automatiquement.
          </p>
        </div>

        <label className="configurator-field">
          <span>Pack</span>
          <select
            value={configuration.packKey}
            onChange={(event) => {
              const packKey = packKeyFromMaybeString(event.target.value);
              const nextPack = packs.find((pack) => pack.key === packKey);
              if (packKey) {
                setConfiguration((current) =>
                  applyIncludedCapabilities({ ...current, packKey }, nextPack));
              }
            }}
          >
            {packs.map((pack) => (
              <option key={pack.key} value={pack.key}>
                {pack.label}
              </option>
            ))}
          </select>
        </label>

        <div className="configurator-grid-controls">
          <label className="configurator-field">
            <span>Utilisateurs</span>
            <select
              value={String(configuration.users ?? "")}
              onChange={(event) => {
                const value = event.target.value;
                setConfiguration((current) => ({
                  ...current,
                  users: value ? Number(value) : null,
                }));
              }}
            >
              <option value="">A preciser</option>
              {[1, 2, 3, 4, 5, 6].map((value) => (
                <option key={value} value={value}>
                  {value === 6 ? "6+" : value}
                </option>
              ))}
            </select>
          </label>

          <label className="configurator-field">
            <span>Besoin de stockage estimé</span>
            <select
              value={String(configuration.storageGb ?? "")}
              onChange={(event) => {
                const value = event.target.value;
                setConfiguration((current) => ({
                  ...current,
                  storageGb: value ? Number(value) : null,
                }));
              }}
            >
              <option value="">Je ne sais pas</option>
              <option value="8">Moins de 10 Go</option>
              <option value="32">32 Go</option>
              <option value="64">64 Go</option>
            </select>
          </label>

          {isVpnIncluded ? (
            <div className="configurator-field">
              <span>VPN</span>
              <p className="configurator-static-value">Inclus</p>
            </div>
          ) : (
            <NullableBooleanSelect
              label="VPN"
              value={configuration.needsVpn}
              onChange={(needsVpn) =>
                setConfiguration((current) => ({ ...current, needsVpn }))}
            />
          )}

          {isWindowsDesktopIncluded ? (
            <div className="configurator-field">
              <span>Bureau Windows distant</span>
              <p className="configurator-static-value">Inclus</p>
            </div>
          ) : (
            <NullableBooleanSelect
              label="Bureau Windows distant"
              value={configuration.needsWindowsDesktop}
              onChange={(needsWindowsDesktop) =>
                setConfiguration((current) => ({
                  ...current,
                  needsWindowsDesktop,
                }))}
            />
          )}

          <label className="configurator-field">
            <span>Engagement</span>
            <select
              value={String(configuration.commitmentMonths)}
              onChange={(event) => {
                const commitmentMonths = Number(
                  event.target.value,
                ) as PublicPackCommitmentMonths;
                setConfiguration((current) => ({
                  ...current,
                  commitmentMonths,
                  paymentMode: updateConfigurationPaymentMode(
                    commitmentMonths,
                    current.paymentMode,
                  ),
                }));
              }}
            >
              <option value="1">1 mois</option>
              <option value="6">6 mois</option>
              <option value="12">12 mois</option>
            </select>
          </label>

          {configuration.commitmentMonths === 1 ? (
            <div className="configurator-field">
              <span>Paiement</span>
              <p className="configurator-static-value">Mensuel</p>
            </div>
          ) : (
            <label className="configurator-field">
              <span>Paiement</span>
              <select
                value={configuration.paymentMode}
                onChange={(event) =>
                  setConfiguration((current) => ({
                    ...current,
                    paymentMode: event.target.value as "monthly" | "upfront",
                  }))}
              >
                <option value="monthly">Mensuel</option>
                <option value="upfront">Comptant</option>
              </select>
            </label>
          )}
        </div>
      </section>

      <aside className="configurator-summary" aria-live="polite">
        <div className="configurator-section-heading">
          <h2>Votre estimation</h2>
          <p>
            {isLoading
              ? "Mise à jour en cours..."
              : "Tarif correspondant à votre configuration actuelle."}
          </p>
        </div>

        {error ? (
          <FormMessage title="Simulation indisponible" tone="error">
            <p>{error}</p>
          </FormMessage>
        ) : null}

        {resolution?.status === "requires_different_offer"
        && resolution.suggestedPackKey ? (
          <FormMessage title="Autre pack recommande" tone="info">
            <p>
              Cette configuration correspond mieux a{" "}
              <strong>{resolvedPack?.label ?? "un autre pack"}</strong>.
            </p>
            <button
              className="button button-secondary"
              onClick={() =>
                setConfiguration((current) => ({
                  ...current,
                  packKey: resolution.suggestedPackKey as PublicPackCode,
                }))}
              type="button"
            >
              Utiliser ce pack
            </button>
          </FormMessage>
        ) : null}

        {resolution?.status === "requires_quote" ? (
          <FormMessage title="Cadrage necessaire" tone="info">
            <p>
              Le besoin sort des variantes standards proposées en ligne. Un
              échange est nécessaire avant inscription.
            </p>
            <Link className="button button-secondary" href="/contact">
              Demander un cadrage
            </Link>
          </FormMessage>
        ) : null}

        {simulation ? (
          <div className="configurator-price-box">
            <h3>{resolvedPack?.label ?? selectedPack?.label}</h3>
            <dl>
              <div>
                <dt>Abonnement</dt>
                <dd>
                  {shouldShowVatBreakdown(simulation.fiscalRegime) ? (
                    <>
                      <strong>
                        {formatCurrencyFromCents(simulation.monthlyPriceIncVatCents)}
                      </strong>{" "}
                      TTC / mois
                      <span>
                        {formatCurrencyFromCents(simulation.monthlyPriceExVatCents)}
                        {" "}HT / mois
                      </span>
                    </>
                  ) : (
                    <>
                      <strong>
                        {formatCommercialAmountFromCents(
                          simulation.monthlyPriceIncVatCents,
                          {
                            fiscalRegime: simulation.fiscalRegime,
                            suffix: " / mois",
                          },
                        )}
                      </strong>
                      <span>
                        {formatFiscalMention(
                          simulation.fiscalRegime,
                          simulation.fiscalMention,
                        )}
                      </span>
                    </>
                  )}
                </dd>
              </div>
              <div>
                <dt>Mise en service</dt>
                <dd>
                  {shouldShowVatBreakdown(simulation.fiscalRegime) ? (
                    <>
                      <strong>
                        {formatCurrencyFromCents(simulation.setupPriceIncVatCents)}
                      </strong>{" "}
                      TTC
                      <span>
                        {formatCurrencyFromCents(simulation.setupPriceExVatCents)}
                        {" "}HT
                      </span>
                    </>
                  ) : (
                    <>
                      <strong>
                        {formatCommercialAmountFromCents(
                          simulation.setupPriceIncVatCents,
                          { fiscalRegime: simulation.fiscalRegime },
                        )}
                      </strong>
                      <span>
                        {formatFiscalMention(
                          simulation.fiscalRegime,
                          simulation.fiscalMention,
                        )}
                      </span>
                    </>
                  )}
                </dd>
              </div>
              <div>
                <dt>Total initial estimé</dt>
                <dd>
                  {shouldShowVatBreakdown(simulation.fiscalRegime) ? (
                    <>
                      <strong>
                        {formatCurrencyFromCents(simulation.firstChargeIncVatCents)}
                      </strong>{" "}
                      TTC
                      <span>
                        abonnement + mise en service,{" "}
                        {formatCurrencyFromCents(simulation.firstChargeExVatCents)}
                        {" "}HT
                      </span>
                    </>
                  ) : (
                    <>
                      <strong>
                        {formatCommercialAmountFromCents(
                          simulation.firstChargeIncVatCents,
                          { fiscalRegime: simulation.fiscalRegime },
                        )}
                      </strong>
                      <span>
                        abonnement + mise en service ·{" "}
                        {formatFiscalMention(
                          simulation.fiscalRegime,
                          simulation.fiscalMention,
                        )}
                      </span>
                    </>
                  )}
                </dd>
              </div>
            </dl>
          </div>
        ) : null}

        {resolution?.warnings.length ? (
          <ul className="configurator-warnings">
            {resolution.warnings.map((warning) => (
              <li key={warning}>
                {WARNING_LABELS[warning as CatalogConfigurationWarningCode]
                  ?? warning}
              </li>
            ))}
          </ul>
        ) : null}

        {signupHref ? (
          <Link className="button" href={signupHref}>
            Continuer avec cette configuration
          </Link>
        ) : null}
      </aside>
    </div>
  );
}

function applyIncludedCapabilities(
  configuration: CatalogConfigurationInput,
  pack: ResolvedPublicPackManifest | undefined,
): CatalogConfigurationInput {
  return {
    ...configuration,
    needsVpn: pack?.capabilities.supportsVpn ? true : configuration.needsVpn,
    needsWindowsDesktop: pack?.capabilities.supportsWindowsDesktop
      ? true
      : configuration.needsWindowsDesktop,
  };
}

function NullableBooleanSelect({
  label,
  value,
  onChange,
}: {
  label: string;
  value: boolean | null;
  onChange: (value: boolean | null) => void;
}) {
  return (
    <label className="configurator-field">
      <span>{label}</span>
      <select
        value={value === null ? "" : value ? "yes" : "no"}
        onChange={(event) => {
          const next = event.target.value;
          onChange(next === "" ? null : next === "yes");
        }}
      >
        <option value="">Je ne sais pas</option>
        <option value="yes">Oui</option>
        <option value="no">Non</option>
      </select>
    </label>
  );
}
