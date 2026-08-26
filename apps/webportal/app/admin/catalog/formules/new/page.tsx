import Link from "next/link";
import { CatalogCreateForm } from "@/components/admin/catalog/CatalogCreateForm";
import { CatalogNavigation, adminCatalogStyles as styles } from "@/components/admin/catalog/AdminCatalogUi";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
export const dynamic = "force-dynamic";
export default async function Page() { await requireAdminSession(); return <div className={styles.shell}><PageHeader eyebrow="Catalogue · Formule" title="Créer une formule" description="Une formule compose des services sans stocker de prix." action={<Link className="button button-secondary" href="/admin/catalog?section=formules">Annuler</Link>} /><CatalogNavigation active="formules" /><CatalogCreateForm kind="formule" /></div>; }
