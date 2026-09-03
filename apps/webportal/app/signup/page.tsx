import type { Metadata } from "next";
import Link from "next/link";

import { SignupForm } from "@/components/SignupForm";
import { readBillingV2SelectionSearchParams } from "@/lib/billing-v2-selection";
import {
  getBillingV2FormulesCatalog,
  quoteBillingV2Formule,
} from "@/lib/internal-api";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { resolveCorrelationId } from "@/lib/correlation";
import { isSignupEnabled } from "@/lib/public-routes";
import { resolveSelfServiceVpsSignupContinuation } from "@/lib/public-route-config";
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
  const enabled = isSignupEnabled();
  const hcaptchaSiteKey = process.env.HCAPTCHA_SITE_KEY?.trim() || null;
  const rawSearchParams = await searchParams;
  const selfServiceVpsContinuation = rawSearchParams.flow === "vps_self_service"
    ? resolveSelfServiceVpsSignupContinuation(rawSearchParams.next)
    : null;
  const billingV2Requested = rawSearchParams.v2 === "1";
  const billingV2Selection =
    readBillingV2SelectionSearchParams(rawSearchParams);
  const billingV2Quote = billingV2Selection
    ? await quoteBillingV2Formule(
        billingV2Selection,
        resolveCorrelationId(null),
      ).catch(() => null)
    : null;
  const billingV2CatalogResult = billingV2Selection
    ? await getBillingV2FormulesCatalog().catch(() => null)
    : null;
  const billingV2PresetName = billingV2Selection
    ? billingV2CatalogResult?.data.presets.find(
        (preset) => preset.code === billingV2Selection.presetCode,
      )?.name ?? null
    : null;

  return (
    <div className={`signup-page ${styles.page}`}>
      <Link className="back-link" href="/">
        <span aria-hidden="true">{"<-"}</span> Retour à l&apos;accueil
      </Link>

      <header className={`signup-header ${styles.header}`}>
        <p className="eyebrow">Inscription</p>
        <h1>Créer un compte client</h1>
        <p className="signup-lead">
          {selfServiceVpsContinuation
            ? "Créez votre accès client pour reprendre immédiatement la configuration et le paiement de votre VPS."
            : "Renseignez vos informations pour demander l'ouverture de votre accès client. Le parcours reste simple et assumé : confirmation de votre adresse e-mail, validation de votre demande par notre équipe, puis définition du mot de passe avant la finalisation de l'offre choisie."}
        </p>
      </header>

      {selfServiceVpsContinuation ? (
        <section className={styles.stepsCard} aria-label="Reprise de votre VPS">
          <p className="eyebrow">Votre VPS</p>
          <h2>Votre configuration sera conservée</h2>
          <p>
            Après la création du compte, vous reviendrez à votre configurateur
            VPS pour relire le récapitulatif de votre commande avant le paiement.
          </p>
        </section>
      ) : null}

      {billingV2Selection && billingV2Quote ? (
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
                  {line.label}{line.detail ? ` - ${line.detail}` : ""}
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
      ) : null}

      {billingV2Requested && (!billingV2Selection || !billingV2Quote) ? (
        <section className={styles.stepsCard} aria-label="Configuration invalide">
          <h2>Configuration à reprendre</h2>
          <p>L'offre transmise ne peut pas être revalidée. Revenez au configurateur avant de créer le compte.</p>
          <Link className="button button-secondary" href="/formules">Reprendre mon offre</Link>
        </section>
      ) : null}


      <section className={styles.stepsCard} aria-label="Étapes d'ouverture">
        <h2>Ce qui se passe ensuite</h2>
        {selfServiceVpsContinuation ? (
          <ol>
            <li>Vous créez votre accès client et choisissez votre mot de passe.</li>
            <li>Votre session client est ouverte immédiatement.</li>
            <li>Vous reprenez votre configurateur VPS puis le récapitulatif de votre commande.</li>
          </ol>
        ) : (
          <ol>
            <li>Vous confirmez votre adresse e-mail.</li>
            <li>Nous validons l&apos;ouverture de votre accès client.</li>
            <li>Vous définissez votre mot de passe et activez votre accès client.</li>
            <li>Vous finalisez ensuite votre offre depuis l&apos;espace client.</li>
          </ol>
        )}
      </section>

      {enabled && (!billingV2Requested || (billingV2Selection && billingV2Quote)) ? (
        <SignupForm
          hcaptchaSiteKey={hcaptchaSiteKey}
          initialBillingV2Selection={billingV2Selection}
          selfServiceVps={selfServiceVpsContinuation}
        />
      ) : (
        <section className="signup-closed">
          <p>
            Les inscriptions en ligne ne sont pas ouvertes pour le moment. Pour
            toute demande d&apos;accès, contactez-nous via le{" "}
            <Link href="/contact">formulaire de contact</Link>.
          </p>
        </section>
      )}

      <p className="login-help">
        Déjà client ?{" "}
        <Link
          href={selfServiceVpsContinuation
            ? `/login?next=${encodeURIComponent(selfServiceVpsContinuation.continuationPath)}`
            : "/login"}
        >
          Se connecter
        </Link>
      </p>
    </div>
  );
}
