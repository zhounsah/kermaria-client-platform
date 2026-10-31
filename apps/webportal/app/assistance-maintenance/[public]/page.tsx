import { notFound } from "next/navigation";
import { AssistanceAudiencePage, type AssistanceAudience } from "@/components/AssistancePages";
import { buildPublicMetadata, CONTENT_UNAVAILABLE_ROBOTS } from "@/lib/public-metadata";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";

type Props = { params: Promise<{ public: string }> };
function audienceOf(value: string): AssistanceAudience | null { return value === "particuliers" || value === "professionnels" ? value : null; }
export async function generateMetadata({ params }: Props) {
  const audience = audienceOf((await params).public);
  if (!audience) return {};
  const result = await getBillingV2FormulesCatalog();
  const prefix = audience === "particuliers" ? "ASSIST-PERS-" : "ASSIST-PRO-";
  const available = result.data.source !== "unavailable" && result.data.services.some((service) => service.publicVisible && service.code.startsWith(prefix));
  return buildPublicMetadata({ title: `Assistance et maintenance informatique pour ${audience}`, description: `Découvrez les abonnements d’assistance et de maintenance informatique Zachary IT pour ${audience}.`, path: `/assistance-maintenance/${audience}`, ...(available ? {} : { robots: CONTENT_UNAVAILABLE_ROBOTS }) });
}
export default async function Page({ params }: Props) {
  const audience = audienceOf((await params).public);
  if (!audience) notFound();
  return <AssistanceAudiencePage audience={audience} />;
}
