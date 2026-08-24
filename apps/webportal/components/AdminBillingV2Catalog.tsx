"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { StatusBadge } from "@/components/StatusBadge";
import { requestBffJson } from "@/lib/client-api";
import { formatCurrencyFromCents } from "@/lib/formatters";
import type {
  BillingV2AdminCatalogProviderCoverage,
  BillingV2AdminCatalogSnapshot,
  BillingV2AdminCommitment,
  BillingV2AdminPreset,
  BillingV2AdminPrice,
  BillingV2AdminService,
  BillingV2AdminTier,
} from "@/lib/internal-api";

/**
 * Administration du catalogue Billing V2/V2.1.
 *
 * Ecran unique de la conception commerciale : services, paliers, versions de
 * prix, formules, engagements et rattachements provider. L'exploitation
 * courante — readiness, abonnements, paiements, provisioning, reconciliation —
 * reste sur `/admin/billing-v2` : ce sont deux metiers distincts et les
 * melanger rendrait l'un et l'autre illisibles.
 *
 * Rien n'est calcule ici. Une revision tarifaire envoie un montant et une date
 * d'effet ; c'est API-INTERNAL qui ferme l'ancienne fenetre, ouvre la version
 * N+1 dans la meme transaction et refuse un recouvrement. Le navigateur ne
 * decide jamais quel prix fait autorite.
 */
type Props = {
  snapshot: BillingV2AdminCatalogSnapshot;
  providers: BillingV2AdminCatalogProviderCoverage[];
};

type Section =
  | "services"
  | "prices"
  | "presets"
  | "commitments"
  | "providers";

type Feedback = { tone: "success" | "error"; message: string } | null;

const SECTIONS: Array<{ key: Section; label: string }> = [
  { key: "services", label: "Services et paliers" },
  { key: "prices", label: "Tarifs" },
  { key: "presets", label: "Formules" },
  { key: "commitments", label: "Engagements et remises" },
  { key: "providers", label: "Providers" },
];

export function AdminBillingV2Catalog({ snapshot, providers }: Props) {
  const router = useRouter();
  const [section, setSection] = useState<Section>("services");
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [busy, setBusy] = useState(false);

  const services = useMemo(
    () =>
      [...snapshot.services].sort(
        (left, right) =>
          left.displayOrder - right.displayOrder
          || left.code.localeCompare(right.code),
      ),
    [snapshot.services],
  );

  async function send(command: Record<string, unknown>) {
    if (busy || !snapshot.editable) {
      return;
    }

    setBusy(true);
    setFeedback(null);

    const result = await requestBffJson<{ code: string; message: string }>(
      "/api/admin/billing-v2/catalog",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(command),
      },
    );

    if (result.ok) {
      setFeedback({ tone: "success", message: result.data.message });
      router.refresh();
    } else {
      setFeedback({ tone: "error", message: result.error.message });
    }

    setBusy(false);
  }

  if (!snapshot.editable) {
    return (
      <section className="content-panel">
        <h2>Catalogue non administrable</h2>
        <p>
          Le catalogue Billing V2 se lit et s&apos;administre exclusivement en
          base. La persistance n&apos;est pas disponible sur cet environnement :
          aucune donnée n&apos;est affichée et aucune modification n&apos;est
          acceptée. Une administration en mémoire donnerait l&apos;illusion
          d&apos;un tarif enregistré.
        </p>
      </section>
    );
  }

  return (
    <div className="admin-catalog-v2">
      <nav aria-label="Sections du catalogue" className="filter-links">
        {SECTIONS.map((entry) => (
          <button
            aria-current={section === entry.key ? "page" : undefined}
            key={entry.key}
            onClick={() => setSection(entry.key)}
            type="button"
          >
            {entry.label}
          </button>
        ))}
      </nav>

      {feedback ? (
        <FormMessage
          title={feedback.tone === "success" ? "Modification enregistrée" : "Modification refusée"}
          tone={feedback.tone}
        >
          {feedback.message}
        </FormMessage>
      ) : null}

      {section === "services" ? (
        <ServicesSection busy={busy} onCommand={send} services={services} />
      ) : null}
      {section === "prices" ? (
        <PricesSection busy={busy} onCommand={send} services={services} />
      ) : null}
      {section === "presets" ? (
        <PresetsSection
          busy={busy}
          onCommand={send}
          presets={snapshot.presets}
          services={services}
        />
      ) : null}
      {section === "commitments" ? (
        <CommitmentsSection
          busy={busy}
          commitments={snapshot.commitments}
          onCommand={send}
        />
      ) : null}
      {section === "providers" ? (
        <ProvidersSection
          busy={busy}
          coverage={providers}
          onCommand={send}
          services={services}
        />
      ) : null}
    </div>
  );
}

type CommandSender = (command: Record<string, unknown>) => Promise<void>;

// ---------------------------------------------------------------------------
// Services et paliers
// ---------------------------------------------------------------------------

function ServicesSection({
  services,
  onCommand,
  busy,
}: {
  services: BillingV2AdminService[];
  onCommand: CommandSender;
  busy: boolean;
}) {
  return (
    <section className="content-panel">
      <h2>Services</h2>
      <p>
        Un service devient commandable seul lorsque « Libre-service » est actif.
        « Visible » ne suffit pas : un service peut être présenté sans être
        vendable isolément.
      </p>

      {services.map((service) => (
        <details className="admin-catalog-service" key={service.id}>
          <summary>
            <strong>{service.name}</strong>
            <span className="cell-secondary"> · {service.code}</span>
            <StatusBadge
              label={service.status === "active" ? "Actif" : "Inactif"}
              tone={service.status === "active" ? "success" : "neutral"}
            />
          </summary>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = new FormData(event.currentTarget);
              void onCommand({
                kind: "service.update",
                id: service.id,
                name: String(form.get("name") ?? ""),
                description: String(form.get("description") ?? ""),
                category: String(form.get("category") ?? ""),
                status: String(form.get("status") ?? ""),
                displayOrder: Number(form.get("displayOrder") ?? 0),
                publicVisible: form.get("publicVisible") === "on",
                selfServiceOrderable: form.get("selfServiceOrderable") === "on",
                discountEligible: form.get("discountEligible") === "on",
              });
            }}
          >
            <label>
              <span>Nom</span>
              <input defaultValue={service.name} maxLength={160} name="name" />
            </label>
            <label>
              <span>Description</span>
              <textarea
                defaultValue={service.description ?? ""}
                name="description"
                rows={2}
              />
            </label>
            <label>
              <span>Catégorie</span>
              <input
                defaultValue={service.category ?? ""}
                maxLength={80}
                name="category"
              />
            </label>
            <label>
              <span>Statut</span>
              <select defaultValue={service.status} name="status">
                <option value="active">Actif</option>
                <option value="inactive">Inactif</option>
              </select>
            </label>
            <label>
              <span>Ordre d&apos;affichage</span>
              <input
                defaultValue={service.displayOrder}
                min={0}
                name="displayOrder"
                type="number"
              />
            </label>
            <label className="admin-catalog-checkbox">
              <input
                defaultChecked={service.publicVisible}
                name="publicVisible"
                type="checkbox"
              />
              <span>Visible publiquement</span>
            </label>
            <label className="admin-catalog-checkbox">
              <input
                defaultChecked={service.selfServiceOrderable}
                name="selfServiceOrderable"
                type="checkbox"
              />
              <span>Commandable en libre-service</span>
            </label>
            <label className="admin-catalog-checkbox">
              <input
                defaultChecked={service.discountEligible}
                name="discountEligible"
                type="checkbox"
              />
              <span>Éligible aux remises d&apos;engagement</span>
            </label>
            <button className="button" disabled={busy} type="submit">
              Enregistrer le service
            </button>
          </form>

          {service.tiers.length > 0 ? (
            <div className="admin-catalog-tiers">
              <h3>Paliers</h3>
              {service.tiers.map((tier) => (
                <TierForm
                  busy={busy}
                  key={tier.id}
                  onCommand={onCommand}
                  tier={tier}
                />
              ))}
            </div>
          ) : null}
        </details>
      ))}
    </section>
  );
}

function TierForm({
  tier,
  onCommand,
  busy,
}: {
  tier: BillingV2AdminTier;
  onCommand: CommandSender;
  busy: boolean;
}) {
  return (
    <form
      className="admin-catalog-tier"
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        const attributes = String(form.get("attributes") ?? "")
          .split("\n")
          .map((line) => line.trim())
          .filter((line) => line.length > 0)
          .map((line) => {
            const [code, rest] = splitOnce(line, "=");
            const [rawValue, unit] = splitOnce(rest ?? "", "|");
            const numeric = Number(rawValue);
            const isNumeric = rawValue.trim().length > 0
              && Number.isFinite(numeric);
            return {
              attributeCode: code,
              valueNumeric: isNumeric ? numeric : undefined,
              valueText: isNumeric ? undefined : rawValue,
              unit,
            };
          });

        void onCommand({
          kind: "tier.update",
          id: tier.id,
          label: String(form.get("label") ?? ""),
          publicLabel: String(form.get("publicLabel") ?? ""),
          description: String(form.get("description") ?? ""),
          status: String(form.get("status") ?? ""),
          displayOrder: Number(form.get("displayOrder") ?? 0),
          publicSelectable: form.get("publicSelectable") === "on",
          numericValue: form.get("numericValue")
            ? Number(form.get("numericValue"))
            : undefined,
          unit: String(form.get("unit") ?? ""),
          attributes,
        });
      }}
    >
      <h4>
        {tier.name} <span className="cell-secondary">({tier.code})</span>
      </h4>
      <label>
        <span>Libellé</span>
        <input defaultValue={tier.name} maxLength={160} name="label" />
      </label>
      <label>
        <span>Libellé public</span>
        <input
          defaultValue={tier.publicLabel ?? ""}
          maxLength={160}
          name="publicLabel"
        />
      </label>
      <label>
        <span>Description</span>
        <textarea
          defaultValue={tier.description ?? ""}
          name="description"
          rows={2}
        />
      </label>
      <label>
        <span>Valeur</span>
        <input
          defaultValue={tier.numericValue ?? ""}
          name="numericValue"
          type="number"
        />
      </label>
      <label>
        <span>Unité</span>
        <input defaultValue={tier.unit ?? ""} maxLength={32} name="unit" />
      </label>
      <label>
        <span>Statut</span>
        <select defaultValue={tier.status} name="status">
          <option value="active">Actif</option>
          <option value="inactive">Inactif</option>
        </select>
      </label>
      <label>
        <span>Ordre</span>
        <input
          defaultValue={tier.displayOrder}
          min={0}
          name="displayOrder"
          type="number"
        />
      </label>
      <label className="admin-catalog-checkbox">
        <input
          defaultChecked={tier.publicSelectable}
          name="publicSelectable"
          type="checkbox"
        />
        <span>Sélectionnable publiquement</span>
      </label>
      <label>
        <span>
          Attributs commerciaux — une ligne par attribut,
          {" "}
          <code>code=valeur|unité</code> (vCPU, RAM, stockage…)
        </span>
        <textarea
          defaultValue={tier.attributes
            .map(
              (attribute) =>
                `${attribute.attributeCode}=${
                  attribute.valueNumeric ?? attribute.valueText ?? ""
                }|${attribute.unit ?? ""}`,
            )
            .join("\n")}
          name="attributes"
          rows={3}
        />
      </label>
      <button className="button button-secondary" disabled={busy} type="submit">
        Enregistrer le palier
      </button>
    </form>
  );
}

// ---------------------------------------------------------------------------
// Tarifs
// ---------------------------------------------------------------------------

function PricesSection({
  services,
  onCommand,
  busy,
}: {
  services: BillingV2AdminService[];
  onCommand: CommandSender;
  busy: boolean;
}) {
  const now = new Date();
  const [serviceId, setServiceId] = useState(services[0]?.id ?? "");
  const service = services.find((candidate) => candidate.id === serviceId)
    ?? services[0];

  return (
    <section className="content-panel">
      <h2>Tarifs</h2>
      <p>
        Un tarif ne se modifie pas : il se <strong>remplace</strong>. Publier une
        révision ferme la fenêtre courante à la date d&apos;effet et ouvre la
        version suivante dans la même transaction. Les versions passées restent
        lisibles — elles font autorité sur les factures qu&apos;elles ont
        produites.
      </p>

      <label>
        <span>Service</span>
        <select
          onChange={(event) => setServiceId(event.currentTarget.value)}
          value={service?.id ?? ""}
        >
          {services.map((candidate) => (
            <option key={candidate.id} value={candidate.id}>
              {candidate.name} ({candidate.code})
            </option>
          ))}
        </select>
      </label>

      {!service ? (
        <p>Aucun service.</p>
      ) : (
        <>
          <PriceGroup
            busy={busy}
            label="Tarif du service (sans palier)"
            onCommand={onCommand}
            now={now}
            prices={service.flatPrices}
            serviceId={service.id}
            tierId={null}
          />
          {service.tiers.map((tier) => (
            <PriceGroup
              busy={busy}
              key={tier.id}
              label={`Palier ${tier.name} (${tier.code})`}
              onCommand={onCommand}
              now={now}
              prices={tier.prices}
              serviceId={service.id}
              tierId={tier.id}
            />
          ))}
        </>
      )}
    </section>
  );
}

function PriceGroup({
  label,
  serviceId,
  tierId,
  prices,
  onCommand,
  busy,
  now,
}: {
  label: string;
  serviceId: string;
  tierId: string | null;
  prices: BillingV2AdminPrice[];
  onCommand: CommandSender;
  busy: boolean;
  now: Date;
}) {
  const ordered = [...prices].sort(
    (left, right) =>
      new Date(right.validFrom).getTime() - new Date(left.validFrom).getTime(),
  );

  return (
    <div className="admin-catalog-price-group">
      <h3>{label}</h3>

      {ordered.length === 0 ? (
        <p className="cell-secondary">Aucune version tarifaire.</p>
      ) : (
        <table className="admin-table">
          <caption>Historique des versions tarifaires</caption>
          <thead>
            <tr>
              <th scope="col">Version</th>
              <th scope="col">Montant</th>
              <th scope="col">Cadence</th>
              <th scope="col">Déclencheur</th>
              <th scope="col">Du</th>
              <th scope="col">Au</th>
              <th scope="col">État</th>
              <th scope="col">Action</th>
            </tr>
          </thead>
          <tbody>
            {ordered.map((price) => {
              const current = isCurrent(price, now);
              const scheduled = isScheduled(price, now);
              return (
                <tr key={price.id}>
                  <td>
                    v{price.priceVersion}
                    <div className="cell-secondary">{price.priceCode}</div>
                  </td>
                  <td>
                    <strong>{formatCurrencyFromCents(price.amountCents)}</strong>
                    <div className="cell-secondary">{price.currency}</div>
                  </td>
                  <td>
                    {price.billingCadence === "monthly"
                      ? "Mensuel"
                      : "Ponctuel"}
                  </td>
                  <td>
                    {price.chargeTrigger === "initial_subscription"
                      ? "Souscription"
                      : "Changement"}
                  </td>
                  <td>{formatDateTime(price.validFrom)}</td>
                  <td>{formatDateTime(price.validUntil)}</td>
                  <td>
                    <StatusBadge
                      label={
                        current
                          ? "En vigueur"
                          : scheduled
                            ? "Planifié"
                            : "Historique"
                      }
                      tone={
                        current ? "success" : scheduled ? "info" : "neutral"
                      }
                    />
                  </td>
                  <td>
                    {current || scheduled ? (
                      <button
                        className="table-action"
                        disabled={busy}
                        onClick={() =>
                          void onCommand({
                            kind: "price.close",
                            id: price.id,
                          })
                        }
                        type="button"
                      >
                        Retirer
                      </button>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          const form = new FormData(event.currentTarget);
          const euros = Number(form.get("amount") ?? 0);
          const effectiveAt = String(form.get("effectiveAt") ?? "").trim();
          void onCommand({
            kind: "price.publish",
            serviceId,
            tierId,
            // Saisie en euros, transmise en centimes : le calcul entier evite
            // l'arrondi flottant qui ferait deriver un tarif de un centime.
            amountCents: Math.round(euros * 100),
            currency: String(form.get("currency") ?? "EUR"),
            billingCadence: String(form.get("cadence") ?? "monthly"),
            chargeTrigger: String(form.get("trigger") ?? "initial_subscription"),
            taxRateBasisPoints: form.get("tax")
              ? Number(form.get("tax"))
              : undefined,
            effectiveAt: effectiveAt.length > 0
              ? new Date(effectiveAt).toISOString()
              : undefined,
          });
          event.currentTarget.reset();
        }}
      >
        <h4>Publier une nouvelle version</h4>
        <label>
          <span>Montant HT (€)</span>
          <input min={0} name="amount" required step="0.01" type="number" />
        </label>
        <label>
          <span>Devise</span>
          <input defaultValue="EUR" maxLength={3} name="currency" />
        </label>
        <label>
          <span>Cadence</span>
          <select defaultValue="monthly" name="cadence">
            <option value="monthly">Mensuel</option>
            <option value="one_time">Ponctuel</option>
          </select>
        </label>
        <label>
          <span>Déclencheur</span>
          <select defaultValue="initial_subscription" name="trigger">
            <option value="initial_subscription">Souscription initiale</option>
            <option value="subscription_change">Changement de config.</option>
          </select>
        </label>
        <label>
          <span>TVA (points de base, 2000 = 20 %)</span>
          <input max={10000} min={0} name="tax" type="number" />
        </label>
        <label>
          <span>Date d&apos;effet — vide = immédiat</span>
          <input name="effectiveAt" type="datetime-local" />
        </label>
        <button className="button" disabled={busy} type="submit">
          Publier la révision
        </button>
      </form>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Formules
// ---------------------------------------------------------------------------

function PresetsSection({
  presets,
  services,
  onCommand,
  busy,
}: {
  presets: BillingV2AdminPreset[];
  services: BillingV2AdminService[];
  onCommand: CommandSender;
  busy: boolean;
}) {
  return (
    <section className="content-panel">
      <h2>Formules</h2>
      <p>
        Une formule est un modèle de configuration, pas un prix. Son total
        n&apos;est jamais stocké : il est recalculé par le moteur tarifaire à
        partir des composants et des versions de prix en vigueur.
      </p>

      {presets.map((preset) => (
        <details className="admin-catalog-preset" key={preset.id}>
          <summary>
            <strong>{preset.name}</strong>
            <span className="cell-secondary"> · {preset.code}</span>
            <StatusBadge
              label={preset.isPublic ? "Publique" : "Interne"}
              tone={preset.isPublic ? "success" : "neutral"}
            />
          </summary>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = new FormData(event.currentTarget);
              void onCommand({
                kind: "preset.update",
                id: preset.id,
                name: String(form.get("name") ?? ""),
                description: String(form.get("description") ?? ""),
                status: String(form.get("status") ?? ""),
                isPublic: form.get("isPublic") === "on",
                displayOrder: Number(form.get("displayOrder") ?? 0),
              });
            }}
          >
            <label>
              <span>Nom</span>
              <input defaultValue={preset.name} maxLength={160} name="name" />
            </label>
            <label>
              <span>Description</span>
              <textarea
                defaultValue={preset.description ?? ""}
                name="description"
                rows={2}
              />
            </label>
            <label>
              <span>Statut</span>
              <select defaultValue={preset.status} name="status">
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
              </select>
            </label>
            <label>
              <span>Ordre</span>
              <input
                defaultValue={preset.displayOrder}
                min={0}
                name="displayOrder"
                type="number"
              />
            </label>
            <label className="admin-catalog-checkbox">
              <input
                defaultChecked={preset.isPublic}
                name="isPublic"
                type="checkbox"
              />
              <span>Visible sur la vitrine</span>
            </label>
            <button className="button" disabled={busy} type="submit">
              Enregistrer la formule
            </button>
          </form>

          <h4>Composants</h4>
          <ul className="admin-catalog-preset-items">
            {preset.items.map((item) => (
              <li key={item.id}>
                <span>
                  {item.serviceCode}
                  {item.tierCode ? ` · ${item.tierCode}` : ""} — {item.scopeTemplate}
                  {item.quantity > 1 ? ` × ${item.quantity}` : ""}
                  {item.requiredItem ? " (obligatoire)" : ""}
                </span>
                <button
                  className="table-action"
                  disabled={busy}
                  onClick={() =>
                    void onCommand({
                      kind: "preset.item.remove",
                      presetId: preset.id,
                      itemId: item.id,
                    })
                  }
                  type="button"
                >
                  Retirer
                </button>
              </li>
            ))}
          </ul>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = new FormData(event.currentTarget);
              const tierId = String(form.get("tierId") ?? "");
              void onCommand({
                kind: "preset.item.add",
                presetId: preset.id,
                serviceId: String(form.get("serviceId") ?? ""),
                tierId: tierId.length > 0 ? tierId : null,
                scopeTemplate: String(form.get("scope") ?? "subscription"),
                quantity: Number(form.get("quantity") ?? 1),
                requiredItem: form.get("required") === "on",
                customerEditable: form.get("editable") === "on",
                displayOrder: Number(form.get("order") ?? 0),
              });
            }}
          >
            <h5>Ajouter un composant</h5>
            <label>
              <span>Service</span>
              <select name="serviceId">
                {services.map((service) => (
                  <option key={service.id} value={service.id}>
                    {service.name} ({service.code})
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Palier — vide pour un service sans palier</span>
              <select name="tierId">
                <option value="">Aucun</option>
                {services.flatMap((service) =>
                  service.tiers.map((tier) => (
                    <option key={tier.id} value={tier.id}>
                      {service.code} · {tier.code}
                    </option>
                  )),
                )}
              </select>
            </label>
            <label>
              <span>Portée</span>
              <select defaultValue="subscription" name="scope">
                <option value="subscription">Abonnement</option>
                <option value="primary_user">Utilisateur principal</option>
                <option value="additional_user">Utilisateur supplémentaire</option>
              </select>
            </label>
            <label>
              <span>Quantité</span>
              <input defaultValue={1} min={1} name="quantity" type="number" />
            </label>
            <label>
              <span>Ordre</span>
              <input defaultValue={0} min={0} name="order" type="number" />
            </label>
            <label className="admin-catalog-checkbox">
              <input name="required" type="checkbox" />
              <span>Obligatoire</span>
            </label>
            <label className="admin-catalog-checkbox">
              <input defaultChecked name="editable" type="checkbox" />
              <span>Modifiable par le client</span>
            </label>
            <button
              className="button button-secondary"
              disabled={busy}
              type="submit"
            >
              Ajouter
            </button>
          </form>
        </details>
      ))}

      <form
        className="admin-catalog-create"
        onSubmit={(event) => {
          event.preventDefault();
          const form = new FormData(event.currentTarget);
          void onCommand({
            kind: "preset.create",
            code: String(form.get("code") ?? ""),
            name: String(form.get("name") ?? ""),
            description: String(form.get("description") ?? ""),
            isPublic: form.get("isPublic") === "on",
            displayOrder: Number(form.get("order") ?? 0),
          });
          event.currentTarget.reset();
        }}
      >
        <h3>Créer une formule</h3>
        <p className="cell-secondary">
          Le code est l&apos;identité publique de la formule : il apparaît dans
          l&apos;URL <code>/formules/&#123;code&#125;</code> et dans les
          souscriptions enregistrées. Il n&apos;est pas modifiable ensuite.
        </p>
        <label>
          <span>Code</span>
          <input maxLength={96} name="code" required />
        </label>
        <label>
          <span>Nom</span>
          <input maxLength={160} name="name" required />
        </label>
        <label>
          <span>Description</span>
          <textarea name="description" rows={2} />
        </label>
        <label>
          <span>Ordre</span>
          <input defaultValue={0} min={0} name="order" type="number" />
        </label>
        <label className="admin-catalog-checkbox">
          <input name="isPublic" type="checkbox" />
          <span>Visible sur la vitrine</span>
        </label>
        <button className="button" disabled={busy} type="submit">
          Créer
        </button>
      </form>
    </section>
  );
}

// ---------------------------------------------------------------------------
// Engagements et remises
// ---------------------------------------------------------------------------

function CommitmentsSection({
  commitments,
  onCommand,
  busy,
}: {
  commitments: BillingV2AdminCommitment[];
  onCommand: CommandSender;
  busy: boolean;
}) {
  return (
    <section className="content-panel">
      <h2>Engagements et remises</h2>
      <p>
        Les remises s&apos;expriment en points de base : 1000 = 10 %. Elles ne
        s&apos;appliquent qu&apos;aux lignes mensuelles marquées éligibles ; une
        prestation ponctuelle n&apos;est jamais remisée.
      </p>

      {commitments.map((commitment) => (
        <details className="admin-catalog-commitment" key={commitment.id}>
          <summary>
            <strong>{commitment.name}</strong>
            <span className="cell-secondary">
              {" "}
              · {commitment.code} · {commitment.commitmentMonths} mois
            </span>
          </summary>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = new FormData(event.currentTarget);
              void onCommand({
                kind: "commitment.update",
                id: commitment.id,
                name: String(form.get("name") ?? ""),
                commitmentMonths: Number(form.get("months") ?? 1),
                discountBasisPoints: Number(form.get("discount") ?? 0),
                allowMonthlyPayment: form.get("monthly") === "on",
                allowUpfrontPayment: form.get("upfront") === "on",
                status: String(form.get("status") ?? ""),
                displayOrder: Number(form.get("order") ?? 0),
              });
            }}
          >
            <label>
              <span>Nom</span>
              <input
                defaultValue={commitment.name}
                maxLength={160}
                name="name"
              />
            </label>
            <label>
              <span>Durée (mois)</span>
              <input
                defaultValue={commitment.commitmentMonths}
                max={120}
                min={1}
                name="months"
                type="number"
              />
            </label>
            <label>
              <span>Remise par défaut (points de base)</span>
              <input
                defaultValue={commitment.discountBasisPoints ?? 0}
                max={10000}
                min={0}
                name="discount"
                type="number"
              />
            </label>
            <label>
              <span>Statut</span>
              <select defaultValue={commitment.status} name="status">
                <option value="active">Actif</option>
                <option value="inactive">Inactif</option>
              </select>
            </label>
            <label>
              <span>Ordre</span>
              <input
                defaultValue={commitment.displayOrder}
                min={0}
                name="order"
                type="number"
              />
            </label>
            <label className="admin-catalog-checkbox">
              <input
                defaultChecked={commitment.allowMonthlyPayment}
                name="monthly"
                type="checkbox"
              />
              <span>Règlement mensuel autorisé</span>
            </label>
            <label className="admin-catalog-checkbox">
              <input
                defaultChecked={commitment.allowUpfrontPayment}
                name="upfront"
                type="checkbox"
              />
              <span>Règlement comptant autorisé</span>
            </label>
            <button className="button" disabled={busy} type="submit">
              Enregistrer l&apos;engagement
            </button>
          </form>

          <h4>Remises par mode de règlement</h4>
          <ul>
            {commitment.paymentOptions.map((option) => (
              <li key={option.id}>
                {option.paymentMode === "monthly" ? "Mensuel" : "Comptant"} —{" "}
                {option.discountBasisPoints / 100} % ({option.status})
              </li>
            ))}
          </ul>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = new FormData(event.currentTarget);
              void onCommand({
                kind: "commitment.payment_option",
                id: commitment.id,
                paymentMode: String(form.get("mode") ?? "monthly"),
                discountBasisPoints: Number(form.get("discount") ?? 0),
                status: String(form.get("status") ?? "active"),
              });
            }}
          >
            <label>
              <span>Mode</span>
              <select defaultValue="monthly" name="mode">
                <option value="monthly">Mensuel</option>
                <option value="upfront">Comptant</option>
              </select>
            </label>
            <label>
              <span>Remise (points de base)</span>
              <input max={10000} min={0} name="discount" type="number" />
            </label>
            <label>
              <span>Statut</span>
              <select defaultValue="active" name="status">
                <option value="active">Actif</option>
                <option value="inactive">Inactif</option>
              </select>
            </label>
            <button
              className="button button-secondary"
              disabled={busy}
              type="submit"
            >
              Enregistrer la remise
            </button>
          </form>
        </details>
      ))}

      <form
        className="admin-catalog-create"
        onSubmit={(event) => {
          event.preventDefault();
          const form = new FormData(event.currentTarget);
          void onCommand({
            kind: "commitment.create",
            code: String(form.get("code") ?? ""),
            name: String(form.get("name") ?? ""),
            commitmentMonths: Number(form.get("months") ?? 1),
            discountBasisPoints: Number(form.get("discount") ?? 0),
          });
          event.currentTarget.reset();
        }}
      >
        <h3>Créer un engagement</h3>
        <label>
          <span>Code</span>
          <input maxLength={64} name="code" required />
        </label>
        <label>
          <span>Nom</span>
          <input maxLength={160} name="name" required />
        </label>
        <label>
          <span>Durée (mois)</span>
          <input
            defaultValue={12}
            max={120}
            min={1}
            name="months"
            required
            type="number"
          />
        </label>
        <label>
          <span>Remise (points de base)</span>
          <input defaultValue={0} max={10000} min={0} name="discount" type="number" />
        </label>
        <button className="button" disabled={busy} type="submit">
          Créer
        </button>
      </form>
    </section>
  );
}

// ---------------------------------------------------------------------------
// Providers
// ---------------------------------------------------------------------------

/**
 * Matrice fournisseur / environnement, alignee sur
 * `CATALOG_PROVIDER_ENVIRONMENTS` cote BFF et sur `ProviderEnvironments` cote
 * API interne. Les trois doivent rester d'accord : le back-office ne doit pas
 * proposer un couple que le serveur refusera.
 */
const PROVIDER_ENVIRONMENTS: Readonly<Record<string, readonly string[]>> = {
  stripe: ["test", "live"],
  paypal: ["sandbox", "live"],
};

const ENVIRONMENT_LABELS: Readonly<Record<string, string>> = {
  test: "Test",
  live: "Live",
  sandbox: "Sandbox",
};

function ProvidersSection({
  coverage,
  services,
  onCommand,
  busy,
}: {
  coverage: BillingV2AdminCatalogProviderCoverage[];
  services: BillingV2AdminService[];
  onCommand: CommandSender;
  busy: boolean;
}) {
  const [mappingProvider, setMappingProvider] = useState("stripe");
  const now = new Date();
  const currentPrices = services.flatMap((service) => [
    ...service.flatPrices.map((price) => ({ service, price })),
    ...service.tiers.flatMap((tier) =>
      tier.prices.map((price) => ({ service, price })),
    ),
  ]).filter((entry) => isCurrent(entry.price, now));

  return (
    <section className="content-panel">
      <h2>Providers</h2>
      <p>
        Le rail Stripe de Billing V2 construit ses lignes en{" "}
        <code>price_data</code> inline : un checkout ne dépend d&apos;aucun{" "}
        <code>price_id</code> externe. Un rattachement manquant n&apos;y est donc
        pas bloquant. Un rail qui exige un plan préexistant, lui, ne peut pas
        vendre une offre non rattachée — ne rendez pas un service libre-service
        tant que son rail requis est incomplet.
      </p>

      {coverage.length === 0 ? (
        <p className="cell-secondary">
          Aucun rattachement provider n&apos;est enregistré.
        </p>
      ) : (
        <table className="admin-table">
          <caption>Couverture des rattachements provider</caption>
          <thead>
            <tr>
              <th scope="col">Provider</th>
              <th scope="col">Environnement</th>
              <th scope="col">Mapping requis</th>
              <th scope="col">Couverture</th>
              <th scope="col">Prix non rattachés</th>
            </tr>
          </thead>
          <tbody>
            {coverage.map((entry) => (
              <tr key={`${entry.provider}-${entry.environment}`}>
                <td>{entry.provider}</td>
                <td>{entry.environment}</td>
                <td>{entry.requiresExternalMapping ? "Oui" : "Non"}</td>
                <td>
                  {entry.mappedPriceCount} / {entry.currentPriceCount}
                </td>
                <td className="cell-secondary">
                  {entry.unmappedPriceCodes.slice(0, 6).join(", ")}
                  {entry.unmappedPriceCodes.length > 6 ? " …" : ""}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          const form = new FormData(event.currentTarget);
          void onCommand({
            kind: "provider.mapping",
            priceId: String(form.get("priceId") ?? ""),
            provider: String(form.get("provider") ?? "stripe"),
            environment: String(form.get("environment") ?? ""),
            externalProductId: String(form.get("productId") ?? ""),
            externalPriceId: String(form.get("externalPriceId") ?? ""),
            externalPlanId: String(form.get("planId") ?? ""),
            status: String(form.get("status") ?? "active"),
          });
        }}
      >
        <h3>Rattacher une version tarifaire</h3>
        <label>
          <span>Version tarifaire en vigueur</span>
          <select name="priceId">
            {currentPrices.map((entry) => (
              <option key={entry.price.id} value={entry.price.id}>
                {entry.price.priceCode} —{" "}
                {formatCurrencyFromCents(entry.price.amountCents)}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span>Provider</span>
          <select
            name="provider"
            onChange={(event) =>
              setMappingProvider(event.currentTarget.value)
            }
            value={mappingProvider}
          >
            <option value="stripe">Stripe</option>
            <option value="paypal">PayPal</option>
          </select>
        </label>
        <label>
          <span>Environnement</span>
          {/*
            Les environnements dependent du fournisseur : Stripe n'a pas de
            sandbox, PayPal n'a pas de test. Proposer la liste complete
            laisserait enregistrer un couple qui n'existe nulle part.
          */}
          <select key={mappingProvider} name="environment">
            {(PROVIDER_ENVIRONMENTS[mappingProvider] ?? []).map(
              (environment) => (
                <option key={environment} value={environment}>
                  {ENVIRONMENT_LABELS[environment] ?? environment}
                </option>
              ),
            )}
          </select>
        </label>
        <label>
          <span>Identifiant produit externe</span>
          <input maxLength={255} name="productId" />
        </label>
        <label>
          <span>Identifiant prix externe</span>
          <input maxLength={255} name="externalPriceId" />
        </label>
        <label>
          <span>Identifiant plan externe</span>
          <input maxLength={255} name="planId" />
        </label>
        <label>
          <span>Statut</span>
          <select defaultValue="active" name="status">
            <option value="active">Actif</option>
            <option value="inactive">Inactif</option>
          </select>
        </label>
        <button className="button" disabled={busy} type="submit">
          Enregistrer le rattachement
        </button>
      </form>
    </section>
  );
}

// ---------------------------------------------------------------------------

function isCurrent(price: BillingV2AdminPrice, now: Date) {
  const from = new Date(price.validFrom).getTime();
  const until = price.validUntil ? new Date(price.validUntil).getTime() : null;
  return price.status === "active"
    && from <= now.getTime()
    && (until === null || until > now.getTime());
}

function isScheduled(price: BillingV2AdminPrice, now: Date) {
  return price.status === "active"
    && new Date(price.validFrom).getTime() > now.getTime();
}

function formatDateTime(value: string | null) {
  if (!value) {
    return "—";
  }

  return new Date(value).toLocaleString("fr-FR", {
    timeZone: "Europe/Paris",
    dateStyle: "short",
    timeStyle: "short",
  });
}

function splitOnce(value: string, separator: string): [string, string] {
  const index = value.indexOf(separator);
  return index === -1
    ? [value.trim(), ""]
    : [value.slice(0, index).trim(), value.slice(index + 1).trim()];
}
