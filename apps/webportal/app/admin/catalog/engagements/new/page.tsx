import Link from "next/link";
import { CatalogCreateForm } from "@/components/admin/catalog/CatalogCreateForm";
import { CatalogNavigation, adminCatalogStyles as styles } from "@/components/admin/catalog/AdminCatalogUi";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
export const dynamic = "force-dynamic";
export default async function Page() { await requireAdminSession(); return <div className={styles.shell}><PageHeader eyebrow="Catalogue · Engagement" title="Créer un engagement" description="Configurez ensuite les remises propres à chaque mode de règlement." action={<Link className="button button-secondary" href="/admin/catalog?section=engagements">Annuler</Link>} /><CatalogNavigation active="engagements" /><CatalogCreateForm kind="engagement" /></div>; }
