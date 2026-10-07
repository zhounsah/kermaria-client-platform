import type { Metadata } from "next";
import Link from "next/link";

import { SignupForm } from "@/components/SignupForm";
import { SitePageFrame } from "@/components/SitePageFrame";
import { readBillingV2SelectionSearchParams } from "@/lib/billing-v2-selection";
import { resolveServicePublicDetail, resolveServicePublicLabel } from "@/lib/billing-v2-formules";
import {
  getBillingV2FormulesCatalog,
  getPublicSignupMode,
  getPublicSitePageLayout,
  quoteBillingV2Formule,
} from "@/lib/internal-api";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { resolveCorrelationId } from "@/lib/correlation";
import { isSignupEnabled } from "@/lib/public-routes";
import {
  resolveSelfServiceCartSignupContinuation,
  resolveSelfServiceVpsSignupContinuation,
} from "@/lib/public-route-config";
import styles from "./page.module.css";

export const metadata: Metadata = {
  title: "Créer un compte",
  description:
    "Demandez l'ouverture de votre accès client et reprenez, si besoin, l'offre déjà configurée sur la vitrine.",
  // Seule route non publique qui n'avait ni `X-Robots-Tag` (via
  // NOINDEX_ROUTE_PREFIXES dans `next.config.ts`) ni `Disallow`. Comme
  // `robots.txt` n'empeche pas l'indexation d'une URL decouverte par un
  // lien externe, le `noindex` est pose ici, ou il est contraignant.
  robots: { index: false, follow: true },
};

export const dynamic = "force-dynamic";

export default async function SignupPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const webSignupEnabled = isSignupEnabled();
  const hcaptchaSiteKey = process.env.HCAPTCHA_SITE_KEY?.trim() || null;
  const rawSearchParams = await searchParams;
  const selfServiceVpsContinuation = rawSearchParams.flow === "vps_self_service"
    ? resolveSelfServiceVpsSignupContinuation(rawSearchParams.next)
    : null;
  const selfServiceCartContinuation = rawSearchParams.flow === "cart_checkout"
    ? resolveSelfServiceCartSignupContinuation(rawSearchParams.next)
    : null;
  const selfServiceContinuation = selfServiceVpsContinuation ?? selfServiceCartContinuation;
  const billingV2Requested = rawSearchParams.v2 === "1";
  const billingV2Selection =
    readBillingV2SelectionSearchParams(rawSearchParams);
  const [billingV2Quote, billingV2CatalogResult, layoutResult, modeResult] = await Promise.all([
    billingV2Selection
      ? quoteBillingV2Formule(billingV2Selection, resolveCorrelationId(null)).catch(() => null)
      : Promise.resolve(null),
    billingV2Selection
      ? getBillingV2FormulesCatalog().catch(() => null)
      : Promise.resolve(null),
    getPublicSitePageLayout("/signup"),
    getPublicSignupMode(),
  ]);
  const enabled = webSignupEnabled && modeResult.data.enabled;
  const autoApprove = modeResult.data.autoApprove;
  const billingV2PresetName = billingV2Selection
    ? billingV2CatalogResult?.data.presets.find(
        (preset) => preset.code === billingV2Selection.presetCode,
      )?.name ?? null
    : null;

  const slots = {
    signup_intro: <>
      <Link className="back-link" href="/">
        <span aria-hidden="true">{"<-"}</span> Retour à l&apos;accueil
      </Link>

      <header className={`signup-header ${styles.header}`}>
        <p className="eyebrow">Inscription</p>
        <h1>Créer un compte client</h1>
        <p className="signup-lead">
          {selfServiceCartContinuation
            ? "Créez votre espace client pour finaliser votre commande et retrouver vos services."
            : selfServiceVpsContinuation
            ? "Créez votre accès client pour reprendre votre serveur à distance et vérifier votre commande avant le paiement."
            : "Renseignez vos informations, puis confirmez votre adresse e-mail. Vous recevrez ensuite les instructions pour ouvrir votre accès et reprendre votre offre."}
        </p>
      </header>
    </>,

    signup_continuation: selfServiceContinuation ? <>

      {selfServiceVpsContinuation ? (
        <section className={styles.stepsCard} aria-label="Reprise de votre serveur à distance">
          <p className="eyebrow">Votre serveur</p>
          <h2>Votre configuration sera conservée</h2>
          <p>
            Après la création du compte, vous reviendrez à votre configurateur
            serveur à distance pour relire le récapitulatif de votre commande avant le paiement.
          </p>
        </section>
      ) : null}

      {selfServiceCartContinuation ? (
        <section className={styles.stepsCard} aria-label="Reprise de votre panier">
          <p className="eyebrow">Votre panier</p>
          <h2>Votre sélection sera conservée</h2>
          <p>
            Après la création de votre compte, nous reprendrons votre panier puis
            vous pourrez vérifier son montant avant le paiement.
          </p>
        </section>
      ) : null}
    </> : null,

    signup_selection: billingV2Selection && billingV2Quote ? (
        <div className={styles.selectionStack}>
          <section className={styles.stepsCard} aria-label="Offre sélectionnée">
            <p className="eyebrow">Offre sélectionnée</p>
            <h2>{billingV2PresetName ?? "Votre offre"}</h2>
            <p>
              <strong>{formatCurrencyFromCents(billingV2Quote.monthlyAfterDiscountCents)} / mois</strong>
              {" - "}{billingV2Quote.commitmentMonths} mois,
              {billingV2Quote.paymentMode === "upfront" ? " paiement comptant" : " paiement mensuel"}.
            </p>
            <ul>
              {billingV2Quote.lines.map((line) => (
                <li key={`${line.serviceCode}-${line.tierCode ?? "base"}`}>
                  {resolveServicePublicLabel(line.serviceCode, line.label)}
                  {line.detail ? ` - ${resolveServicePublicDetail(line.serviceCode, line.detail)}` : ""}
                  {line.quantity > 1 ? ` x${line.quantity}` : ""}
                </li>
              ))}
            </ul>
            <p>
              Cette configuration est attachée à votre inscription. Aucun paiement
              n&apos;est effectué ici : après activation puis connexion, vous la retrouverez
              telle quelle avant le paiement sécurisé.
            </p>
          </section>
        </div>
      ) : billingV2Requested && (!billingV2Selection || !billingV2Quote) ? (
        <section className={styles.stepsCard} aria-label="Configuration invalide">
          <h2>Configuration à reprendre</h2>
          <p>L&apos;offre transmise ne peut pas être revalidée. Revenez au configurateur avant de créer le compte.</p>
          <Link className="button button-secondary" href="/formules">Reprendre mon offre</Link>
        </section>
      ) : null,

    signup_steps: <section className={styles.stepsCard} aria-label="Étapes d'ouverture">
        <h2>Ce qui se passe ensuite</h2>
        {selfServiceContinuation ? (
          <ol>
            <li>Vous créez votre accès client et choisissez votre mot de passe.</li>
            <li>Votre session client est ouverte immédiatement.</li>
            <li>{selfServiceCartContinuation
              ? "Nous reprenons votre panier et vous vérifiez votre commande avant le paiement."
              : "Vous reprenez le choix de votre serveur puis le récapitulatif de votre commande."}</li>
          </ol>
        ) : (
          <ol>
            <li>Vous confirmez votre adresse e-mail.</li>
            <li>{autoApprove
              ? "Votre accès est ouvert après cette confirmation et nous vous envoyons un lien pour choisir votre mot de passe."
              : "Notre équipe examine votre demande, puis vous envoie un lien pour choisir votre mot de passe après validation."}</li>
            <li>Vous choisissez votre mot de passe et ouvrez votre espace client.</li>
            <li>Vous finalisez ensuite votre offre depuis l&apos;espace client.</li>
          </ol>
        )}
      </section>,

    signup_form: enabled && (!billingV2Requested || (billingV2Selection && billingV2Quote)) ? (
        <SignupForm
          autoApprove={autoApprove}
          hcaptchaSiteKey={hcaptchaSiteKey}
          initialBillingV2Selection={billingV2Selection}
          selfServiceVps={selfServiceVpsContinuation}
          selfServiceCart={selfServiceCartContinuation}
        />
      ) : (
        <section className="signup-closed">
          <p>
            Les inscriptions en ligne ne sont pas ouvertes pour le moment. Pour
            toute demande d&apos;accès, contactez-nous via le{" "}
            <Link href="/contact">formulaire de contact</Link>.
          </p>
        </section>
      ),

    signup_login: <p className="login-help">
        Déjà client ?{" "}
        <Link
          href={selfServiceContinuation
            ? `/login?next=${encodeURIComponent(selfServiceContinuation.continuationPath)}`
            : "/login"}
        >
          Se connecter
        </Link>
      </p>,
  };
  return <div className={`signup-page ${styles.page}`}>
    <SitePageFrame area="public" pageKey="/signup" initialLayout={layoutResult.data} slots={slots}>{null}</SitePageFrame>
  </div>;
}
