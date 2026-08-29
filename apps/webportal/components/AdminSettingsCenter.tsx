"use client";

import type {
  ApplicationSettingItem,
  ApplicationSettingsSnapshot,
  ConfigurationStatusDomain,
} from "@kermaria/shared";
import {
  Activity,
  ArrowLeft,
  ArrowRight,
  Database,
  FileText,
  Mail,
  Settings2,
  ShieldCheck,
  SlidersHorizontal,
  Wrench,
} from "lucide-react";
import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { StatusBadge } from "@/components/StatusBadge";
import { requestBffJson } from "@/lib/client-api";

const categoryLabels: Record<string, string> = {
  site: "Site & entreprise",
  messages: "Messages & communications",
  signup: "Inscriptions",
  security: "Sécurité",
  billing: "Facturation",
};

const categoryDescriptions: Record<string, string> = {
  site: "Identité, coordonnées et comportement général du portail.",
  messages: "Valeurs globales utilisées par les communications.",
  signup: "Règles appliquées au parcours d'inscription.",
  security: "Garde-vous et comportements de sécurité administrables.",
  billing: "Réglages applicatifs liés à la facturation.",
};

const classificationLabels: Record<string, string> = {
  dynamic: "Dynamique",
  restart_required: "Redémarrage requis",
  secret: "Secret",
  code_invariant: "Verrouillé par le code",
};

const riskLabels: Record<string, string> = {
  low: "faible",
  medium: "modéré",
  high: "élevé",
  critical: "critique",
};

const quickActions = [
  {
    href: "/admin/settings/messages",
    label: "Modifier un message",
    description: "E-mails, notifications et textes système",
    icon: Mail,
  },
  {
    href: "/admin/settings/diagnostic",
    label: "Configurer le diagnostic",
    description: "Parcours, questions et recommandations",
    icon: Activity,
  },
  {
    href: "/admin/settings/demonstrations",
    label: "Gérer les démonstrations",
    description: "Modèles de comptes et services présentés",
    icon: Wrench,
  },
  {
    href: "/admin/settings/audit",
    label: "Consulter l'audit",
    description: "Historique des modifications et permissions",
    icon: FileText,
  },
] as const;

export function AdminSettingsCenter({
  initialSnapshot,
  statusDomains,
}: {
  initialSnapshot: ApplicationSettingsSnapshot;
  statusDomains: ConfigurationStatusDomain[];
}) {
  const [settings, setSettings] = useState(initialSnapshot.settings);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [selectedSettingKey, setSelectedSettingKey] = useState<string | null>(null);

  const groups = useMemo(() => {
    const grouped = new Map<string, ApplicationSettingItem[]>();
    for (const setting of settings) {
      const items = grouped.get(setting.category) ?? [];
      items.push(setting);
      grouped.set(setting.category, items);
    }
    return [...grouped.entries()];
  }, [settings]);

  const selectedItems = selectedCategory
    ? groups.find(([category]) => category === selectedCategory)?.[1] ?? []
    : [];
  const selectedSetting =
    selectedItems.find((item) => item.key === selectedSettingKey)
    ?? selectedItems[0]
    ?? null;

  function openCategory(category: string) {
    const items = groups.find(([key]) => key === category)?.[1] ?? [];
    setSelectedCategory(category);
    setSelectedSettingKey(items[0]?.key ?? null);
    setMessage(null);
  }

  if (selectedCategory) {
    return (
      <section
        aria-label="Éditeur de paramètres"
        className="content-panel section-card admin-settings-main admin-settings-workspace-page"
      >
        <header className="admin-settings-workspace-header">
          <button
            className="button button-link admin-settings-back"
            onClick={() => setSelectedCategory(null)}
            type="button"
          >
            <ArrowLeft aria-hidden="true" size={17} />
            Retour à la vue d&apos;ensemble
          </button>
          <div>
            <p className="eyebrow">Paramètres</p>
            <h2>{categoryLabels[selectedCategory] ?? selectedCategory}</h2>
            <p className="muted">
              {categoryDescriptions[selectedCategory]
                ?? "Sélectionnez précisément le réglage à modifier."}
            </p>
          </div>
        </header>

        {message ? (
          <FormMessage
            tone={message.tone}
            title={
              message.tone === "success"
                ? "Configuration enregistrée"
                : "Modification refusée"
            }
          >
            {message.text}
          </FormMessage>
        ) : null}

        <div className="admin-settings-workspace">
          <aside
            aria-label="Réglages de la catégorie"
            className="admin-settings-selector"
          >
            <div className="admin-settings-selector-heading">
              <strong>
                {selectedItems.length} réglage{selectedItems.length > 1 ? "s" : ""}
              </strong>
              <span>Un seul réglage est affiché à la fois.</span>
            </div>
            <div className="admin-settings-selector-list">
              {selectedItems.map((setting) => (
                <button
                  aria-current={
                    selectedSetting?.key === setting.key ? "true" : undefined
                  }
                  className="admin-settings-selector-item"
                  key={setting.key}
                  onClick={() => {
                    setSelectedSettingKey(setting.key);
                    setMessage(null);
                  }}
                  type="button"
                >
                  <span>{setting.label}</span>
                  <small>{setting.editable ? "Modifiable" : "Lecture seule"}</small>
                  <ArrowRight aria-hidden="true" size={16} />
                </button>
              ))}
            </div>
          </aside>

          <div className="admin-settings-detail-panel">
            {selectedSetting ? (
              <SettingEditor
                key={selectedSetting.key}
                setting={selectedSetting}
                onSaved={(next) => {
                  setSettings((current) =>
                    current.map((item) => (item.key === next.key ? next : item)),
                  );
                  setMessage({
                    tone: "success",
                    text: "La valeur effective et sa version ont été mises à jour.",
                  });
                }}
                onError={(text) => setMessage({ tone: "error", text })}
              />
            ) : (
              <p className="admin-settings-inline-state muted">
                Aucun réglage dans cette catégorie.
              </p>
            )}
          </div>
        </div>
      </section>
    );
  }

  const healthyDomains = statusDomains.filter(
    (domain) => domain.state === "healthy",
  ).length;
  const attentionDomains = statusDomains.length - healthyDomains;

  return (
    <section
      aria-label="Vue d'ensemble du Centre de configuration"
      className="admin-settings-hub"
    >
      <div className="admin-settings-hub-status" aria-label="État des domaines">
        {statusDomains.slice(0, 5).map((domain) => (
          <article className="admin-settings-health-card" key={domain.key}>
            <div className="admin-settings-health-icon">
              <ShieldCheck aria-hidden="true" size={20} />
            </div>
            <div>
              <strong>{domain.label}</strong>
              <span>{domain.facts[0]?.value ?? "Configuration disponible"}</span>
            </div>
            <StatusBadge
              label={
                domain.state === "healthy"
                  ? "Prêt"
                  : domain.state === "warning"
                    ? "À vérifier"
                    : "Lecture seule"
              }
              tone={
                domain.state === "healthy"
                  ? "success"
                  : domain.state === "warning"
                    ? "warning"
                    : "info"
              }
            />
          </article>
        ))}
      </div>

      {message ? (
        <FormMessage
          tone={message.tone}
          title={
            message.tone === "success"
              ? "Configuration enregistrée"
              : "Modification refusée"
          }
        >
          {message.text}
        </FormMessage>
      ) : null}

      <div className="admin-settings-hub-grid">
        <section
          aria-labelledby="settings-quick-actions"
          className="content-panel admin-settings-hub-panel"
        >
          <header>
            <h2 id="settings-quick-actions">Actions rapides</h2>
            <p>Accédez directement aux opérations les plus courantes.</p>
          </header>
          <nav className="admin-settings-action-list">
            {quickActions.map(({ href, label, description, icon: Icon }) => (
              <Link href={href} key={href}>
                <span className="admin-settings-action-icon">
                  <Icon aria-hidden="true" size={18} />
                </span>
                <span>
                  <strong>{label}</strong>
                  <small>{description}</small>
                </span>
                <ArrowRight aria-hidden="true" size={17} />
              </Link>
            ))}
          </nav>
        </section>

        <section
          aria-labelledby="settings-system-state"
          className="content-panel admin-settings-hub-panel admin-settings-system-panel"
        >
          <header>
            <h2 id="settings-system-state">État du Centre</h2>
            <p>Les informations essentielles avant toute modification.</p>
          </header>
          <dl className="admin-settings-system-list">
            <div>
              <dt>
                <Database aria-hidden="true" size={17} />
                Persistance
              </dt>
              <dd>{initialSnapshot.persistent ? "MariaDB" : "Temporaire"}</dd>
            </div>
            <div>
              <dt>
                <SlidersHorizontal aria-hidden="true" size={17} />
                Paramètres
              </dt>
              <dd>{settings.length}</dd>
            </div>
            <div>
              <dt>
                <ShieldCheck aria-hidden="true" size={17} />
                Domaines sains
              </dt>
              <dd>
                {healthyDomains}/{statusDomains.length}
              </dd>
            </div>
            <div>
              <dt>
                <Activity aria-hidden="true" size={17} />À surveiller
              </dt>
              <dd>{attentionDomains}</dd>
            </div>
          </dl>
          <Link
            className="button button-secondary admin-settings-system-link"
            href="/admin/settings/audit"
          >
            Voir l&apos;audit et les permissions
          </Link>
        </section>
      </div>

      <section
        aria-labelledby="settings-categories-title"
        className="content-panel admin-settings-categories"
      >
        <header>
          <div>
            <h2 id="settings-categories-title">Catégories de paramètres</h2>
            <p>
              Choisissez d&apos;abord un domaine, puis le réglage précis à modifier.
            </p>
          </div>
          <StatusBadge label={ `${settings.length} paramètres` } tone="info" />
        </header>
        <div className="admin-settings-category-grid">
          {groups.map(([category, items], index) => {
            const Icon = [
              Settings2,
              Mail,
              SlidersHorizontal,
              ShieldCheck,
              Database,
            ][index % 5];
            return (
              <button
                className="admin-settings-category-card"
                key={category}
                onClick={() => openCategory(category)}
                type="button"
              >
                <span className="admin-settings-category-icon">
                  <Icon aria-hidden="true" size={20} />
                </span>
                <span className="admin-settings-category-copy">
                  <strong>{categoryLabels[category] ?? category}</strong>
                  <small>
                    {categoryDescriptions[category] ?? "Réglages applicatifs"}
                  </small>
                  <em>
                    {items.length} paramètre{items.length > 1 ? "s" : ""}
                  </em>
                </span>
                <ArrowRight aria-hidden="true" size={17} />
              </button>
            );
          })}
        </div>
      </section>
    </section>
  );
}

function describeValue(
  setting: ApplicationSettingItem,
  value: string,
): string {
  return setting.valueType === "bool"
    ? value === "true"
      ? "activ\u00e9e"
      : "d\u00e9sactiv\u00e9e"
    : value;
}

function SettingMeta({ setting }: { setting: ApplicationSettingItem }) {
  return (
    <div
      aria-label={`Métadonnées de ${setting.label}`}
      className="admin-settings-meta"
    >
      <code>{setting.key}</code>
      <span>
        {setting.source === "database"
          ? "Valeur enregistrée"
          : "Valeur par défaut"}
      </span>
      <span>
        {classificationLabels[setting.classification] ?? setting.classification}
      </span>
      <span
        className={`admin-settings-risk admin-settings-risk-${setting.risk}`}
      >
        Risque {riskLabels[setting.risk] ?? setting.risk}
      </span>
      {setting.restartRequired ? <span>Redérarrage requis</span> : null}
    </div>
  );
}

function SettingEditor({
  setting,
  onSaved,
  onError,
}: {
  setting: ApplicationSettingItem;
  onSaved: (setting: ApplicationSettingItem) => void;
  onError: (message: string) => void;
}) {
  const [value, setValue] = useState(String(setting.value));
  const [saving, setSaving] = useState(false);
  const dirty = setting.editable && value !== String(setting.value);

  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  async function save() {
    const normalized =
      setting.valueType === "bool"
        ? value === "true"
        : setting.valueType === "int"
          ? Number(value)
          : value;
    if (
      (setting.valueType === "int" && !Number.isInteger(normalized))
      || !dirty
    ) {
      return;
    }
    if (
      setting.risk === "high"
      && !window.confirm(
        `Paramètre à risque élevé.\n\n« ${setting.label} » passera à ${describeValue(setting, value)} immédiatement, pour tout le service.\n\nConfirmer ?`,
      )
    ) {
      return;
    }

    setSaving(true);
    const result = await requestBffJson<{
      setting: ApplicationSettingItem | null;
      message: string;
    }>(`/api/admin/settings/${setting.key}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        value: normalized,
        expectedVersion: setting.version,
      }),
    });
    setSaving(false);

    if (!result.ok || !result.data.setting) {
      onError(
        result.ok
          ? result.data.message
          : result.error.code === "SETTINGS_VERSION_CONFLICT"
            ? "Ce paramètre a été modifié ailleurs. Rechargez la page avant de recommencer."
            : result.error.message,
      );
      return;
    }
    onSaved(result.data.setting);
  }

  return (
    <section className="admin-settings-single-editor">
      <header>
        <div className="admin-settings-editor-icon">
          <Settings2 aria-hidden="true" size={20} />
        </div>
        <div>
          <p className="eyebrow">Réglage sélectionné</p>
          <h3>{setting.label}</h3>
          <p>{setting.description}</p>
        </div>
        <StatusBadge
          label={setting.editable ? "Modifiable" : "Lecture seule"}
          tone={setting.editable ? "info" : "neutral"}
        />
      </header>

      <SettingMeta setting={setting} />

      {!setting.editable ? (
        <div className="admin-settings-readonly-box">
          <span>Valeur effective</span>
          <strong>
            {setting.valueType === "bool"
              ? String(setting.value) === "true"
                ? "Activée"
                : "Désactivée"
              : String(setting.value)}
          </strong>
          <p>
            Ce réglage reste visible pour l&apos;exploitation mais ne peut pas être
            modifié depuis le navigateur.
          </p>
        </div>
      ) : (
        <form
          className="admin-settings-editor-form"
          onSubmit={(event) => {
            event.preventDefault();
            void save();
          }}
        >
          <label htmlFor={setting.key}>Valeur</label>
          {setting.valueType === "bool" ? (
            <select
              id={setting.key}
              onChange={(event) => setValue(event.target.value)}
              value={value}
            >
              <option value="true">Activée</option>
              <option value="false">Désactivée</option>
            </select>
          ) : (
            <input
              id={setting.key}
              inputMode={setting.valueType === "int" ? "numeric" : undefined}
              onChange={(event) => setValue(event.target.value)}
              type={
                setting.valueType === "email"
                  ? "email"
                  : setting.valueType === "int"
                    ? "number"
                    : "text"
              }
              value={value}
            />
          )}
          <div className="admin-settings-editor-actions">
            <button
              className="button"
              disabled={!dirty || saving}
              type="submit"
            >
              {saving ? "Enregistrement…" : "Enregistrer"}
            </button>
            {dirty ? (
              <button
                className="button button-link"
                onClick={() => setValue(String(setting.value))}
                type="button"
              >
                Annuler
              </button>
            ) : null}
          </div>
        </form>
      )}
    </section>
  );
}
