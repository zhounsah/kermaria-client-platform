import { AssistanceHub } from "@/components/AssistancePages";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata = buildPublicMetadata({ title: "Assistance et maintenance informatique", description: "Découvrez les abonnements d’assistance et de maintenance informatique Zachary IT pour particuliers et professionnels.", path: "/assistance-maintenance" });

export default function Page() { return <AssistanceHub />; }
