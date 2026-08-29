import type { DemoContentTemplateMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

// Amorce unique : recopie les modeles du code dans la table. API-INTERNAL refuse
// si la table contient deja quelque chose — ce n'est pas une restauration.
export function POST(request: NextRequest) {
  return handleAdminMutation<undefined, DemoContentTemplateMutationResponse>(
    request,
    "/internal/admin/settings/demo-templates/import",
    "POST",
  );
}
