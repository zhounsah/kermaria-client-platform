import { AssistanceHub } from "@/components/AssistancePages";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata = buildPublicMetadata({ title: "Assistance et maintenance informatique", description: "Découvrez les abonnements d’assistance et de maintenance informatique Zachary IT pour particuliers et professionnels.", path: "/assistance-maintenance" });

// Les prix de départ viennent du catalogue Billing V2 et doivent suivre ses publications.
export const dynamic = "force-dynamic";

export default function Page() { return <AssistanceHub />; }
