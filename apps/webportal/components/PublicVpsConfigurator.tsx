"use client";

import Link from "next/link";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import type { FormEvent, RefObject } from "react";
import type {
  BillingV2PublicQuote,
  BillingV2VpsCheckoutResponse,
  BillingV2VpsConfigurationPayload,
  BillingV2VpsConfigurationQuoteResponse,
} from "@kermaria/shared";

import { requestBffJson } from "@/lib/client-api";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getPortalFamilyCookieDomain } from "@/lib/public-route-config";

export type PublicVpsConfiguratorSelection = {
  serviceCode: string;
  serviceName: string;
  serviceDescription: string | null;
  tierCode: string;
  tierLabel: string;
  tierDescription: string | null;
  specifications: string[];
  pricing: {
    monthlyLabel: string;
    setupFees: Array<{ amountLabel: string }>;
  };
};

type VpsConfiguration = {
  hostname: string;
  operatingSystem: string;
  usage: string;
  managementMode: string;
  internetExposure: "yes" | "no" | "to_confirm";
  comment: string;
};

type Props = {
  selection: PublicVpsConfiguratorSelection;
};

const INITIAL_CONFIGURATION: VpsConfiguration = {
  hostname: "",
  operatingSystem: "",
  usage: "",
  managementMode: "",
  internetExposure: "to_confirm",
  comment: "",
};

const INTERNET_EXPOSURE_LABELS: Record<VpsConfiguration["internetExposure"], string> = {
  yes: "Oui, le VPS devra être accessible depuis Internet",
  no: "Non, pas d’exposition Internet prévue",
  to_confirm: "À préciser lors de la validation technique",
};

const VPS_DRAFT_STORAGE_PREFIX = "kermaria:vps-configurator-draft:v1";
const VPS_DRAFT_COOKIE_PREFIX = "kermaria_vps_configurator_draft_v1";
const VPS_DRAFT_COOKIE_CHUNK_SIZE = 2_800;
const VPS_DRAFT_COOKIE_MAX_CHUNKS = 4;
const VPS_DRAFT_CHANGED_EVENT = "kermaria:vps-configurator-draft:changed";

type VpsConfiguratorDraft = {
  serviceCode: string;
  tierCode: string;
  configuration: VpsConfiguration;
};

type IdentityDialogState = "closed" | "open" | "closing";

export function PublicVpsConfigurator({ selection }: Props) {
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [quote, setQuote] = useState<BillingV2PublicQuote | null>(null);
  const [technicalRequestId, setTechnicalRequestId] = useState<string | null>(null);
  const [submissionError, setSubmissionError] = useState<string | null>(null);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [startingPayment, setStartingPayment] = useState(false);
  const [identityDialogState, setIdentityDialogState] = useState<IdentityDialogState>(
    "closed",
  );
  const [configuration, setConfiguration] = useState<VpsConfiguration>(
    INITIAL_CONFIGURATION,
  );
  const idempotencyKey = useRef<string | null>(null);
  const checkoutIdempotencyKey = useRef<string | null>(null);
  const identityDialogRef = useRef<HTMLDivElement | null>(null);
  const identityCloseButtonRef = useRef<HTMLButtonElement | null>(null);
  const identityReturnFocusRef = useRef<HTMLElement | null>(null);
  const identityDialogStateRef = useRef<IdentityDialogState>("closed");
  const [useRestoredDraft, setUseRestoredDraft] = useState(true);
  const draftStorageKey = useMemo(
    () => getVpsDraftStorageKey(selection),
    [selection],
  );
  const readDraft = useCallback(
    () => readVpsDraftValue(draftStorageKey),
    [draftStorageKey],
  );
  const storedDraftValue = useSyncExternalStore(
    subscribeToVpsDraft,
    readDraft,
    () => null,
  );
  const storedDraft = useMemo(
    () => parseVpsDraft(storedDraftValue),
    [storedDraftValue],
  );
  const restoredDraft = useRestoredDraft
    && storedDraft?.serviceCode === selection.serviceCode
    && storedDraft.tierCode === selection.tierCode
    ? storedDraft
    : null;
  const effectiveConfiguration = restoredDraft?.configuration ?? configuration;
  const effectiveStep = restoredDraft && step === 1 ? 2 : step;
  const identityDialogPresent = identityDialogState !== "closed";
  const identityDialogClosing = identityDialogState === "closing";

  const updateIdentityDialogState = useCallback((nextState: IdentityDialogState) => {
    identityDialogStateRef.current = nextState;
    setIdentityDialogState(nextState);
  }, []);

  const finishClosingIdentityDialog = useCallback(() => {
    updateIdentityDialogState("closed");
    window.requestAnimationFrame(() => {
      identityReturnFocusRef.current?.focus();
    });
  }, [updateIdentityDialogState]);

  const closeIdentityDialog = useCallback(() => {
    if (identityDialogStateRef.current !== "open") {
      return;
    }

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      finishClosingIdentityDialog();
      return;
    }

    updateIdentityDialogState("closing");
  }, [finishClosingIdentityDialog, updateIdentityDialogState]);

  useEffect(() => {
    if (!identityDialogPresent) {
      return undefined;
    }

    const previousOverflow = document.body.style.overflow;
    const previousPaddingRight = document.body.style.paddingRight;
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    const currentPaddingRight = Number.parseFloat(
      window.getComputedStyle(document.body).paddingRight,
    ) || 0;
    document.body.style.overflow = "hidden";
    if (scrollbarWidth > 0) {
      document.body.style.paddingRight = `${currentPaddingRight + scrollbarWidth}px`;
    }
    const focusFrame = window.requestAnimationFrame(() => {
      if (identityDialogStateRef.current === "open") {
        identityCloseButtonRef.current?.focus();
      }
    });
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        closeIdentityDialog();
        return;
      }
      if (event.key !== "Tab") {
        return;
      }

      const focusable = identityDialogRef.current?.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])',
      );
      if (!focusable?.length) {
        return;
      }
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKeyDown);

    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.body.style.overflow = previousOverflow;
      document.body.style.paddingRight = previousPaddingRight;
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [closeIdentityDialog, identityDialogPresent]);

  function updateConfiguration<Key extends keyof VpsConfiguration>(
    key: Key,
    value: VpsConfiguration[Key],
  ) {
    setUseRestoredDraft(false);
    setConfiguration((current) => ({
      ...(restoredDraft?.configuration ?? current),
      [key]: value,
    }));
  }

  function saveDraft(draftConfiguration = effectiveConfiguration) {
    try {
      const draft: VpsConfiguratorDraft = {
        serviceCode: selection.serviceCode,
        tierCode: selection.tierCode,
        configuration: draftConfiguration,
      };
      const serializedDraft = JSON.stringify(draft);
      window.sessionStorage.setItem(draftStorageKey, serializedDraft);
      writeVpsDraftCookie(draftStorageKey, serializedDraft);
      notifyVpsDraftChanged();
    } catch {
      // Le formulaire reste utilisable si le navigateur bloque le stockage
      // temporaire. Aucune donnée technique libre ne bascule dans l'URL.
    }
  }

  function clearDraft() {
    try {
      window.sessionStorage.removeItem(draftStorageKey);
      clearVpsDraftCookie(draftStorageKey);
      notifyVpsDraftChanged();
    } catch {
      // Rien à faire : le brouillon temporaire expirera avec la session.
    }
  }

  function requireIdentity() {
    saveDraft();
    identityReturnFocusRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    updateIdentityDialogState("open");
  }

  async function continueToSummary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmissionError(null);
    setSaving(true);
    if (!idempotencyKey.current) {
      idempotencyKey.current = globalThis.crypto.randomUUID();
    }

    const payload: BillingV2VpsConfigurationPayload = {
      serviceCode: selection.serviceCode,
      tierCode: selection.tierCode,
      ...effectiveConfiguration,
      idempotencyKey: idempotencyKey.current,
    };
    try {
      const result = await requestBffJson<BillingV2VpsConfigurationQuoteResponse>(
        "/api/vps/configurations",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        },
      );
      if (!result.ok) {
        if (result.status === 401) {
          requireIdentity();
          return;
        }
        setSubmissionError(result.error.message);
        return;
      }

      setQuote(result.data.quote);
      setTechnicalRequestId(result.data.configurationId);
      setUseRestoredDraft(false);
      clearDraft();
      setStep(3);
    } catch {
      setSubmissionError("La configuration n’a pas pu être enregistrée. Réessayez.");
    } finally {
      setSaving(false);
    }
  }

  async function continueToPayment() {
    if (!technicalRequestId || startingPayment) return;
    setPaymentError(null);
    setStartingPayment(true);
    if (!checkoutIdempotencyKey.current) {
      checkoutIdempotencyKey.current = globalThis.crypto.randomUUID();
    }

    try {
      const result = await requestBffJson<BillingV2VpsCheckoutResponse>(
        "/api/vps/checkout",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "Idempotency-Key": checkoutIdempotencyKey.current,
          },
          body: JSON.stringify({ technicalRequestId }),
        },
      );
      if (!result.ok) {
        if (result.status === 401) {
          requireIdentity();
          return;
        }
        setPaymentError(result.error.message);
        return;
      }
      window.location.assign(result.data.approveUrl);
    } catch {
      setPaymentError("Le paiement n’a pas pu être initialisé. Réessayez.");
    } finally {
      setStartingPayment(false);
    }
  }

  return (
    <>
      <main className="services-page vps-configurator-page">
      <nav aria-label="Fil d’Ariane" className="service-breadcrumb">
        <Link href="/">Accueil</Link>
        <span aria-hidden="true">/</span>
        <Link href="/services">Services</Link>
        <span aria-hidden="true">/</span>
        <Link href="/services/vps">VPS</Link>
        <span aria-hidden="true">/</span>
        <span aria-current="page">Configurer</span>
      </nav>

      <header className="vps-configurator-hero">
        <p className="card-kicker">Préparation de votre VPS</p>
        <h1>Configurez votre VPS</h1>
        <p>
          Vérifiez votre offre, décrivez votre besoin technique, puis relisez le
          récapitulatif avant l’ouverture de la commande.
        </p>
      </header>

      <ol className="vps-configurator-steps" aria-label="Étapes de configuration">
        <StepItem currentStep={effectiveStep} number={1} title="Votre VPS" />
        <StepItem currentStep={effectiveStep} number={2} title="Configuration" />
        <StepItem currentStep={effectiveStep} number={3} title="Récapitulatif" />
      </ol>

      {effectiveStep === 1 ? (
        <section className="vps-configurator-panel" aria-labelledby="vps-selection-title">
          <div className="vps-configurator-panel-heading">
            <div>
              <p className="card-kicker">Votre VPS</p>
              <h2 id="vps-selection-title">{selection.serviceName} — {selection.tierLabel}</h2>
              {selection.tierDescription ?? selection.serviceDescription ? (
                <p>{selection.tierDescription ?? selection.serviceDescription}</p>
              ) : null}
            </div>
            <Link className="text-link" href="/services/vps">Modifier le choix</Link>
          </div>
          <VpsSelectionSummary selection={selection} />
          <div className="vps-configurator-actions">
            <button className="button" onClick={() => setStep(2)} type="button">
              Continuer vers la configuration
            </button>
          </div>
        </section>
      ) : null}

      {effectiveStep === 2 ? (
        <section className="vps-configurator-panel" aria-labelledby="vps-configuration-title">
          <div className="vps-configurator-panel-heading">
            <div>
              <p className="card-kicker">Configuration</p>
              <h2 id="vps-configuration-title">Décrivez votre besoin</h2>
              <p>
                Ces informations préparent la future mise en service. N’indiquez
                jamais de mot de passe, clé privée, jeton ou autre secret.
              </p>
            </div>
          </div>
          <form className="vps-configuration-form" onSubmit={continueToSummary}>
            <div className="form-grid">
              <label className="form-field" htmlFor="vps-hostname">
                <span>Hostname souhaité</span>
                <input
                  autoComplete="off"
                  id="vps-hostname"
                  maxLength={253}
                  onChange={(event) => updateConfiguration("hostname", event.target.value)}
                  placeholder="ex. application-metier"
                  required
                  value={effectiveConfiguration.hostname}
                />
                <span className="form-hint">Sans mot de passe ni identifiant technique.</span>
              </label>
              <label className="form-field" htmlFor="vps-operating-system">
                <span>OS ou template souhaité</span>
                <input
                  autoComplete="off"
                  id="vps-operating-system"
                  maxLength={120}
                  onChange={(event) => updateConfiguration("operatingSystem", event.target.value)}
                  placeholder="ex. Debian 12"
                  required
                  value={effectiveConfiguration.operatingSystem}
                />
                <span className="form-hint">La disponibilité sera revalidée ultérieurement.</span>
              </label>
              <label className="form-field" htmlFor="vps-management-mode">
                <span>Mode de gestion souhaité</span>
                <input
                  autoComplete="off"
                  id="vps-management-mode"
                  maxLength={120}
                  onChange={(event) => updateConfiguration("managementMode", event.target.value)}
                  placeholder="ex. Autogéré ou à préciser"
                  required
                  value={effectiveConfiguration.managementMode}
                />
              </label>
              <label className="form-field" htmlFor="vps-internet-exposure">
                <span>Exposition Internet</span>
                <select
                  id="vps-internet-exposure"
                  onChange={(event) => updateConfiguration(
                    "internetExposure",
                    event.target.value as VpsConfiguration["internetExposure"],
                  )}
                  value={effectiveConfiguration.internetExposure}
                >
                  <option value="to_confirm">À préciser lors de la validation technique</option>
                  <option value="yes">Oui, le VPS devra être accessible depuis Internet</option>
                  <option value="no">Non, pas d’exposition Internet prévue</option>
                </select>
              </label>
              <label className="form-field vps-form-field-wide" htmlFor="vps-usage">
                <span>Usage prévu</span>
                <textarea
                  id="vps-usage"
                  maxLength={1000}
                  onChange={(event) => updateConfiguration("usage", event.target.value)}
                  placeholder="Décrivez les applications ou services à héberger."
                  required
                  value={effectiveConfiguration.usage}
                />
              </label>
              <label className="form-field vps-form-field-wide" htmlFor="vps-comment">
                <span>Commentaire complémentaire <span className="vps-field-optional">(facultatif)</span></span>
                <textarea
                  id="vps-comment"
                  maxLength={1000}
                  onChange={(event) => updateConfiguration("comment", event.target.value)}
                  placeholder="Informations utiles, sans donnée secrète."
                  value={effectiveConfiguration.comment}
                />
              </label>
            </div>
            <div className="vps-configurator-actions">
              <button className="button button-secondary" onClick={() => setStep(1)} type="button">
                Retour
              </button>
              <button className="button" disabled={saving} type="submit">
                {saving ? "Enregistrement…" : "Voir le récapitulatif"}
              </button>
            </div>
            {submissionError ? <p className="field-error" role="alert">{submissionError}</p> : null}
          </form>
        </section>
      ) : null}

      {effectiveStep === 3 ? (
        <section className="vps-configurator-panel" aria-labelledby="vps-summary-title">
          <div className="vps-configurator-panel-heading">
            <div>
              <p className="card-kicker">Récapitulatif</p>
              <h2 id="vps-summary-title">Votre préparation VPS</h2>
              <p>
                Vérifiez votre configuration et les montants associés avant de
                poursuivre. Aucun paiement n’est effectué tant que vous ne choisissez
                pas de continuer.
              </p>
            </div>
          </div>
          <VpsSelectionSummary quote={quote} selection={selection} />
          <dl className="vps-configuration-summary">
            <div><dt>Hostname</dt><dd>{effectiveConfiguration.hostname}</dd></div>
            <div><dt>OS ou template</dt><dd>{effectiveConfiguration.operatingSystem}</dd></div>
            <div><dt>Mode de gestion</dt><dd>{effectiveConfiguration.managementMode}</dd></div>
            <div><dt>Exposition Internet</dt><dd>{INTERNET_EXPOSURE_LABELS[effectiveConfiguration.internetExposure]}</dd></div>
            <div className="vps-summary-wide"><dt>Usage prévu</dt><dd>{effectiveConfiguration.usage}</dd></div>
            {effectiveConfiguration.comment ? (
              <div className="vps-summary-wide"><dt>Commentaire</dt><dd>{effectiveConfiguration.comment}</dd></div>
            ) : null}
          </dl>
          <p className="vps-configurator-notice">
            Votre configuration technique est enregistrée. Votre commande sera
            vérifiée une dernière fois avant l’ouverture du paiement sécurisé.
          </p>
          <div className="vps-configurator-actions">
            <button className="button button-secondary" onClick={() => setStep(2)} type="button">
              Modifier la configuration
            </button>
            <button
              className="button"
              disabled={!technicalRequestId || startingPayment}
              onClick={continueToPayment}
              type="button"
            >
              {startingPayment ? "Ouverture du paiement…" : "Continuer vers le paiement"}
            </button>
          </div>
          {paymentError ? <p className="field-error" role="alert">{paymentError}</p> : null}
        </section>
      ) : null}
      </main>
      {identityDialogPresent ? (
        <IdentityContinuationDialog
          closeButtonRef={identityCloseButtonRef}
          dialogRef={identityDialogRef}
          isClosing={identityDialogClosing}
          onExitAnimationEnd={finishClosingIdentityDialog}
          onDismiss={closeIdentityDialog}
          selection={selection}
        />
      ) : null}
    </>
  );
}

function IdentityContinuationDialog({
  closeButtonRef,
  dialogRef,
  isClosing,
  onExitAnimationEnd,
  onDismiss,
  selection,
}: {
  closeButtonRef: RefObject<HTMLButtonElement | null>;
  dialogRef: RefObject<HTMLDivElement | null>;
  isClosing: boolean;
  onExitAnimationEnd: () => void;
  onDismiss: () => void;
  selection: PublicVpsConfiguratorSelection;
}) {
  const next = `/services/vps/choisir?serviceCode=${encodeURIComponent(selection.serviceCode)}&tierCode=${encodeURIComponent(selection.tierCode)}`;
  return (
    <div
      className={`vps-identity-dialog-backdrop${isClosing ? " is-closing" : ""}`}
      onAnimationEnd={(event) => {
        if (isClosing && event.target === event.currentTarget) {
          onExitAnimationEnd();
        }
      }}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onDismiss();
        }
      }}
    >
      <section
        aria-describedby="vps-identity-dialog-description"
        aria-labelledby="vps-identity-dialog-title"
        aria-modal="true"
        className="vps-identity-dialog"
        ref={dialogRef}
        role="dialog"
      >
        <button
          aria-label="Fermer la fenêtre"
          className="vps-identity-dialog-close"
          onClick={onDismiss}
          ref={closeButtonRef}
          type="button"
        >
          <span aria-hidden="true">×</span>
        </button>
        <p className="card-kicker">Votre VPS</p>
        <h2 id="vps-identity-dialog-title">Votre configuration est prête</h2>
        <p id="vps-identity-dialog-description">
          Pour consulter le récapitulatif et poursuivre votre commande,
          connectez-vous ou créez votre espace client.
        </p>
        <p className="vps-identity-dialog-reassurance">
          Votre configuration sera conservée.
        </p>
        <div className="vps-identity-dialog-actions">
          <Link
            className="button"
            href={`/signup?flow=vps_self_service&next=${encodeURIComponent(next)}`}
          >
            Créer un compte
          </Link>
          <Link
            className="button button-secondary"
            href={`/login?next=${encodeURIComponent(next)}`}
          >
            Se connecter
          </Link>
        </div>
      </section>
    </div>
  );
}

function getVpsDraftStorageKey(selection: PublicVpsConfiguratorSelection) {
  return `${VPS_DRAFT_STORAGE_PREFIX}:${selection.serviceCode}:${selection.tierCode}`;
}

function getVpsDraftCookieName(storageKey: string) {
  return `${VPS_DRAFT_COOKIE_PREFIX}_${storageKey
    .slice(VPS_DRAFT_STORAGE_PREFIX.length + 1)
    .replace(/[^A-Za-z0-9_-]/g, "_")}`;
}

function subscribeToVpsDraft(onStoreChange: () => void) {
  window.addEventListener("storage", onStoreChange);
  window.addEventListener(VPS_DRAFT_CHANGED_EVENT, onStoreChange);
  return () => {
    window.removeEventListener("storage", onStoreChange);
    window.removeEventListener(VPS_DRAFT_CHANGED_EVENT, onStoreChange);
  };
}

function readVpsDraftValue(storageKey: string): string | null {
  try {
    return readVpsDraftCookie(storageKey)
      ?? window.sessionStorage.getItem(storageKey);
  } catch {
    return null;
  }
}

/**
 * `sessionStorage` reste utile sur une même origine, mais la connexion client
 * passe de la vitrine à `dashboard.<domaine>`. Le brouillon non sensible est
 * donc aussi gardé dans un cookie de session, limité au domaine de portail,
 * afin de survivre à cette bascule. Les valeurs restent réévaluées côté
 * serveur avant leur persistance et ne sont jamais ajoutées à l'URL.
 */
function writeVpsDraftCookie(storageKey: string, value: string) {
  const baseName = getVpsDraftCookieName(storageKey);
  const encoded = encodeURIComponent(value);
  const chunks = encoded.match(new RegExp(`.{1,${VPS_DRAFT_COOKIE_CHUNK_SIZE}}`, "g")) ?? [];
  if (chunks.length === 0 || chunks.length > VPS_DRAFT_COOKIE_MAX_CHUNKS) {
    return;
  }

  const previousCount = readVpsDraftCookieChunkCount(baseName);
  chunks.forEach((chunk, index) => {
    writeVpsDraftCookieValue(`${baseName}_${index}`, chunk);
  });
  writeVpsDraftCookieValue(`${baseName}_count`, String(chunks.length));
  for (let index = chunks.length; index < previousCount; index += 1) {
    expireVpsDraftCookie(`${baseName}_${index}`);
  }
}

function readVpsDraftCookie(storageKey: string): string | null {
  const baseName = getVpsDraftCookieName(storageKey);
  const chunkCount = readVpsDraftCookieChunkCount(baseName);
  if (chunkCount === 0) {
    return null;
  }

  const chunks: string[] = [];
  for (let index = 0; index < chunkCount; index += 1) {
    const chunk = readCookieValue(`${baseName}_${index}`);
    if (chunk === null) {
      return null;
    }
    chunks.push(chunk);
  }

  try {
    return decodeURIComponent(chunks.join(""));
  } catch {
    return null;
  }
}

function clearVpsDraftCookie(storageKey: string) {
  const baseName = getVpsDraftCookieName(storageKey);
  const chunkCount = readVpsDraftCookieChunkCount(baseName);
  for (let index = 0; index < Math.max(chunkCount, VPS_DRAFT_COOKIE_MAX_CHUNKS); index += 1) {
    expireVpsDraftCookie(`${baseName}_${index}`);
  }
  expireVpsDraftCookie(`${baseName}_count`);
}

function readVpsDraftCookieChunkCount(baseName: string) {
  const parsed = Number.parseInt(readCookieValue(`${baseName}_count`) ?? "", 10);
  return Number.isSafeInteger(parsed)
    && parsed > 0
    && parsed <= VPS_DRAFT_COOKIE_MAX_CHUNKS
    ? parsed
    : 0;
}

function readCookieValue(name: string) {
  const prefix = `${name}=`;
  const cookie = document.cookie
    .split(";")
    .map((entry) => entry.trim())
    .find((entry) => entry.startsWith(prefix));
  return cookie ? cookie.slice(prefix.length) : null;
}

function writeVpsDraftCookieValue(name: string, value: string) {
  document.cookie = `${name}=${value}; ${vpsDraftCookieAttributes()}`;
}

function expireVpsDraftCookie(name: string) {
  document.cookie = `${name}=; Max-Age=0; ${vpsDraftCookieAttributes()}`;
}

function vpsDraftCookieAttributes() {
  const rootDomain = getPortalFamilyCookieDomain(window.location.hostname);
  const domain = rootDomain ? ` Domain=${rootDomain};` : "";
  const secure = window.location.protocol === "https:" ? " Secure;" : "";
  return `Path=/;${domain} SameSite=Lax;${secure}`;
}

function notifyVpsDraftChanged() {
  window.dispatchEvent(new Event(VPS_DRAFT_CHANGED_EVENT));
}

function parseVpsDraft(raw: string | null): VpsConfiguratorDraft | null {
  try {
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as Partial<VpsConfiguratorDraft>;
    const configuration = parsed.configuration;
    if (
      typeof parsed.serviceCode !== "string"
      || typeof parsed.tierCode !== "string"
      || !configuration
      || typeof configuration.hostname !== "string"
      || typeof configuration.operatingSystem !== "string"
      || typeof configuration.usage !== "string"
      || typeof configuration.managementMode !== "string"
      || typeof configuration.comment !== "string"
      || !["yes", "no", "to_confirm"].includes(configuration.internetExposure)
    ) {
      return null;
    }
    return {
      serviceCode: parsed.serviceCode,
      tierCode: parsed.tierCode,
      configuration: {
        hostname: configuration.hostname,
        operatingSystem: configuration.operatingSystem,
        usage: configuration.usage,
        managementMode: configuration.managementMode,
        internetExposure: configuration.internetExposure,
        comment: configuration.comment,
      },
    };
  } catch {
    return null;
  }
}

function StepItem({
  currentStep,
  number,
  title,
}: {
  currentStep: number;
  number: number;
  title: string;
}) {
  const state = number === currentStep ? "is-current" : number < currentStep ? "is-complete" : "";
  return (
    <li className={state} aria-current={number === currentStep ? "step" : undefined}>
      <span>{number}</span>
      <strong>{title}</strong>
    </li>
  );
}

function VpsSelectionSummary({
  quote = null,
  selection,
}: {
  quote?: BillingV2PublicQuote | null;
  selection: PublicVpsConfiguratorSelection;
}) {
  return (
    <div className="vps-selection-summary">
      <div className="vps-selection-summary-main">
        <h3>{selection.serviceName} — {selection.tierLabel}</h3>
        {selection.specifications.length ? (
          <ul aria-label="Caractéristiques du VPS" className="vps-tier-specifications">
            {selection.specifications.map((specification) => <li key={specification}>{specification}</li>)}
          </ul>
        ) : null}
      </div>
      {quote ? <VpsQuoteSummary quote={quote} /> : <VpsCatalogPriceSummary pricing={selection.pricing} />}
    </div>
  );
}

function VpsCatalogPriceSummary({
  pricing,
}: {
  pricing: PublicVpsConfiguratorSelection["pricing"];
}) {
  return (
    <dl className="vps-price-summary">
      <div>
        <dt>Frais de mise en service</dt>
        {pricing.setupFees.length ? (
          <dd>
            <ul>
              {pricing.setupFees.map((fee, index) => (
                <li key={`${fee.amountLabel}-${index}`}>
                  Frais de mise en service&nbsp;: <strong>{fee.amountLabel}</strong>
                </li>
              ))}
            </ul>
          </dd>
        ) : <dd>Aucun frais de mise en service publié</dd>}
      </div>
      <div>
        <dt>Abonnement</dt>
        <dd><strong>{pricing.monthlyLabel}</strong> / mois</dd>
      </div>
    </dl>
  );
}

function VpsQuoteSummary({ quote }: { quote: BillingV2PublicQuote }) {
  const setupLines = quote.lines.filter((line) => line.billingCadence === "one_time");
  return (
    <dl className="vps-price-summary">
      <div>
        <dt>À payer aujourd’hui</dt>
        <dd><strong>{formatCurrencyFromCents(quote.totalDueNowCents)}</strong></dd>
        {quote.oneTimeCents > 0 ? (
          <p className="vps-price-detail">Dont {formatCurrencyFromCents(quote.oneTimeCents)} de frais ponctuels.</p>
        ) : null}
      </div>
      <div>
        <dt>Abonnement</dt>
        <dd><strong>{formatCurrencyFromCents(quote.monthlyAfterDiscountCents)}</strong> / mois</dd>
        {setupLines.length ? (
          <p className="vps-price-detail">{setupLines.map((line) => line.detail ?? line.label).join(" · ")}</p>
        ) : null}
      </div>
    </dl>
  );
}
