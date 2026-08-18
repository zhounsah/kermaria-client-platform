import Link from "next/link";
import { notFound } from "next/navigation";

import { BillingV2AdditionalUsersManager } from "@/components/BillingV2AdditionalUsersManager";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireClientSession } from "@/lib/auth";
import {
  getBillingV2AdditionalUsers,
  getClientSubscriptions,
} from "@/lib/internal-api";

export const metadata = {
  title: "Utilisateurs supplémentaires",
};

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ id: string }>;
};

export default async function SubscriptionAdditionalUsersPage({
  params,
}: PageProps) {
  await requireClientSession();
  const { id } = await params;

  // Les deux lectures sont bornées au client par la session côté API : le
  // navigateur ne transmet aucun identifiant de client. Une souscription
  // d'une autre organisation se comporte donc comme une souscription
  // inexistante.
  const [subscriptionsResult, slotsResult] = await Promise.all([
    getClientSubscriptions(),
    getBillingV2AdditionalUsers(id),
  ]);

  if (subscriptionsResult.error) {
    return (
      <ErrorState
        action={
          <Link className="button" href="/profile/subscriptions">
            Retour aux souscriptions
          </Link>
        }
        description="Impossible de charger cette souscription."
        reference={subscriptionsResult.correlationId}
        title="Souscription indisponible"
      />
    );
  }

  const subscription = subscriptionsResult.data.find(
    (item) => item.id === id,
  );

  if (!subscription) {
    notFound();
  }

  if (slotsResult.error) {
    return (
      <ErrorState
        action={
          <Link className="button" href="/profile/subscriptions">
            Retour aux souscriptions
          </Link>
        }
        description="Impossible de charger les utilisateurs de cette souscription."
        reference={slotsResult.correlationId}
        title="Utilisateurs indisponibles"
      />
    );
  }

  const slots = slotsResult.data;
  const assigned = slots.filter((slot) => slot.status !== "available").length;

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-secondary" href="/profile/subscriptions">
            Retour aux souscriptions
          </Link>
        }
        description={`Souscription ${subscription.offerName}. Chaque place ouvre un accès nominatif à votre espace client et aux services associés.`}
        eyebrow="Mes souscriptions"
        title="Utilisateurs supplémentaires"
      />

      {slots.length === 0 ? (
        <EmptyState
          description="Cette souscription n'ouvre aucune place d'utilisateur supplémentaire. Contactez notre équipe pour en ajouter."
          title="Aucune place à configurer"
        />
      ) : (
        <section className="content-panel stack-panel">
          <p className="field-hint">
            {assigned} place{assigned > 1 ? "s" : ""} configurée
            {assigned > 1 ? "s" : ""} sur {slots.length}.
          </p>
          <BillingV2AdditionalUsersManager
            slots={slots}
            subscriptionId={subscription.id}
          />
        </section>
      )}
    </>
  );
}
