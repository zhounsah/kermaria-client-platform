import Link from "next/link";

import { CartConfirmButton } from "@/components/CartConfirmButton";
import { CartItemRemoveButton } from "@/components/CartItemRemoveButton";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { requireClientSession } from "@/lib/auth";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getCart } from "@/lib/internal-api";

export const metadata = {
  title: "Mon panier",
};

export const dynamic = "force-dynamic";

export default async function CartPage() {
  await requireClientSession();
  const cartResult = await getCart();
  const cart = cartResult.data;

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-ghost" href="/souscrire">
            Continuer mes achats
          </Link>
        }
        description="Vérifiez la synthèse de vos options à la carte, puis confirmez pour régler en une fois (Stripe, PayPal ou virement bancaire)."
        eyebrow="Commande à la carte"
        title="Mon panier"
      />

      {cartResult.error ? (
        <ErrorState
          compact
          description="Impossible de charger votre panier pour le moment."
          reference={cartResult.correlationId}
          title="Panier indisponible"
        />
      ) : cart.items.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/souscrire">
              Découvrir les options
            </Link>
          }
          description="Votre panier est vide. Ajoutez des options à la carte depuis l'espace « Souscrire »."
          title="Panier vide"
        />
      ) : (
        <>
          <section className="request-history-section">
            <SectionHeading
              description="Chaque option retenue apparaît ci-dessous. Vous pouvez ajuster votre sélection avant de confirmer."
              title="Synthèse de la commande"
            />
            <div className="content-panel">
              <table className="admin-table cart-table">
                <thead>
                  <tr>
                    <th scope="col">Prestation</th>
                    <th scope="col">Qté</th>
                    <th scope="col">Prix unitaire HT</th>
                    <th scope="col">Total ligne HT</th>
                    <th scope="col">
                      <span className="visually-hidden">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {cart.items.map((item) => (
                    <tr key={item.offerId}>
                      <td>
                        <strong>{item.name}</strong>
                        <span className="cart-item-category">
                          {item.category}
                        </span>
                      </td>
                      <td>{item.quantity}</td>
                      <td>{formatCurrencyFromCents(item.unitPriceCents)}</td>
                      <td>{formatCurrencyFromCents(item.lineTotalCents)}</td>
                      <td>
                        <CartItemRemoveButton offerId={item.offerId} />
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <th colSpan={3} scope="row">
                      Total HT
                    </th>
                    <td colSpan={2}>
                      <strong>
                        {formatCurrencyFromCents(cart.subtotalCents)}
                      </strong>
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </section>

          <section className="request-history-section">
            <SectionHeading
              description="En confirmant, une commande unique regroupant toutes ces prestations est créée. Vous choisissez ensuite votre moyen de paiement : carte bancaire (Stripe), PayPal ou virement bancaire. Le service est activé automatiquement dès le paiement validé, le cas échéant."
              title="Confirmer et payer"
            />
            <div className="content-panel">
              <CartConfirmButton />
            </div>
          </section>
        </>
      )}

      {cartResult.source !== "unavailable" ? (
        <MockNotice
          correlationId={cartResult.correlationId}
          source={cartResult.source}
        />
      ) : null}
    </>
  );
}
