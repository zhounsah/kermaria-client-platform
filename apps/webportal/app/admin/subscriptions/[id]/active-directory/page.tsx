import { notFound, redirect } from "next/navigation";

import { requireAdminSession } from "@/lib/auth";
import { getAdminSubscription } from "@/lib/internal-api";

export const dynamic = "force-dynamic";

export default async function AdminSubscriptionActiveDirectoryProjectionPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireAdminSession();
  const { id } = await params;
  const result = await getAdminSubscription(id);

  if (!result.data) {
    notFound();
  }

  redirect(
    `/admin/customers/${encodeURIComponent(result.data.subscription.customerReference)}/active-directory?subscriptionId=${encodeURIComponent(result.data.subscription.id)}`,
  );
}
