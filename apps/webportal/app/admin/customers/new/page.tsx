import { AdminCustomerCreateForm } from "@/components/AdminCustomerCreateForm";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";

export const metadata = { title: "Nouveau client - Administration" };

export default async function AdminNewCustomerPage() {
  await requireAdminSession();
  return <>
    <PageHeader
      eyebrow="Administration interne"
      title="Nouveau client"
      description="Créez une fiche client sans créer d’accès au portail."
    />
    <AdminCustomerCreateForm />
  </>;
}
