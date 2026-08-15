# Billing V2 safety rules

This file is a TEMPLATE for repository instructions.
Merge it into the repository's existing AGENTS.md only after reviewing existing project instructions.

## Billing safety

- Never change production billing behavior without focused regression tests.
- Never delete or rewrite legacy rows in `commercial_offers` during the Billing V2 migration.
- Existing active subscriptions must remain billable.
- Existing historical invoices must never be recalculated.
- Existing Stripe Price IDs and PayPal Plan IDs are immutable during migration.
- Database migrations must remain backward compatible while legacy billing exists.
- Never run destructive database migrations automatically.
- Never use floating-point arithmetic for money.
- Store money as integer cents.
- Store percentage discounts as basis points.
- Never infer a VPN tier for a legacy subscription when the old data does not prove the tier.
- Do not construct migrated subscription entitlements from the current V2 preset.
- Reconstruct migrated entitlements from legacy technical service references.
- A V2 preset is a commercial starting point, not a contract.
- Provisioning must depend on effective services and tiers, not pack names.
- Provider webhooks and provisioning operations must be idempotent.
- Every billing mutation requires an audit trail.
- Every external side effect must be retry-safe.
- Billing changes require a documented rollback path.

## First Billing V2 task

The first task is audit-only.
Do not modify functional code until the legacy dependency map has been reviewed.
