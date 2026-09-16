import { requireAdminSession } from "@/lib/auth";
import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";
export const metadata = { title: "Diagnostic - Administration" };

export default async function AdminDiagnosticPage() {
  await requireAdminSession();
  // L'éditeur adaptatif historique et ses API sont conservés pour leurs
  // consommateurs existants, mais ne pilotent plus /diagnostic. Cette URL ne
  // doit donc plus se présenter comme une autorité de configuration active.
  redirect("/admin/diagnostic");
}
