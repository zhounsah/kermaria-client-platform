import { StatusBadge } from "@/components/StatusBadge";

type RequestAttentionBadgeProps = {
  hasRecentClientReply: boolean;
  requiresAttention: boolean;
};

export function RequestAttentionBadge({
  hasRecentClientReply,
  requiresAttention,
}: RequestAttentionBadgeProps) {
  if (hasRecentClientReply) {
    return <StatusBadge label="Réponse client" tone="warning" />;
  }

  if (requiresAttention) {
    return <StatusBadge label="À traiter" tone="info" />;
  }

  return <StatusBadge label="Suivi à jour" tone="success" />;
}
