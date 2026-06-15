import type { PublicRequestMessage } from "@kermaria/shared";

import { RequestAttentionBadge } from "@/components/RequestAttentionBadge";
import { SectionCard } from "@/components/SectionCard";
import { formatDateTime } from "@/lib/formatters";

type AdminRequestFollowUpProps = {
  messages: PublicRequestMessage[];
  requiresAttention: boolean;
};

export function AdminRequestFollowUp({
  messages,
  requiresAttention,
}: AdminRequestFollowUpProps) {
  const latestClientReply = [...messages]
    .reverse()
    .find((message) => message.authorType === "client");
  const latestMessage = messages.at(-1);
  const hasRecentClientReply =
    latestMessage?.authorType === "client";

  return (
    <SectionCard
      ariaLabel="Suivi de la réponse client"
      className="admin-follow-up"
    >
      <div className="section-heading">
        <div>
          <h2>Suivi administrateur</h2>
          <p>
            Les réponses client sont visibles dans la conversation publique.
            Les notes internes restent privées.
          </p>
        </div>
        <RequestAttentionBadge
          hasRecentClientReply={hasRecentClientReply}
          requiresAttention={requiresAttention || hasRecentClientReply}
        />
      </div>
      {latestClientReply ? (
        <p className="admin-follow-up-detail">
          Dernière réponse client :{" "}
          <strong>{formatDateTime(latestClientReply.createdAt)}</strong>
        </p>
      ) : (
        <p className="admin-follow-up-detail">
          Aucune réponse client n’est encore enregistrée.
        </p>
      )}
    </SectionCard>
  );
}
