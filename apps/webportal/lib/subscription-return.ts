export type SubscriptionReturnRail = "paypal" | "stripe";

export type ReturnedSubscriptionLike = {
  id: string;
  paypalSubscriptionId?: string | null;
  stripeSubscriptionId?: string | null;
};

export function findReturnedSubscription(
  subscriptions: readonly ReturnedSubscriptionLike[],
  rail: SubscriptionReturnRail,
  providerSubscriptionId: string,
): ReturnedSubscriptionLike | null {
  const normalizedProviderSubscriptionId = providerSubscriptionId.trim();
  if (!normalizedProviderSubscriptionId) {
    return null;
  }

  return (
    subscriptions.find((subscription) =>
      rail === "stripe"
        ? subscription.stripeSubscriptionId === normalizedProviderSubscriptionId
        : subscription.paypalSubscriptionId === normalizedProviderSubscriptionId,
    ) ?? null
  );
}
