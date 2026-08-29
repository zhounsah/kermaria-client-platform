"use client";

import type { ApplicationSettingItem, ApplicationSettingsSnapshot, ConfigurationStatusDomain } from "@kermaria/shared";
import { useEffect, useMemo, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

const categoryLabels: Record<string, string> = { site: "Site & entreprise", messages: "Messages & communications", signup: "Inscriptions", security: "Sécurité", billing: "Facturation" };

export function AdminSettingsCenter({ initialSnapshot, statusDomains }: { initialSnapshot: ApplicationSettingsSnapshot; statusDomains: ConfigurationStatusDomain[] }) {
  const [settings, setSettings] = useState(initialSnapshot.settings);
  const [message, setMessage] = useState<{ tone: "success" | "error"; text: string } | null>(null);
  const groups = useMemo(() => {
    const grouped = new Map<string, ApplicationSettingItem[]>();
    for (const setting of settings) {
      const items = grouped.get(setting.category) ?? [];
      items.push(setting);
      grouped.set(setting.category, items);
    }
    return [...grouped.entries()];
  }, [settings]);
  return <section aria-label="Paramètres administrables" className="admin-settings-center">
    <p className="muted">{initialSnapshot.persistent ? "Les modifications sont persistées dans MariaDB." : "Mode de démonstration : les modifications disparaissent au redémarrage."}</p>
    {message ? <FormMessage tone={message.tone} title={message.tone === "success" ? "Configuration enregistrée" : "Modification refusée"}>{message.text}</FormMessage> : null}
    {statusDomains.length > 0 ? <section aria-label="État des domaines" className="admin-settings-status-grid">{statusDomains.map(domain => <article className={`admin-settings-status admin-settings-status-${domain.state}`} key={domain.key}><header><p className="eyebrow">{domain.state === "healthy" ? "Prêt" : domain.state === "warning" ? "À vérifier" : "Lecture seule"}</p><h2>{domain.label}</h2></header><dl>{domain.facts.map(fact => <div key={fact.label}><dt>{fact.label}</dt><dd>{fact.sensitive ? (fact.value === "Configuré" ? "Configuré" : "Non configuré") : fact.value}</dd></div>)}</dl>{domain.warning ? <p role="status">{domain.warning}</p> : null}</article>)}</section> : null}
    <div className="admin-settings-grid">
      {groups.map(([category, items]) => <section className="admin-settings-card" key={category}><header><p className="eyebrow">Configuration</p><h2>{categoryLabels[category] ?? category}</h2></header><div>{items.map(setting => <SettingEditor key={setting.key} setting={setting} onSaved={(next) => { setSettings(current => current.map(item => item.key === next.key ? next : item)); setMessage({ tone: "success", text: "La valeur effective et sa version ont été mises à jour." }); }} onError={(text) => setMessage({ tone: "error", text })} />)}</div></section>)}
    </div>
  </section>;
}

const classificationLabels: Record<string, string> = { dynamic: "Dynamique", restart_required: "Redémarrage requis", secret: "Secret", code_invariant: "Verrouillé par le code" };

function describeValue(setting: ApplicationSettingItem, value: string): string {
  return setting.valueType === "bool" ? (value === "true" ? "activée" : "désactivée") : `« ${value} »`;
}

function SettingEditor({ setting, onSaved, onError }: { setting: ApplicationSettingItem; onSaved: (setting: ApplicationSettingItem) => void; onError: (message: string) => void }) {
  const [value, setValue] = useState(String(setting.value));
  const [saving, setSaving] = useState(false);
  const dirty = setting.editable && value !== String(setting.value);
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
  async function save() {
    const normalized = setting.valueType === "bool" ? value === "true" : setting.valueType === "int" ? Number(value) : value;
    if ((setting.valueType === "int" && !Number.isInteger(normalized)) || !dirty) return;
    // Confirmation renforcee : un reglage a risque eleve change le comportement
    // du service en production des l'enregistrement, sans redeploiement.
    if (setting.risk === "high" && !window.confirm(`Paramètre à risque élevé.

« ${setting.label} » passera à ${describeValue(setting, value)} immédiatement, pour tout le service.

Confirmer ?`)) return;
    setSaving(true);
    const result = await requestBffJson<{ setting: ApplicationSettingItem | null; message: string }>(`/api/admin/settings/${setting.key}`, { method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ value: normalized, expectedVersion: setting.version }) });
    setSaving(false);
    if (!result.ok || !result.data.setting) { onError(result.ok ? result.data.message : result.error.code === "SETTINGS_VERSION_CONFLICT" ? "Ce paramètre a été modifié ailleurs. Rechargez la page avant de recommencer." : result.error.message); return; }
    onSaved(result.data.setting);
  }
  const meta = <small><code>{setting.key}</code> · {setting.source === "database" ? "Valeur enregistrée" : "Valeur par défaut"} · <strong>{classificationLabels[setting.classification] ?? setting.classification}</strong> · <strong>Risque {setting.risk}</strong>{setting.restartRequired ? <> · <strong>Redémarrage requis</strong></> : null}</small>;
  // Un parametre verrouille par le code reste visible : masquer son existence
  // rendrait son etat reel invisible de l'exploitant. Il n'offre simplement
  // aucun controle, et l'API refuse l'ecriture de toute facon.
  if (!setting.editable) {
    return <div className="admin-settings-row admin-settings-row-readonly">
      <div><p className="admin-settings-label">{setting.label}</p><p id={`${setting.key}-help`}>{setting.description}</p>{meta}</div>
      <div className="admin-settings-control"><p aria-describedby={`${setting.key}-help`}><strong>{setting.valueType === "bool" ? (String(setting.value) === "true" ? "Activée" : "Désactivée") : String(setting.value)}</strong></p><p className="muted">Lecture seule</p></div>
    </div>;
  }
  return <form className="admin-settings-row" onSubmit={(event) => { event.preventDefault(); void save(); }}>
    <div><label htmlFor={setting.key}>{setting.label}</label><p id={`${setting.key}-help`}>{setting.description}</p>{meta}</div>
    <div className="admin-settings-control">
      {setting.valueType === "bool" ? <select aria-describedby={`${setting.key}-help`} id={setting.key} onChange={event => setValue(event.target.value)} value={value}><option value="true">Activée</option><option value="false">Désactivée</option></select> : <input aria-describedby={`${setting.key}-help`} id={setting.key} inputMode={setting.valueType === "int" ? "numeric" : undefined} onChange={event => setValue(event.target.value)} type={setting.valueType === "email" ? "email" : setting.valueType === "int" ? "number" : "text"} value={value} />}
      <button className="button button-secondary" disabled={!dirty || saving} type="submit">{saving ? "Enregistrement…" : "Enregistrer"}</button>
      {dirty ? <button className="button button-link" onClick={() => setValue(String(setting.value))} type="button">Annuler</button> : null}
    </div>
  </form>;
}
