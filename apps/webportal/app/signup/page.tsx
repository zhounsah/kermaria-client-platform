import type { Metadata } from "next";
import Link from "next/link";

import { PublicPackSelectionSummary } from "@/components/PublicPackSelectionSummary";
import { SignupForm } from "@/components/SignupForm";
import { readBillingV2SelectionSearchParams } from "@/lib/billing-v2-selection";
import { resolveCatalogConfiguration } from "@/lib/catalog-configuration-server";
import {
  getPublicCommercialCatalog,
  getBillingV2FormulesCatalog,
  quoteBillingV2Formule,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import {
  configurationFromSearchParams,
  configurationFromSelection,
} from "@/lib/public-configurator";
import {
  buildSignupPackSnapshot,
  selectionFromSearchParams,
} from "@/lib/public-packs";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { resolveCorrelationId } from "@/lib/correlation";
import { isSignupEnabled } from "@/lib/public-routes";
import styles from "./page.module.css";

export const metadata: Metadata = {
  title: "Créer un compte",
  description:
    "Demandez l'ouverture de votre accès client et reprenez, si besoin, le pack déjà sélectionné sur la vitrine.",
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
  const catalogConfiguration =
    billingV2Requested ? null : configurationFromSearchParams(rawSearchParams);
  const selection =
    catalogConfiguration
      ? {
          packKey: catalogConfiguration.packKey,
          commitmentMonths: catalogConfiguration.commitmentMonths,
          paymentMode: catalogConfiguration.paymentMode,
        }
      : selectionFromSearchParams(rawSearchParams);
  const configurationResult = catalogConfiguration
    ? await resolveCatalogConfiguration(catalogConfiguration)
    : null;
  const resolvedConfiguration =
    configurationResult?.ok
    && configurationResult.data.status === "ok"
    && configurationResult.data.resolvedConfiguration
      ? configurationResult.data.resolvedConfiguration
      : !catalogConfiguration && selection
        ? configurationFromSelection(selection)
        : null;
  const [catalogResult, packContentResult] = selection && !catalogConfiguration
    ? await Promise.all([
        getPublicCommercialCatalog(),
        getPublicPackCatalogContent(),
      ])
    : [null, null];
  const packSelection =
    configurationResult?.ok
    && configurationResult.data.status === "ok"
    && configurationResult.data.packSelection
      ? configurationResult.data.packSelection
      : selection && catalogResult
        ? buildSignupPackSnapshot(
            catalogResult.data,
            selection,
            packContentResult?.data ?? null,
          )
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
          Renseignez vos informations pour demander l&apos;ouverture de votre accès
          client. Le parcours reste simple et assumÉ : confirmation de votre
          adresse e-mail, validation de votre demande par notre équipe, puis
          définition du mot de passe avant la finalisation du pack choisi.
        </p>
      </header>

      {billingV2Selection && billingV2Quote ? (
        <div className={styles.selectionStack}>
          <section className={styles.stepsCard} aria-label="Formule Billing V2 sélectionnée">
            <p className="eyebrow">Formule sélectionnée</p>
            <h2>{billingV2PresetName ?? "Votre formule"}</h2>
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
              telle quelle avant le passage chez Stripe.
            </p>
          </section>
        </div>
      ) : null}

      {billingV2Requested && (!billingV2Selection || !billingV2Quote) ? (
        <section className={styles.stepsCard} aria-label="Configuration Billing V2 invalide">
          <h2>Configuration à reprendre</h2>
          <p>La formule transmise ne peut pas être revalidée. Revenez au configurateur avant de créer le compte.</p>
          <Link className="button button-secondary" href="/formules">Reprendre ma formule</Link>
        </section>
      ) : null}


      {packSelection ? (
        <div className={styles.selectionStack}>
          <PublicPackSelectionSummary
            commitmentMonths={packSelection.commitmentMonths}
            description="Le pack sélectionné reste attaché à cette demande. Le paiement ne se fait pas sur cet écran : vous retrouverez ensuite ce contexte dans l'espace client."
            eyebrow="Pack repris"
            fiscalMention={packSelection.fiscalMention}
            fiscalRegime={packSelection.fiscalRegime}
            firstChargeAmountCents={packSelection.firstChargeAmountCents}
            monthlyPriceAmountCents={packSelection.monthlyPriceAmountCents}
            packLabel={packSelection.packLabel}
            paymentMode={packSelection.paymentMode}
            setupFeeAmountCents={packSelection.setupFeeAmountCents}
          />
          <section className={styles.stepsCard} aria-label="Étapes d'ouverture">
            <h2>Ce qui se passe ensuite</h2>
            <ol>
              <li>Vous confirmez votre adresse e-mail.</li>
              <li>Nous validons l&apos;ouverture de votre accès client.</li>
              <li>Vous définissez votre mot de passe et activez votre accès client.</li>
              <li>Si l&apos;écriture AD est active, l&apos;identité clients.home.bzh est finalisée à ce moment-là.</li>
              <li>Vous finalisez ensuite le pack depuis l&apos;espace client.</li>
            </ol>
          </section>
        </div>
      ) : null}

      {catalogConfiguration && configurationResult && !configurationResult.ok ? (
        <section className={styles.stepsCard} aria-label="Configuration indisponible">
          <h2>Configuration à vérifier</h2>
          <p>
            La configuration transmise n&apos;a pas pu être recalculée pour
            l&apos;instant. Revenez au configurateur pour obtenir une estimation
            à jour avant de poursuivre.
          </p>
          <Link className="button button-secondary" href="/configurer">
            Reprendre la configuration
          </Link>
        </section>
      ) : null}

      {enabled && (!billingV2Requested || (billingV2Selection && billingV2Quote)) ? (
        <SignupForm
          hcaptchaSiteKey={hcaptchaSiteKey}
          initialBillingV2Selection={billingV2Selection}
          initialPackSelection={packSelection
            ? {
                packKey: packSelection.packKey,
                packLabel: packSelection.packLabel,
                commitmentMonths: packSelection.commitmentMonths,
                paymentMode: packSelection.paymentMode,
                monthlyPriceAmountCents: packSelection.monthlyPriceAmountCents,
                setupFeeAmountCents: packSelection.setupFeeAmountCents,
                firstChargeAmountCents: packSelection.firstChargeAmountCents,
                fiscalRegime: packSelection.fiscalRegime,
                fiscalMention: packSelection.fiscalMention,
              }
            : null}
          initialCatalogConfiguration={resolvedConfiguration}
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
    </div>
  );
}
