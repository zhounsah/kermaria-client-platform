import type { PublicRequestMessage } from "@kermaria/shared";

import { EmptyState } from "@/components/EmptyState";
import { formatDateTime } from "@/lib/formatters";

type PublicConversationProps = {
  messages: PublicRequestMessage[];
};

export function PublicConversation({ messages }: PublicConversationProps) {
  if (messages.length === 0) {
    return (
      <EmptyState
        description="Les messages visibles par le client apparaîtront dans cet espace."
        title="Aucun message public"
      />
    );
  }

  return (
    <ol className="public-conversation" aria-label="Conversation publique">
      {messages.map((message) => (
        <li
          className={`conversation-message conversation-message-${message.authorType}`}
          key={message.id}
        >
          <header>
            <strong>{message.authorLabel}</strong>
            <span>
              {message.authorType === "admin"
                ? "Support Kermaria"
                : "Réponse client"}
            </span>
          </header>
          <p>{message.message}</p>
          <time dateTime={message.createdAt}>
            {formatDateTime(message.createdAt)}
          </time>
        </li>
      ))}
    </ol>
  );
}
