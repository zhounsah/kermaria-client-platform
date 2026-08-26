import Link from "next/link";
import { CatalogCreateForm } from "@/components/admin/catalog/CatalogCreateForm";
import { CatalogNavigation, adminCatalogStyles as styles } from "@/components/admin/catalog/AdminCatalogUi";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
export const dynamic = "force-dynamic";
export default async function Page() { await requireAdminSession(); return <div className={styles.shell}><PageHeader eyebrow="Catalogue · Service" title="Créer un service" description="Le service est créé de manière sûre avant la configuration de ses paliers et tarifs." action={<Link className="button button-secondary" href="/admin/catalog">Annuler</Link>} /><CatalogNavigation active="services" /><CatalogCreateForm kind="service" /></div>; }
