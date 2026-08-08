import Link from "next/link";

import { CartConfirmButton } from "@/components/CartConfirmButton";
import { CartItemRemoveButton } from "@/components/CartItemRemoveButton";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RecurringCheckoutConfirmButton } from "@/components/RecurringCheckoutConfirmButton";
import { RecurringCheckoutItemRemoveButton } from "@/components/RecurringCheckoutItemRemoveButton";
import { SectionHeading } from "@/components/SectionHeading";
import { requireClientSession } from "@/lib/auth";
import { formatCommercialAmountFromCents } from "@/lib/fiscal-formatters";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getCheckoutSummary } from "@/lib/internal-api";

export const metadata = {
  title: "Mon panier",
};

export const dynamic = "force-dynamic";

export default async function CartPage() {
  await requireClientSession();
  const checkoutResult = await getCheckoutSummary();
  const checkout = checkoutResult.data;
  const immediateTotal =
    checkout.cart.subtotalCents + checkout.recurring.subtotalCents;

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-ghost" href="/souscrire">
            Continuer mes achats
          </Link>
        }
        description="Un seul écran de récapitulatif, puis deux confirmations distinctes selon qu'il s'agit d'achats ponctuels ou d'abonnements récurrents."
        eyebrow="Panier unifié"
        title="Mon panier"
      />

      {checkoutResult.error ? (
        <ErrorState
          compact
          description="Impossible de charger votre panier pour le moment."
          reference={checkoutResult.correlationId}
          title="Panier indisponible"
        />
      ) : checkout.totalItemCount === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/souscrire">
              Découvrir les offres
            </Link>
          }
          description="Votre panier est vide. Ajoutez des achats ponctuels ou des abonnements depuis l'espace Souscrire."
          title="Panier vide"
        />
      ) : (
        <div className="checkout-page-layout">
          <div className="checkout-page-main">
            <section className="request-history-section">
              <SectionHeading
                description="Ces lignes seront regroupées dans une commande unique, puis payables par Stripe, PayPal ou virement bancaire."
                title="Achats ponctuels"
              />
              {checkout.cart.itemCount === 0 ? (
                <div className="content-panel">
                  <p className="request-description">
                    Aucun achat ponctuel n&apos;est actuellement présent dans votre
                    panier.
                  </p>
                </div>
              ) : (
                <div className="content-panel">
                  <table className="admin-table cart-table">
                    <thead>
                      <tr>
                        <th scope="col">Prestation</th>
                        <th scope="col">Qté</th>
                        <th scope="col">Prix unitaire</th>
                        <th scope="col">Total ligne</th>
                        <th scope="col">
                          <span className="visually-hidden">Actions</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {checkout.cart.items.map((item) => (
                        <tr key={item.offerId}>
                          <td>
                            <strong>{item.name}</strong>
                            <span className="cart-item-category">
                              {item.category}
                            </span>
                          </td>
                          <td>{item.quantity}</td>
                          <td>
                            {formatCommercialAmountFromCents(
                              item.unitPriceCents,
                              { fiscalRegime: item.fiscalRegime },
                            )}
                          </td>
                          <td>
                            {formatCommercialAmountFromCents(
                              item.lineTotalCents,
                              { fiscalRegime: item.fiscalRegime },
                            )}
                          </td>
                          <td>
                            <CartItemRemoveButton offerId={item.offerId} />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div className="checkout-bucket-footer">
                    <div>
                      <strong>Sous-total achats ponctuels</strong>
                      <p>
                        {checkout.cart.itemCount} article(s) pour{" "}
                        {formatCurrencyFromCents(checkout.cart.subtotalCents)}
                      </p>
                    </div>
                    <CartConfirmButton />
                  </div>
                </div>
              )}
            </section>

            <section className="request-history-section">
              <SectionHeading
                description="Ces sélections créent des souscriptions locales en attente de paiement, puis une facture initiale commune pour choisir ensuite Stripe, PayPal ou virement bancaire."
                title="Abonnements récurrents"
              />
              {checkout.recurring.itemCount === 0 ? (
                <div className="content-panel">
                  <p className="request-description">
                    Aucun abonnement récurrent n&apos;est actuellement sélectionné.
                  </p>
                </div>
              ) : (
                <div className="content-panel checkout-recurring-list">
                  {checkout.recurring.items.map((item) => (
                    <article className="checkout-recurring-item" key={item.offerId}>
                      <div className="checkout-recurring-item-copy">
                        <span className="card-kicker">
                          {item.category}
                          {item.publicPackCode ? " · Pack" : ""}
                        </span>
                        <h3>{item.name}</h3>
                        <p>{item.description}</p>
                        <dl className="checkout-recurring-facts">
                          <div>
                            <dt>Engagement</dt>
                            <dd>{item.commitmentMonths} mois</dd>
                          </div>
                          <div>
                            <dt>Facturation</dt>
                            <dd>
                              {item.paymentMode === "upfront"
                                ? `${formatCommercialAmountFromCents(
                                    item.priceAmountCents,
                                    { fiscalRegime: item.fiscalRegime },
                                  )} tous les ${item.billingIntervalMonths} mois`
                                : formatCommercialAmountFromCents(
                                    item.priceAmountCents,
                                    {
                                      fiscalRegime: item.fiscalRegime,
                                      suffix: " / mois",
                                    },
                                  )}
                            </dd>
                          </div>
                          <div>
                            <dt>Mise en service</dt>
                            <dd>
                              {formatCommercialAmountFromCents(
                                item.setupFeeAmountCents,
                                { fiscalRegime: item.fiscalRegime },
                              )}
                            </dd>
                          </div>
                          <div>
                            <dt>Total initial estimé</dt>
                            <dd>
                              {formatCommercialAmountFromCents(
                                item.firstChargeAmountCents,
                                { fiscalRegime: item.fiscalRegime },
                              )}
                            </dd>
                          </div>
                        </dl>
                      </div>
                      <div className="checkout-recurring-item-actions">
                        <strong>
                          {formatCommercialAmountFromCents(
                            item.firstChargeAmountCents,
                            { fiscalRegime: item.fiscalRegime },
                          )}
                        </strong>
                        <RecurringCheckoutItemRemoveButton offerId={item.offerId} />
                      </div>
                    </article>
                  ))}

                  <div className="checkout-bucket-footer">
                    <div>
                      <strong>Sous-total abonnements</strong>
                      <p>
                        {checkout.recurring.itemCount} abonnement(s) pour{" "}
                        {formatCurrencyFromCents(checkout.recurring.subtotalCents)}
                      </p>
                    </div>
                    <RecurringCheckoutConfirmButton />
                  </div>
                </div>
              )}
            </section>
          </div>

          <aside className="checkout-page-summary">
            <div className="checkout-summary-card">
              <span className="card-kicker">Récapitulatif</span>
              <h2>Total</h2>
              <dl className="checkout-summary-totals">
                <div>
                  <dt>Achats ponctuels</dt>
                  <dd>{formatCurrencyFromCents(checkout.cart.subtotalCents)}</dd>
                </div>
                <div>
                  <dt>Abonnements</dt>
                  <dd>{formatCurrencyFromCents(checkout.recurring.subtotalCents)}</dd>
                </div>
                <div className="checkout-summary-totals-grand">
                  <dt>Immédiat</dt>
                  <dd>{formatCurrencyFromCents(immediateTotal)}</dd>
                </div>
              </dl>
              <p>
                Les achats ponctuels et les abonnements restent visibles sur un
                seul écran, avec une validation adaptée à chaque tunnel.
              </p>
              <div className="checkout-summary-actions">
                <Link className="button button-ghost" href="/souscrire">
                  Continuer mes achats
                </Link>
                <Link className="button" href="/commercial-documents">
                  Voir mes factures
                </Link>
              </div>
            </div>
          </aside>
        </div>
      )}

      {checkoutResult.source !== "unavailable" ? (
        <MockNotice
          correlationId={checkoutResult.correlationId}
          source={checkoutResult.source}
        />
      ) : null}
    </>
  );
}
