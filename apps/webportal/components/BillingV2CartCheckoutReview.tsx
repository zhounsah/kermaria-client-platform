"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { CheckCircle2, CircleAlert, LoaderCircle } from "lucide-react";

import type {
  AuthMeResponse,
  BillingV2Cart,
  BillingV2CartCheckoutStatusResponse,
  BillingV2CartQuote,
} from "@kermaria/shared";

import { BillingV2PricingSummary } from "@/components/BillingV2PricingSummary";
import {
  checkoutBillingV2CartClient,
  commandBillingV2CartClient,
  getBillingV2CartCheckoutStatusClient,
  notifyBillingV2CartChanged,
} from "@/lib/billing-v2-cart-client";
import { requestBffJson } from "@/lib/client-api";
import { resolveServicePublicLabel } from "@/lib/billing-v2-formules";

type State = "loading" | "anonymous" | "claiming" | "claim_conflict" | "empty" | "ready" | "complete" | "error";
/**
 * Revue courte avant que le serveur n'ouvre l'intention financière. Tous les
 * montants restent ceux du quote Cart retourné par API-INTERNAL.
 */
export function BillingV2CartCheckoutReview() {
  const [state, setState] = useState<State>("loading");
  const [cart, setCart] = useState<BillingV2Cart | null>(null);
  const [quote, setQuote] = useState<BillingV2CartQuote | null>(null);
  const [checkoutStatus, setCheckoutStatus] = useState<BillingV2CartCheckoutStatusResponse | null>(null);
  const [accepted, setAccepted] = useState(false);
  const [pending, setPending] = useState(false);
  const [emailVerificationRequired, setEmailVerificationRequired] = useState(false);
  const [resendingVerification, setResendingVerification] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const loadRecovery = useCallback(async (cartId?: string | null) => {
    const resumed = await getBillingV2CartCheckoutStatusClient(cartId);
    if (!resumed.ok) {
      if (resumed.error.code === "AUTH_REQUIRED") {
        return "auth_required";
      }
      if (resumed.error.code === "CART_CHECKOUT_AMBIGUOUS") {
        setMessage("Plusieurs souscriptions récentes sont disponibles. Ouvrez celle que vous souhaitez reprendre depuis vos souscriptions.");
        return "ambiguous";
      }
      return "not_found";
    }
    setCheckoutStatus(resumed.data);
    setState("complete");
    return "found";
  }, []);

  const load = useCallback(async () => {
    setAccepted(false);
    setMessage(null);
    const requestedCartId = readRequestedCartId();
    const authentication = await requestBffJson<AuthMeResponse>("/api/auth/me", { method: "GET" });
    const authenticated = authentication.ok && authentication.data.authenticated;

    if (authenticated) {
      const verification = await requestBffJson<{
        emailVerified: boolean;
        emailVerificationRequired: boolean;
      }>(
        "/api/auth/email-verification",
        { method: "GET" },
      );
      if (!verification.ok) {
        setMessage("La vérification de votre adresse e-mail est indisponible pour le moment.");
        setState("error");
        return;
      }
      setEmailVerificationRequired(verification.data.emailVerificationRequired);
    } else {
      setEmailVerificationRequired(false);
    }

    if (authenticated) {
      // Le BFF transmet encore le cookie Cart HttpOnly. Après login/signup,
      // cette primitive garde le même id et ne reconstruit jamais les items.
      setState("claiming");
      const claimed = await commandBillingV2CartClient({ command: "claim_current" });
      if (!claimed.ok && claimed.error.code === "CART_CLAIM_CONFLICT") {
        setState("claim_conflict");
        return;
      }
      // Sans ancien Cart anonyme, API-INTERNAL renvoie le succès explicite
      // CART_NOTHING_TO_CLAIM. Ce n'est pas une erreur de la session customer.
      if (!claimed.ok && claimed.error.code !== "CART_NOTHING_TO_CLAIM") {
        setMessage("Votre panier ne peut pas être repris pour le moment.");
        setState("error");
        return;
      }
    }

    const current = requestedCartId
      ? await commandBillingV2CartClient({ command: "get", cartId: requestedCartId })
      : await commandBillingV2CartClient({ command: "get_current", currency: "EUR" });
    if (!current.ok || !current.data.cart || current.data.cart.status !== "open") {
      if (!authenticated) {
        setState("empty");
        return;
      }
      const recovery = await loadRecovery(requestedCartId);
      if (recovery === "not_found") setState("empty");
      if (recovery === "auth_required") setState("anonymous");
      if (recovery === "ambiguous") setState("error");
      return;
    }
    setCheckoutStatus(null);
    const currentCart = current.data.cart;
    const quoted = await commandBillingV2CartClient({ command: "quote", cartId: currentCart.id });
    if (!quoted.ok || !quoted.data.quote) {
      setMessage("Le prix de votre panier doit être actualisé avant de poursuivre.");
      setState("error");
      return;
    }
    setCart(quoted.data.cart ?? currentCart);
    setQuote(quoted.data.quote);
    setState(authenticated ? "ready" : "anonymous");
  }, [loadRecovery]);

  useEffect(() => {
    let disposed = false;
    queueMicrotask(() => {
      if (!disposed) void load();
    });
    return () => { disposed = true; };
  }, [load]);

  useEffect(() => {
    if (checkoutStatus?.checkoutStatus !== "pending_provider"
      && checkoutStatus?.checkoutStatus !== "payment_pending") return;
    const interval = window.setInterval(() => {
      void loadRecovery(checkoutStatus.cartId);
    }, 5000);
    return () => window.clearInterval(interval);
  }, [checkoutStatus, loadRecovery]);

  const startCheckout = useCallback(async () => {
    if (!cart || !quote || !accepted || pending || emailVerificationRequired) return;
    setPending(true);
    setMessage(null);
    // The Cart id is already known before the mutation. Persist it in the
    // navigation state first so a lost HTTP response or an immediate refresh
    // can resume this exact checkout instead of relying on a "latest" lookup.
    window.history.replaceState({}, "", `/souscription?cart=${encodeURIComponent(cart.id)}`);
    const result = await checkoutBillingV2CartClient({
      cartId: cart.id,
      expectedCartVersion: quote.cartVersion,
      acceptedQuoteVersion: quote.quoteVersion,
      acceptedCompositionFingerprint: quote.compositionFingerprint,
    });
    setPending(false);
    if (!result.ok) {
      if (result.error.code === "AUTH_REQUIRED") {
        setAccepted(false);
        setState("anonymous");
        return;
      }
      if (result.error.code === "EMAIL_VERIFICATION_REQUIRED") {
        setAccepted(false);
        setMessage("Vérifiez votre adresse e-mail pour continuer vers le paiement. Nous avons envoyé un lien de vérification à votre adresse e-mail.");
        return;
      }
      if (result.error.code === "CART_QUOTE_CHANGED" || result.error.code === "CART_QUOTE_EXPIRED") {
        setMessage("Le prix de votre panier a été mis à jour. Vérifiez le nouveau montant avant de poursuivre.");
        setAccepted(false);
        await load();
        return;
      }
      setMessage("La souscription ne peut pas démarrer pour le moment. Votre panier n’a pas été modifié.");
      return;
    }
    notifyBillingV2CartChanged();
    if (result.data.approvalUrl) {
      window.location.assign(result.data.approvalUrl);
      return;
    }
    if (await loadRecovery(cart.id) !== "found") {
      setMessage("Votre demande a été enregistrée. Le paiement est en cours de préparation.");
      setState("complete");
    }
  }, [accepted, cart, emailVerificationRequired, load, loadRecovery, pending, quote]);

  const resendVerification = useCallback(async () => {
    if (resendingVerification) return;
    setResendingVerification(true);
    setMessage(null);
    const result = await requestBffJson<{ code: string; message: string }>(
      "/api/auth/email-verification",
      { method: "POST" },
    );
    setResendingVerification(false);
    setMessage(result.ok
      ? result.data.message
      : "Un lien vient déjà d’être envoyé. Réessayez plus tard si nécessaire.");
  }, [resendingVerification]);

  if (state === "loading" || state === "claiming") return <main className="content-section"><p>{state === "claiming" ? "Nous récupérons votre panier…" : "Préparation de votre souscription…"}</p></main>;
  if (state === "empty") return <main className="content-section"><h1>Votre panier est vide</h1><p>Ajoutez un service ou une offre avant de poursuivre.</p><Link className="button" href="/panier">Voir mon panier</Link></main>;
  if (state === "claim_conflict") return <main className="content-section"><h1>Vos paniers doivent être vérifiés</h1><p>Un autre panier est déjà associé à votre compte. Votre sélection actuelle n’a pas été modifiée.</p><Link className="button button-secondary" href="/panier">Voir mon panier</Link></main>;
  if (state === "complete" && checkoutStatus) {
    return <CheckoutRecoveryStatus status={checkoutStatus} />;
  }
  if (!cart || !quote) return <main className="content-section"><h1>Votre souscription</h1><p>{message ?? "La vérification du panier est indisponible."}</p><Link className="button" href="/panier">Retour au panier</Link></main>;

  return <section className="cart-storefront subscription-review" aria-labelledby="subscription-review-title">
    <header className="cart-storefront-heading subscription-review-heading">
      <p className="eyebrow">Dernière vérification</p>
      <h1 id="subscription-review-title">Vérifier votre abonnement</h1>
      <p>Vérifiez votre abonnement avant de poursuivre vers le paiement.</p>
    </header>

    <div className="cart-storefront-layout subscription-review-layout">
      <div className="cart-storefront-items subscription-review-details">
        <section className="cart-storefront-item subscription-review-card" aria-labelledby="subscription-services-title">
          <div>
            <h2 id="subscription-services-title">Votre abonnement</h2>
            <p className="cart-storefront-item-detail">Les services ci-dessous seront inclus dans votre abonnement.</p>
          </div>
          <ul className="subscription-review-service-list">
            {quote.lines.map((line) => (
              <li key={`${line.cartItemId}-${line.servicePriceId}`}>
                <span>
                  {resolveServicePublicLabel(line.serviceCode, line.label || "Service inclus")}
                  {line.detail ? <em>{line.detail}</em> : null}
                  {line.quantity > 1 ? <em>Quantité : {line.quantity}</em> : null}
                </span>
              </li>
            ))}
          </ul>
        </section>

        <section className="cart-storefront-item subscription-review-card" aria-labelledby="subscription-terms-title">
          <h2 id="subscription-terms-title">Vos choix</h2>
          <dl className="subscription-review-terms">
            <div>
              <dt>Engagement</dt>
              <dd>{formatCommitmentLabel(cart.commitmentCode)}</dd>
            </div>
            <div>
              <dt>Mode de paiement</dt>
              <dd>{cart.paymentMode === "upfront" ? "Paiement en une fois" : "Mensuel"}</dd>
            </div>
          </dl>
          <Link className="button button-secondary subscription-review-edit" href="/panier">Modifier le panier</Link>
        </section>
      </div>

      <aside className="cart-storefront-summary subscription-review-summary" aria-live="polite" aria-labelledby="subscription-summary-title">
        <h2 id="subscription-summary-title">Récapitulatif</h2>
        <BillingV2PricingSummary
          currency={quote.currency}
          lines={quote.lines.map((line) => ({
            id: `${line.cartItemId}-${line.servicePriceId}`,
            label: resolveServicePublicLabel(line.serviceCode, line.label || "Service inclus"),
            detail: line.detail,
            quantity: line.quantity,
            amountCents: line.amountCents,
          }))}
          oneTimeDueNowCents={quote.oneTimeDueNowCents}
          recurringDiscountCents={quote.recurringDiscountCents}
          recurringSubtotalCents={quote.recurringSubtotalCents}
          recurringTotalCents={quote.recurringTotalCents}
          totalDueNowCents={quote.totalDueNowCents}
        />
        {state === "anonymous" ? (
          <section className="subscription-review-account-step" aria-labelledby="subscription-account-step-title">
            <h3 id="subscription-account-step-title">Finaliser votre commande</h3>
            <p>Créez votre espace client pour finaliser votre commande et retrouver vos services.</p>
            <Link className="button" href={buildAccountContinuation("signup", cart.id)}>Créer mon compte et continuer</Link>
            <p className="subscription-review-login-link">Vous avez déjà un compte ? <Link href={buildAccountContinuation("login", cart.id)}>Se connecter</Link></p>
          </section>
        ) : emailVerificationRequired ? (
          <section className="subscription-review-account-step" aria-labelledby="subscription-email-verification-title">
            <h3 id="subscription-email-verification-title">Vérifiez votre adresse e-mail</h3>
            <p>Nous avons envoyé un lien de vérification à votre adresse. Validez-la pour continuer vers le paiement.</p>
            {message ? <p className="cart-storefront-error" role="status">{message}</p> : null}
            <button className="button button-secondary" disabled={resendingVerification} onClick={() => void resendVerification()} type="button">
              {resendingVerification ? "Envoi en cours…" : "Renvoyer l’e-mail de vérification"}
            </button>
            <p className="subscription-review-login-link"><Link href="/panier">Modifier le panier</Link></p>
          </section>
        ) : (
          <>
            <label className="subscription-review-confirmation">
              <input checked={accepted} onChange={(event) => setAccepted(event.target.checked)} type="checkbox" />
              <span>
                <strong>Je confirme la composition et le prix affichés.</strong>
              </span>
            </label>
            {message ? <p className="cart-storefront-error" role="alert">{message}</p> : null}
            <div className="subscription-review-actions">
              <button className="button" disabled={!accepted || pending} onClick={() => void startCheckout()} type="button">
                {pending ? "Préparation…" : "Continuer vers le paiement"}
              </button>
              <Link className="button button-secondary" href="/panier">Modifier le panier</Link>
            </div>
          </>
        )}
      </aside>
    </div>
  </section>;
}

function formatCommitmentLabel(commitmentCode: string | null) {
  if (commitmentCode === "FLEX") return "Sans engagement";
  const months = /^TERM-(\d+)$/.exec(commitmentCode ?? "")?.[1];
  return months ? `${months} mois` : "Durée sélectionnée";
}

function readRequestedCartId() {
  const cartId = new URLSearchParams(window.location.search).get("cart");
  return cartId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(cartId)
    ? cartId
    : null;
}

function buildAccountContinuation(kind: "signup" | "login", cartId: string) {
  const next = `/souscription?cart=${encodeURIComponent(cartId)}`;
  return kind === "signup"
    ? `/signup?flow=cart_checkout&next=${encodeURIComponent(next)}`
    : `/login?next=${encodeURIComponent(next)}`;
}

function CheckoutRecoveryStatus({ status }: { status: BillingV2CartCheckoutStatusResponse }) {
  const content = status.checkoutStatus === "approval_required" && status.approvalUrl
    ? {
      tone: "ready",
      title: "Votre paiement est prêt",
      description: "Vous pouvez reprendre le paiement en toute sécurité.",
      action: <a className="button" href={status.approvalUrl}>Poursuivre le paiement</a>,
    }
    : status.checkoutStatus === "payment_pending"
      ? {
        tone: "pending",
        title: "Paiement en attente",
        description: "Nous attendons la confirmation du paiement. Cette page s’actualise automatiquement.",
        action: null,
      }
      : status.checkoutStatus === "confirmed"
        ? {
          tone: "success",
          title: "Paiement confirmé",
          description: "Votre souscription est enregistrée.",
          action: <Link className="button" href="/profile/subscriptions">Voir mes souscriptions</Link>,
        }
        : status.checkoutStatus === "failed"
          ? {
            tone: "error",
            title: "Préparation du paiement interrompue",
            description: "Votre souscription existe déjà. Contactez-nous si cet état persiste.",
            action: <Link className="button button-secondary" href="/profile/subscriptions">Voir mes souscriptions</Link>,
          }
          : {
            tone: "pending",
            title: "Préparation du paiement",
            description: "Votre souscription est enregistrée. Nous préparons votre paiement.",
            action: null,
          };
  const icon = content.tone === "success"
    ? <CheckCircle2 aria-hidden="true" />
    : content.tone === "error"
      ? <CircleAlert aria-hidden="true" />
      : <LoaderCircle aria-hidden="true" className="subscription-status-spinner" />;
  return <section className="subscription-status-page" aria-live="polite">
    <div className={`subscription-status-card subscription-status-${content.tone}`}>
      <span className="subscription-status-icon">{icon}</span>
      <p className="eyebrow">Souscription</p>
      <h1>{content.title}</h1>
      <p>{content.description}</p>
      {content.action ? <div className="subscription-status-actions">{content.action}</div> : null}
      {content.tone === "pending" ? <span className="sr-only">Actualisation en cours.</span> : null}
    </div>
  </section>;
}
