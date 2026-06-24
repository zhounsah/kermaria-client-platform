import Link from "next/link";

import { PageHeader } from "@/components/PageHeader";
import { requireClientSession } from "@/lib/auth";

export const metadata = {
  title: "Paiement confirmé",
};

export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function PaymentSuccessPage({ params }: PageProps) {
  await requireClientSession();
  const { id } = await params;

  return (
    <>
      <PageHeader
        description="Votre paiement a bien été reçu."
        eyebrow="Règlement"
        title="Facture réglée"
      />

      <section className="content-panel" aria-label="Confirmation de paiement">
        <div>
          <span className="card-kicker">Confirmation</span>
          <h2>Merci pour votre règlement</h2>
          <p>
            Votre paiement PayPal a été capturé avec succès. La facture est
            désormais marquée comme réglée.
          </p>
          <p style={{ marginTop: "0.5rem", color: "var(--color-text-muted)" }}>
            Un e-mail de confirmation vous sera envoyé si une adresse est
            associée à votre compte.
          </p>
        </div>
      </section>

      <div style={{ display: "flex", gap: "1rem", marginTop: "1.5rem" }}>
        <Link className="button" href={`/commercial-documents/${encodeURIComponent(id)}`}>
          Voir la facture
        </Link>
        <Link className="button button-secondary" href="/invoices">
          Retour aux documents
        </Link>
      </div>
    </>
  );
}
