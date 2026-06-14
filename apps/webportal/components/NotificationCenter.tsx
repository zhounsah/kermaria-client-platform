"use client";

import type {
  NotificationReadResponse,
  PortalNotificationSummary,
} from "@kermaria/shared";
import Link from "next/link";
import { useRef, useState } from "react";

import { EmptyState } from "@/components/EmptyState";
import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import { formatDateTime } from "@/lib/formatters";

type NotificationCenterProps = {
  initialNotifications: PortalNotificationSummary[];
};

export function NotificationCenter({
  initialNotifications,
}: NotificationCenterProps) {
  const [notifications, setNotifications] = useState(initialNotifications);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [isMarkingAll, setIsMarkingAll] = useState(false);
  const [feedback, setFeedback] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const mutationInProgress = useRef(false);
  const unreadCount = notifications.filter((item) => !item.isRead).length;

  async function markAsRead(id: string) {
    if (mutationInProgress.current) {
      return;
    }

    mutationInProgress.current = true;
    setPendingId(id);
    setFeedback(null);
    const result = await requestBffJson<NotificationReadResponse>(
      `/api/notifications/${encodeURIComponent(id)}/read`,
      { method: "POST" },
    );

    if (result.ok) {
      const readAt = new Date().toISOString();
      setNotifications((current) =>
        current.map((item) =>
          item.id === id ? { ...item, isRead: true, readAt } : item,
        ),
      );
      setFeedback({
        tone: "success",
        text: "La notification a été marquée comme lue.",
      });
    } else {
      setFeedback({ tone: "error", text: result.error.message });
    }

    mutationInProgress.current = false;
    setPendingId(null);
  }

  async function markAllAsRead() {
    if (mutationInProgress.current || unreadCount === 0) {
      return;
    }

    mutationInProgress.current = true;
    setIsMarkingAll(true);
    setFeedback(null);
    const result = await requestBffJson<NotificationReadResponse>(
      "/api/notifications/read-all",
      { method: "POST" },
    );

    if (result.ok) {
      const readAt = new Date().toISOString();
      setNotifications((current) =>
        current.map((item) =>
          item.isRead ? item : { ...item, isRead: true, readAt },
        ),
      );
      setFeedback({
        tone: "success",
        text: "Toutes les notifications ont été marquées comme lues.",
      });
    } else {
      setFeedback({ tone: "error", text: result.error.message });
    }

    mutationInProgress.current = false;
    setIsMarkingAll(false);
  }

  if (notifications.length === 0) {
    return (
      <EmptyState
        description="Les changements de statut et nouveaux messages apparaîtront ici."
        title="Aucune notification"
      />
    );
  }

  return (
    <div className="notification-center">
      <div className="notification-toolbar">
        <p>
          <strong>{unreadCount}</strong>{" "}
          notification{unreadCount > 1 ? "s" : ""} non lue
          {unreadCount > 1 ? "s" : ""}
        </p>
        <SubmitButton
          disabled={unreadCount === 0}
          idleLabel="Tout marquer comme lu"
          isSubmitting={isMarkingAll}
          onClick={markAllAsRead}
          submittingLabel="Mise à jour…"
          type="button"
        />
      </div>

      {feedback ? (
        <FormMessage
          title={feedback.tone === "success" ? "Mise à jour effectuée" : "Échec"}
          tone={feedback.tone}
        >
          <p>{feedback.text}</p>
        </FormMessage>
      ) : null}

      <div className="notification-list" aria-live="polite">
        {notifications.map((notification) => {
          const safeLink = safeNotificationLink(notification.linkUrl);
          return (
            <article
              className={
                notification.isRead
                  ? "notification-card"
                  : "notification-card notification-card-unread"
              }
              key={notification.id}
            >
              <div className="notification-card-main">
                <div className="notification-card-heading">
                  <strong>{notification.title}</strong>
                  <span className="notification-state">
                    {notification.isRead ? "Lue" : "Nouvelle"}
                  </span>
                </div>
                <p>{notification.message}</p>
                <time dateTime={notification.createdAt}>
                  {formatDateTime(notification.createdAt)}
                </time>
              </div>
              <div className="notification-actions">
                {safeLink ? (
                  <Link className="button button-secondary" href={safeLink}>
                    Voir la demande
                  </Link>
                ) : null}
                {!notification.isRead ? (
                  <button
                    className="button button-ghost"
                    disabled={pendingId === notification.id}
                    onClick={() => markAsRead(notification.id)}
                    type="button"
                  >
                    {pendingId === notification.id
                      ? "Mise à jour…"
                      : "Marquer comme lue"}
                  </button>
                ) : null}
              </div>
            </article>
          );
        })}
      </div>
    </div>
  );
}

function safeNotificationLink(value: string | null) {
  if (
    value?.startsWith("/support/")
    || value?.startsWith("/request-service/")
  ) {
    return value;
  }

  return null;
}
