---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-01
---

# Task Group: kermaria-client-platform / PayPal/Stripe live configuration and SRV-12/SRV-13 deployment

scope: Live payment-mode configuration and controlled deployment across the dedicated Kermaria edge, Linux webportal, and Windows API hosts; use for configuration/readiness work, not as proof of real payment or webhook processing.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse the topology, config-generation, backup, and readiness procedure for this dedicated SRV-11/12/13 environment; re-check the active host, provider resources, and remote configuration before a later live change.

## Task 1: Switch local PayPal and Stripe runtime configuration to live, success

### rollout_summary_files

- rollout_summaries/2026-07-31T15-48-20-VES0-kermaria_live_deployment_srv12_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T17-48-20-019fb8dc-b61e-78a0-bd51-4163a584629c.jsonl, updated_at=2026-08-01T09:31:45+00:00, thread_id=019fb8dc-b61e-78a0-bd51-4163a584629c, local live-mode configuration verified before remote deployment)

### keywords

- PAYPAL_MODE, PAYPAL_WEBHOOK_VERIFY, STRIPE_MODE, STRIPE_WEBHOOK_SECRET, PUBLIC_PORTAL_URL, WEBPORTAL_BASE_URL, dashboard.zacharyhounsa.ovh, kermaria-client-platform.local.env.ps1, build-webportal-config.ps1, build-api-config.ps1

## Task 2: Deploy live configuration to SRV-12 webportal and SRV-13 API, success

### rollout_summary_files

- rollout_summaries/2026-07-31T15-48-20-VES0-kermaria_live_deployment_srv12_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T17-48-20-019fb8dc-b61e-78a0-bd51-4163a584629c.jsonl, updated_at=2026-08-01T09:31:45+00:00, thread_id=019fb8dc-b61e-78a0-bd51-4163a584629c, backup, restart, and readiness validation on both hosts)

### keywords

- SRV-11, SRV-12, SRV-13, 192.168.100.212, 192.168.100.213, /etc/kermaria/webportal.env, C:\ProgramData\Kermaria\api-internal.config.json, kermaria-webportal, KermariaApiInternal, Plink, Kerberos, KERMARIA-SRV-13.home.bzh, /api/health/ready, /health/ready

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## Task 3: Verify the one-euro live payment path, uncertain

### rollout_summary_files

- rollout_summaries/2026-07-31T15-48-20-VES0-kermaria_live_deployment_srv12_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T17-48-20-019fb8dc-b61e-78a0-bd51-4163a584629c.jsonl, updated_at=2026-08-01T09:31:45+00:00, thread_id=019fb8dc-b61e-78a0-bd51-4163a584629c, no real payment/webhook exercised)

### keywords

- amountCents, 100, createStripeOneShotCheckoutSession, createPayPalOrder, STRIPE_WEBHOOK_SECRET, invoice, webhook

## Task 4: Audit and update live hCaptcha, SMTP, and email allowlist configuration, success

### rollout_summary_files

- rollout_summaries/2026-08-01T09-37-00-bHcq-kermaria_server_config_hcaptcha_smtp_allowlist.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T11-37-01-019fbcaf-1e2d-7eb0-a3b9-96ffd38b1856.jsonl, updated_at=2026-08-01T10:25:21+00:00, thread_id=019fbcaf-1e2d-7eb0-a3b9-96ffd38b1856, live configuration and actual SMTP delivery verified)

### keywords

- hCaptcha, SMTP_TEST_OK, ssl0.ovh.net, STARTTLS, EMAIL_LIVE_ALLOWLIST_ONLY=false, EMAIL_LIVE_ALLOWLIST=*, SERVICE_AUTH_TOKEN, INTERNAL_API_URL, AD_ALLOWED_UPN_DOMAINS, 192.168.100.212:3000

## User preferences

- when the user asked “Mets les à jour stp.” then “Et maintenant, le déploiement sur les serveurs.” -> once the files and targets are identified, favor direct, targeted operational execution over another theoretical explanation [Task 1][Task 2]

## Reusable knowledge

- The ignored parent file `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform.local.env.ps1` is auto-detected by `scripts/build-webportal-config.ps1` and `scripts/build-api-config.ps1`; set `PAYPAL_MODE=live`, `PAYPAL_WEBHOOK_VERIFY=true`, and `STRIPE_MODE=live` there for this environment. [Task 1]
- The active canonical values for `PUBLIC_PORTAL_URL` and `WEBPORTAL_BASE_URL` were `https://dashboard.zacharyhounsa.ovh`; inspect active server configuration before aligning URLs rather than assuming `portail.zacharyhounsa.ovh`. [Task 1]
- `apps/webportal/lib/paypal.ts` chooses live through `PAYPAL_MODE`; `apps/webportal/lib/stripe.ts` uses `STRIPE_MODE`; `apps/webportal/lib/stripe-webhook.ts` reads one `STRIPE_WEBHOOK_SECRET`, so two Stripe destinations with distinct secrets require one active destination or a code change. [Task 1]
- Dedicated topology: SRV-11 is edge/TLS, SRV-12 (`192.168.100.212`) is Ubuntu/Next webportal on private port 3000 with `kermaria-webportal` and `/etc/kermaria/webportal.env`, and SRV-13 (`192.168.100.213`) is the Windows/.NET API with `KermariaApiInternal` and `C:\ProgramData\Kermaria\api-internal.config.json`. Back up config files before modification, restart the corresponding service, then check both private readiness endpoints and `https://dashboard.zacharyhounsa.ovh/api/health/ready`. [Task 2]
- SSH via Plink needs an explicitly verified host fingerprint; WinRM worked with Kerberos using `KERMARIA-SRV-13.home.bzh`, while IP/Negotiate with TrustedHosts did not. [Task 2]
- Amounts are expressed in cents: a one-euro test is `100`; use a one-shot checkout first, since subscriptions also require live Stripe/PayPal offer resources. [Task 3]
- `SERVICE_AUTH_TOKEN` must match between webportal and API but must never be recorded in clear text; `INTERNAL_API_URL` is webportal server-only. SRV-12 was bound to `192.168.100.212:3000`, so localhost checks were invalid; use `curl -fsS http://192.168.100.212:3000/api/health/ready`. [Task 4]
- SMTP direct test on SRV-13 returned `SMTP_TEST_OK` and the user confirmed delivery. `EMAIL_LIVE_ALLOWLIST_ONLY=false` disables recipient filtering; `EMAIL_LIVE_ALLOWLIST=*` is decorative while that guardrail is off. Mail configuration is API/SRV-13 responsibility. [Task 4]

## Failures and how to do differently

- symptom: a presumed portal URL disagrees with deployed behavior -> cause: local assumptions drifted from the active SRV-12 configuration -> fix: inspect the active configuration and retain the verified canonical host before changing both URL variables. [Task 1]
- symptom: a live Stripe setup has multiple webhook destinations/secrets -> cause: the code accepts only one `STRIPE_WEBHOOK_SECRET` -> fix: keep one matching destination active or extend the webhook verification design before enabling both. [Task 1]
- symptom: SSH/WinRM access fails during the deploy -> cause: an unknown SSH host key or Windows authentication by IP -> fix: supply a verified Plink fingerprint; use the SRV-13 FQDN with Kerberos rather than IP/Negotiate. [Task 2]
- symptom: services/readiness are healthy but the payment rollout is reported as fully proven -> cause: no real PayPal/Stripe transaction or webhook was exercised -> fix: report readiness/config as validated but schedule a controlled functional payment and webhook test. Never retain or re-display conversational secrets; treat exposed values as compromised and rotate them. [Task 1][Task 2][Task 3]
- symptom: SRV-12 change/read command fails through SSH -> cause: OpenSSH did not inherit the user's PuTTY context and complex `sudo`/heredoc quoting is fragile -> fix: use Plink with verified host key and a simple known interactive command/session; back up, restart, then verify port and readiness. The systemd warning for `AD_ALLOWED_UPN_DOMAINS=clients.home.bzh` remains a separate cleanup item. [Task 4]

