import type {
  RequestType,
  ServiceRequestStatus,
  SupportRequestStatus,
} from "@kermaria/shared";

import {
  serviceRequestStatus,
  supportStatus,
} from "@/lib/formatters";

import { StatusBadge } from "./StatusBadge";

type RequestStatusBadgeProps = {
  requestType: RequestType;
  status: SupportRequestStatus | ServiceRequestStatus;
};

export function RequestStatusBadge({
  requestType,
  status,
}: RequestStatusBadgeProps) {
  const definition = requestType === "support"
    ? supportStatus[status as SupportRequestStatus]
    : serviceRequestStatus[status as ServiceRequestStatus];

  return <StatusBadge label={definition.label} tone={definition.tone} />;
}
