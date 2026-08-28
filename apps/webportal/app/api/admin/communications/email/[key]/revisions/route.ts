import { NextRequest } from "next/server";

import { handleTemplateRevisions } from "@/lib/admin-communications-bff";

export async function GET(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  return handleTemplateRevisions(request, "email", (await params).key);
}
