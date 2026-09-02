import type { Metadata } from "next";
import Link from "next/link";

import { requireClientSession } from "@/lib/auth";
import { getBillingV2VpsTechnicalRequestStatus } from "@/lib/internal-api";

export const metadata: Metadata = {
  title: "Commande VPS enregistrée",
  robots: { index: false, follow: false },
};

/** Le retour navigateur confirme seulement l'enregistrement chez Billing V2.
 *  Le settlement provider reste authoritative et la validation technique VPS
 *  précède toute mise en service. */
function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function isTechnicalRequestId(value: string | undefined) {
  return Boolean(value && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value));
}

export default async function VpsCheckoutConfirmationPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  await requireClientSession("/services/vps/choisir/confirmation");
  const params = await searchParams;
  const technicalRequestId = first(params.technicalRequestId)?.trim();
  const statusResult = isTechnicalRequestId(technicalRequestId)
    ? await getBillingV2VpsTechnicalRequestStatus(technicalRequestId!)
    : null;
  const status = statusResult?.data ?? null;
  const approved = status?.technicalStatus === "approved";
  const paid = status?.settlementStatus === "settled";
  const provisioning = status?.provisioningStatus;
  const title = provisioning === "active"
    ? "Votre VPS est en service"
    : provisioning === "provisioning"
      ? "Mise en service en cours"
      : approved
    ? "Configuration validée"
    : paid
      ? "Paiement reçu"
      : "Confirmation du paiement en cours";
  const description = provisioning === "active"
    ? "Votre VPS a été mis en service."
    : provisioning === "provisioning"
      ? "Notre équipe réalise actuellement la mise en service de votre VPS."
      : approved
    ? "Mise en service en préparation."
    : paid
      ? "Validation technique en cours."
      : "Nous vérifions la confirmation réelle de votre paiement.";

  return (
    <main className="services-page vps-configurator-page">
      <nav aria-label="Fil d’Ariane" className="service-breadcrumb">
        <Link href="/">Accueil</Link>
        <span aria-hidden="true">/</span>
        <Link href="/services">Services</Link>
        <span aria-hidden="true">/</span>
        <Link href="/services/vps">VPS</Link>
        <span aria-hidden="true">/</span>
        <span aria-current="page">Commande enregistrée</span>
      </nav>
      <section className="vps-configurator-panel" aria-labelledby="vps-checkout-confirmation-title">
        <div className="vps-configurator-panel-heading">
          <div>
            <p className="card-kicker">Commande enregistrée</p>
            <h1 id="vps-checkout-confirmation-title">{title}</h1>
            <p>{description}</p>
          </div>
        </div>
        <p className="vps-configurator-notice">
          Le retour navigateur n’est jamais une preuve de paiement : ce statut vient
          uniquement d’une vérification côté serveur.
        </p>
        <div className="vps-configurator-actions">
          <Link className="button" href="/profile/subscriptions">Voir mes souscriptions</Link>
          <Link className="button button-secondary" href="/services/vps">Retour aux offres VPS</Link>
        </div>
      </section>
    </main>
  );
}
