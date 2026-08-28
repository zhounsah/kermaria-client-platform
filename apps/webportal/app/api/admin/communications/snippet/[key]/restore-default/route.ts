import { NextRequest } from "next/server";

import { handleTemplateRestore } from "@/lib/admin-communications-bff";

export async function POST(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  return handleTemplateRestore(request, "snippet", (await params).key);
}
