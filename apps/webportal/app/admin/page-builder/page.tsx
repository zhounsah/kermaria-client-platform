import { AdminPageBuilder } from "@/components/AdminPageBuilder";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";

export const metadata = { title: "Contenus du site - Administration" };
export const dynamic = "force-dynamic";

export default async function PageBuilderPage() {
  await requireAdminSession();
  return <>
    <PageHeader title="Contenus du site" description="Organisez les pages et publiez vos changements sans modifier le code." eyebrow="Administration interne" />
    <AdminPageBuilder />
  </>;
}
