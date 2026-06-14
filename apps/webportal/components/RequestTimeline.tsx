import type {
  RequestEventSummary,
  RequestType,
} from "@kermaria/shared";

import {
  formatDateTime,
  serviceRequestStatus,
  supportStatus,
} from "@/lib/formatters";

type RequestTimelineProps = {
  events: RequestEventSummary[];
  requestType: RequestType;
};

type TimelineItem = {
  id: string;
  occurredAt: string;
  title: string;
};

export function RequestTimeline({
  events,
  requestType,
}: RequestTimelineProps) {
  const items: TimelineItem[] = events
    .filter((event) => event.eventType !== "public_message_added")
    .map((event, index) => ({
      id: `event-${event.occurredAt}-${index}`,
      occurredAt: event.occurredAt,
      title: eventTitle(requestType, event),
    }))
    .sort(
    (left, right) =>
      new Date(left.occurredAt).getTime()
      - new Date(right.occurredAt).getTime(),
  );

  return (
    <ol className="request-timeline">
      {items.map((item) => (
        <li className="timeline-item timeline-event" key={item.id}>
          <div className="timeline-marker" aria-hidden="true" />
          <div>
            <strong>{item.title}</strong>
            <time dateTime={item.occurredAt}>
              {formatDateTime(item.occurredAt)}
            </time>
          </div>
        </li>
      ))}
    </ol>
  );
}

function eventTitle(
  requestType: RequestType,
  event: RequestEventSummary,
) {
  if (event.eventType === "created") {
    return "Demande créée";
  }

  if (event.eventType === "internal_note_added") {
    return "Note interne ajoutée";
  }

  if (event.eventType === "public_message_added") {
    return "Message client ajouté";
  }

  const status = event.newStatus;
  if (!status) {
    return "Statut mis à jour";
  }

  const definition = requestType === "support"
    ? supportStatus[status as keyof typeof supportStatus]
    : serviceRequestStatus[status as keyof typeof serviceRequestStatus];
  return definition
    ? `Statut : ${definition.label}`
    : "Statut mis à jour";
}
