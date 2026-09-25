import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ id: string }> };

// Reprise d'un compte principal sans identite AD : API-INTERNAL envoie au
// titulaire un lien de definition de mot de passe, puis audite la demande.
export async function POST(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  return handleAdminMutation(
    request,
    `/internal/admin/signups/${encodeURIComponent(id)}/identity-recovery`,
    "POST",
  );
}
