import { redirect } from "next/navigation";

import { AccessDenied } from "@/components/AccessDenied";
import { requireAuth } from "@/lib/auth";

export const metadata = {
  title: "Accès refusé",
};

export const dynamic = "force-dynamic";

export default async function AccessDeniedPage() {
  const session = await requireAuth();

  if (session.user.role === "internal_admin") {
    redirect("/admin");
  }

  return <AccessDenied />;
}
