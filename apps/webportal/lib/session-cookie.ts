import "server-only";

import { cookies } from "next/headers";

import { getSessionCookieName } from "@/lib/session-config";

export async function readPortalSessionToken() {
  const cookieStore = await cookies();
  return cookieStore.get(getSessionCookieName())?.value ?? null;
}
