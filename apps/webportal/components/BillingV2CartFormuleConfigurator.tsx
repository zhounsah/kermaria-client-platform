"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type {
  BillingV2Cart,
  BillingV2CartCommandRequest,
  BillingV2CartCommandResponse,
  BillingV2CartQuote,
  BillingV2PublicCatalog,
  BillingV2PublicPreset,
} from "@kermaria/shared";

import { formatCurrencyFromCents } from "@/lib/formatters";
import { findService, resolveServicePublicLabel } from "@/lib/billing-v2-formules";
import { requestBffJson } from "@/lib/client-api";

type Props = { catalog: BillingV2PublicCatalog; preset: BillingV2PublicPreset };

/**
 * Configurateur Cart générique. Il ne reconstruit aucune règle commerciale :
 * chaque mutation est adressée au BFF avec expectedVersion, et toutes les
 * disponibilités/prix/readiness sont ceux du CartQuote serveur.
 */
export function BillingV2CartFormuleConfigurator({ catalog, preset }: Props) {
  const [cart, setCart] = useState<BillingV2Cart | null>(null);
  const [quote, setQuote] = useState<BillingV2CartQuote | null>(null);
  const [optionTiers, setOptionTiers] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  // React Strict Mode rejoue les Effects de montage en développement. Cette
  // garde de génération couvre aussi une navigation qui réutiliserait le même
  // composant avec un autre preset, sans demander au backend d'être séquentiel.
  const bootstrapKeyRef = useRef<string | null>(null);
  const bootstrapGenerationRef = useRef(0);

  const command = useCallback(async (request: BillingV2CartCommandRequest) => {
    const response = await requestBffJson<BillingV2CartCommandResponse>("/api/billing-v2/cart", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
    if (!response.ok) throw new Error(response.error.code);
    // La classification result-code -> HTTP est uniquement authoritative
    // dans API-INTERNAL. Un HTTP 2xx est donc un resultat Cart reussi ; le
    // configurateur n'a aucune liste locale de codes de succes a maintenir.
    return response.data;
  }, []);

  const refreshQuote = useCallback(async (cartId: string) => {
    return command({ command: "quote", cartId });
  }, [command]);

  useEffect(() => {
    const bootstrapKey = `${preset.code}:${catalog.currency}`;
    if (bootstrapKeyRef.current === bootstrapKey) return;
    bootstrapKeyRef.current = bootstrapKey;
    const generation = ++bootstrapGenerationRef.current;
    const isCurrentBootstrap = () => bootstrapGenerationRef.current === generation
      && bootstrapKeyRef.current === bootstrapKey;
    setLoading(true);
    setMessage(null);
    setQuote(null);
    (async () => {
      try {
        const current = await command({ command: "current", currency: catalog.currency });
        if (!current.cart || !isCurrentBootstrap()) return;
        setCart(current.cart);
        const initialized = await command({
          command: "initialize_preset", cartId: current.cart.id,
          currency: catalog.currency, presetCode: preset.code,
          expectedVersion: current.cart.version,
        });
        if (initialized.cart && isCurrentBootstrap()) {
          let configured = initialized.cart;
          setCart(configured);
          const defaultCommitment = catalog.commitments[0];
          if (!configured.commitmentCode && defaultCommitment) {
            const commitment = await command({ command: "set_commitment", cartId: configured.id,
              expectedVersion: configured.version, commitmentCode: defaultCommitment.code });
            configured = commitment.cart ?? configured;
            if (!isCurrentBootstrap()) return;
            setCart(configured);
          }
          if (!configured.paymentMode) {
            const payment = await command({ command: "set_payment_mode", cartId: configured.id,
              expectedVersion: configured.version, paymentMode: "monthly" });
            configured = payment.cart ?? configured;
            if (!isCurrentBootstrap()) return;
            setCart(configured);
          }
          const quoted = await refreshQuote(configured.id);
          if (quoted.quote && isCurrentBootstrap()) setQuote(quoted.quote);
        }
      } catch (error) {
        if (isCurrentBootstrap()) setMessage(error instanceof Error && error.message === "CART_PRESET_CONFLICT"
          ? "Un autre panier est déjà en cours. Reprenez-le ou remplacez-le explicitement."
          : "La configuration du panier est momentanément indisponible.");
      } finally {
        if (isCurrentBootstrap()) setLoading(false);
      }
    })();
  }, [catalog.commitments, catalog.currency, command, preset.code, refreshQuote]);

  async function mutate(request: BillingV2CartCommandRequest) {
    if (!cart) return;
    setLoading(true);
    setMessage(null);
    try {
      const result = await command({ ...request, cartId: cart.id, expectedVersion: cart.version });
      if (result.cart) {
        setCart(result.cart);
        const quoted = await refreshQuote(result.cart.id);
        if (quoted.quote) setQuote(quoted.quote);
      }
    } catch (error) {
      setMessage(error instanceof Error && error.message === "CART_VERSION_CONFLICT"
        ? "La configuration a changé dans un autre onglet. Rechargez la page."
        : "Cette modification n'est pas disponible pour cette formule.");
    } finally { setLoading(false); }
  }

  if (loading && !cart) return <p className="formules-empty">Préparation de votre configuration…</p>;
  if (!cart) return <p className="formules-empty">{message ?? "Configuration indisponible."}</p>;

  return <section className="formule-configurator" aria-busy={loading}>
    <h2>Votre configuration</h2>
    {message ? <p role="alert" className="formule-error">{message}</p> : null}
    <div className="formule-config-grid">
      <div>
        {cart.items.map((item) => {
          const service = findService(catalog, item.serviceCode);
          const editable = item.customerEditable !== false;
          const tiers = service?.tiers.filter((tier) => tier.publicSelectable) ?? [];
          const definition = cart.presetDefinition?.find((entry) => entry.presetItemId === item.sourcePresetItemId);
          const quantityEditable = editable && definition !== undefined
            && definition.maximumQuantity > definition.minimumQuantity;
          return <article className="formule-option" key={item.id}>
            <h3>{resolveServicePublicLabel(item.serviceCode, service?.name ?? item.serviceCode)}</h3>
            {item.tierCode && tiers.length > 0 ? <label>
              {service?.tierSelectorLabel?.trim() || "Option"}
              <select disabled={!editable || loading} value={item.tierCode}
                onChange={(event) => void mutate({ command: "update_item", itemId: item.id, item: {
                  serviceCode: item.serviceCode, tierCode: event.target.value, quantity: item.quantity,
                  scopeTemplate: item.scopeTemplate, subjectBinding: item.subjectBinding,
                  sourcePresetId: cart.sourcePresetId, sourcePresetItemId: item.sourcePresetItemId,
                  configurationKind: item.configurationKind, configurationReference: item.configurationReference, origin: "preset",
                } })}>
                {tiers.map((tier) => <option key={tier.code} value={tier.code}>{tier.label}</option>)}
              </select>
            </label> : null}
            {definition && quantityEditable ? <label>
              Quantité
              <input disabled={loading} max={definition.maximumQuantity} min={definition.minimumQuantity}
                onChange={(event) => void mutate({ command: "update_item", itemId: item.id, item: {
                  serviceCode: item.serviceCode, tierCode: item.tierCode, quantity: Number(event.target.value),
                  scopeTemplate: item.scopeTemplate, subjectBinding: item.subjectBinding,
                  sourcePresetId: cart.sourcePresetId, sourcePresetItemId: item.sourcePresetItemId,
                  configurationKind: item.configurationKind, configurationReference: item.configurationReference, origin: "preset",
                } })} type="number" value={item.quantity} />
            </label> : null}
            {editable && item.requiredItem !== true && !quantityEditable ? <label>
              Quantité
              <input type="number" min={1} max={10000} value={item.quantity} disabled={loading}
                onChange={(event) => void mutate({ command: "update_item", itemId: item.id, item: {
                  serviceCode: item.serviceCode, tierCode: item.tierCode, quantity: Math.max(1, Number(event.target.value) || 1),
                  scopeTemplate: item.scopeTemplate, subjectBinding: item.subjectBinding,
                  sourcePresetId: cart.sourcePresetId, sourcePresetItemId: item.sourcePresetItemId,
                  configurationKind: item.configurationKind, configurationReference: item.configurationReference, origin: "preset",
                } })} />
            </label> : null}
            {item.requiredItem !== true ? <button type="button" disabled={loading}
              onClick={() => void mutate({ command: "remove_item", itemId: item.id })}>
              Retirer cette option
            </button> : null}
          </article>;
        })}
        {Object.entries((cart.presetDefinition ?? [])
          .filter((definition) => !cart.items.some((item) => item.sourcePresetItemId === definition.presetItemId
            || (item.serviceCode === definition.serviceCode && item.scopeTemplate === definition.scopeTemplate)))
          .reduce<Record<string, NonNullable<typeof cart.presetDefinition>>>((groups, definition) => {
            const key = `${definition.serviceCode}:${definition.scopeTemplate}`;
            (groups[key] ??= []).push(definition);
            return groups;
          }, {}))
          .map(([optionKey, definitions]) => {
            const service = findService(catalog, definitions[0].serviceCode);
            const tiered = definitions.filter((definition) => definition.tierCode !== null);
            const selected = tiered.length === 0
              ? definitions[0]
              : definitions.find((definition) => definition.presetItemId === optionTiers[optionKey]);
            const selectorLabel = service?.tierSelectorLabel?.trim() || "Option";
            return <article className="formule-option" key={optionKey}>
              <h3>{resolveServicePublicLabel(definitions[0].serviceCode, service?.name ?? definitions[0].serviceCode)}</h3>
              {tiered.length > 0 ? <label>{selectorLabel}
                <select value={optionTiers[optionKey] ?? ""} disabled={loading}
                  onChange={(event) => setOptionTiers((current) => ({ ...current, [optionKey]: event.target.value }))}>
                  <option value="">Choisir une option</option>
                  {tiered.map((definition) => <option key={definition.presetItemId} value={definition.presetItemId}>
                    {service?.tiers.find((tier) => tier.code === definition.tierCode)?.label ?? "Option disponible"}
                  </option>)}
                </select>
              </label> : null}
              <button type="button" disabled={loading || !selected?.customerEditable}
                onClick={() => selected && void mutate({ command: "add_preset_item", presetItemId: selected.presetItemId })}>
                Ajouter cette option
              </button>
            </article>;
          })}
      </div>
      <aside className="formule-summary">
        <h2>Récapitulatif</h2>
        <label>Engagement
          <select disabled={loading} value={cart.commitmentCode ?? ""}
            onChange={(event) => void mutate({ command: "set_commitment", commitmentCode: event.target.value || null })}>
            {catalog.commitments.map((commitment) => <option key={commitment.code} value={commitment.code}>{commitment.name}</option>)}
          </select>
        </label>
        <label>Mode de paiement
          <select disabled={loading} value={cart.paymentMode ?? "monthly"}
            onChange={(event) => void mutate({ command: "set_payment_mode", paymentMode: event.target.value as "monthly" | "upfront" })}>
            <option value="monthly">Mensuel</option><option value="upfront">En une fois</option>
          </select>
        </label>
        <p>Mensuel : <strong>{quote ? formatCurrencyFromCents(quote.recurringTotalCents) : "…"}</strong></p>
        <p>Dû maintenant : <strong>{quote ? formatCurrencyFromCents(quote.totalDueNowCents) : "…"}</strong></p>
        {quote?.dependencyIssues.concat(quote.scopeIssues, quote.configurationIssues).map((issue) =>
          <p key={`${issue.code}-${issue.cartItemId}`} className={issue.blocking ? "formule-error" : "formule-note"}>{issue.message}</p>)}
        <p className="formule-summary-note">
          Ce composant est réservé à l&apos;édition future du panier. Il ne lance
          ni checkout, ni paiement, ni souscription.
        </p>
      </aside>
    </div>
  </section>;
}
