import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

export function POST(request: NextRequest) {
  return handleAdminMutation(
    request,
    "/internal/admin/koxo/validate",
    "POST",
  );
}
