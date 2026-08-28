import type { ConfigurationStatusSnapshot } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) { return handleAdminGet<ConfigurationStatusSnapshot>(request, "/internal/admin/settings/status"); }
