---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-19
---

# Task Group: kermaria-client-platform / signup identity, AD alignment, and KoXo V0.38

scope: Code-backed signup/password identity work, Active Directory alignment, and the V0.38 documentation/implementation path from portal-only behavior to linked AD synchronization.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria signup/AD/KoXo design, docs, migrations, or password recovery in this checkout; distinguish pre-V0.38 current-state evidence from the V0.38 implementation and verify actual AD infrastructure separately.

## Task 1: Verify current signup approval and set-password do not create AD accounts, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, current-state verification before future design)

### keywords

- signup, SetPasswordAsync, MariaDbSignupRepository, LdapActiveDirectoryService, Active Directory, customer_ad_links, portal_user, approval flow, no AD creation

## Task 2: Evaluate KoXo Administrator as post-creation AD administration, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, PDF-backed KoXo integration assessment)

### keywords

- KoXo Administrator, koxoadm_fr.pdf, CSV import, XML response, /Synchro=fichier.xml, /ChangePassword, scheduled task, PowerShell, sAMAccountName, clients.home.bzh

## Task 3: Create the future-facing `docs/v0.38/` pack and README routing, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, multi-file V0.38 dossier for later implementation)

### keywords

- docs/v0.38, README.md, V0.38_KOXO_SIGNUP_INTEGRATION.md, V0.38_KOXO_DATA_CONTRACTS.md, V0.38_KOXO_AUTOMATION_RUNBOOK.md, V0.38_R740XD_CUTOVER_CHECKLIST.md, R740xd, future feature

## Task 4: Capture the chosen V0.38 architecture defaults, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, fixed future architecture choices preserved in docs)

### keywords

- set-password trigger, Kermaria source of truth, KoXo async, CSV/XML + tache planifiee, employeeNumber, sAMAccountName, unique email, multi-user signup, clients.home.bzh, OU web clients

## Task 5: Align site/AD documentation for `clients.home.bzh`, partial

### rollout_summary_files

- rollout_summaries/2026-07-18T10-22-38-J5Qp-kermaria_clients_home_bzh_docs_alignment.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\18\rollout-2026-07-18T12-22-43-019f74bf-dab8-7863-a272-ad9c089135ed.jsonl, updated_at=2026-07-18T10:34:30+00:00, thread_id=019f74bf-dab8-7863-a272-ad9c089135ed, documentation-only current-vs-target alignment)

### keywords

- clients.home.bzh, OU=10_Customers, AD_DOMAIN, AD_CLIENTS_OU_DN, AD_REQUIRED_OU_ROOT, AD_ALLOWED_ROOTS, ActiveDirectoryPathScope, customer_ad_links, V0.38_SITE_AD_ALIGNMENT.md

## Task 6: Implement admin password recovery and portal-to-AD identity alignment; publish `v0.38`, partial

### rollout_summary_files

- rollout_summaries/2026-07-18T08-59-39-9eee-v038_signup_password_admin_ad_sync_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\18\rollout-2026-07-18T10-59-44-019f7473-e2c8-7551-9f70-3f4495db4b02.jsonl, updated_at=2026-07-19T13:33:40+00:00, thread_id=019f7473-e2c8-7551-9f70-3f4495db4b02, focused V0.38 release; API suite remains unresolved outside the lot)

### keywords

- initialize-password, resend-password-email, PortalPasswordService, /internal/admin/signups/{id}, migration-034, 034_v038_identity_alignment.sql, clients.home.bzh, cd8a6e7, v0.38, test:signup, VerifyDownloadsAsync

## User preferences

- when the user asks whether signup already creates AD automatically and gives a concrete mapping rule, verify the actual code path against that rule instead of inferring intended behavior from surrounding docs [Task 1]
- when shaping identity/signup work, the user kept refining structured fields, unique email per user, and pro/association multi-user cases -> default to explicit implementation-oriented schemas rather than loose contact blobs [Task 1][Task 4]
- when the user says `La première option me plait bien. On peut en faire un .md pour l'intégrer quand le R740xd va arriver. Pose-moi un maximum de questions.` -> switch into clarification-first architecture capture, then produce a durable doc pack rather than jumping to premature code [Task 2][Task 3]
- when the user later says `PLEASE IMPLEMENT THIS PLAN` and asks for `Plusieurs notes Markdown`, execute the agreed dossier as a multi-file pack under `docs/v0.38/`, not a single summary note [Task 3]
- when discussing KoXo, the user's repeated choices make the default clear: Kermaria remains source of truth, KoXo is post-creation/daily administration only, and the automation should be asynchronous `CSV/XML + tache planifiee` [Task 2][Task 4]
- when the user asked for `Initialisation par mes soins` and `Renvoie de mail de réinitialisation de mot de passe` -> expose both recovery modes within the approved-signup detail, where `portal_user` and password state are visible, rather than creating unrelated flows [Task 6]

## Reusable knowledge

- Pre-V0.38 repo truth: signup approval created `customer` + `portal_user` in MariaDB, and `SetPasswordAsync` stored only the portal password; AD creation was a separate admin capability. Keep that chronology when interpreting earlier docs. [Task 1]
- When the question is "does signup already create the AD user?", inspect `SignupService`, `MariaDbSignupRepository`, and only then trace the AD service; that is the fastest route to a code-backed answer [Task 1]
- KoXo manual evidence supports CSV import, user-attribute import, XML response files, command-line options such as `/Synchro=fichier.xml` and `/ChangePassword`, plus PowerShell/VBS/BAT automation; no clear REST/webhook interface was found [Task 2]
- The realistic integration shape supported by the evidence is transactional Kermaria-first AD creation followed by asynchronous KoXo replay/administration via exported files and scheduled jobs [Task 2]
- The V0.38 doc pack already exists at `docs/v0.38/` with a README plus four companion docs, and the repo `README.md` links to that folder as the future handoff entrypoint [Task 3]
- The fixed future defaults captured in the V0.38 pack are: AD creation at `set-password`, continuous portal -> AD password sync, `sAMAccountName` based on initial + 6 surname chars with numeric suffixing on collision, `employeeNumber` = Kermaria customer reference, unique email per portal user, multi-user signup for pro/association, and KoXo automation through `CSV/XML` plus a scheduled task [Task 4]
- `ActiveDirectoryPathScope` expects `OU=<customerReference>,OU=10_Customers,<clientsOuDn>` with `Users`, `Groups`, and `Disabled`; the documentation alignment corrected the old `OU=Customers` wording. The exact production DN, service account, ACLs, and cutover remain infrastructure work, not validated application behavior. [Task 5]
- V0.38 adds `POST /internal/admin/signups/{id}/initialize-password` and `/resend-password-email` with matching protected BFF routes. Reuse the one-shot set-password mechanism by refreshing only the hashed token; never log/persist plaintext passwords or tokens, and retain the 12-character minimum. [Task 6]
- Migration `034_v038_identity_alignment.sql` adds structured signup/customer/portal-user and AD synchronization state. `npm run test:signup` passed with 36 checks and `npm run typecheck:webportal` passed; commit `cd8a6e7` (`feat: align signup password flow with AD identity sync`) and annotated `v0.38` were pushed while unrelated dirty changes stayed out. [Task 6]

## Failures and how to do differently

- symptom: AD-behavior answers drift into assumption-heavy architecture talk -> cause: the current signup flow was not checked first -> fix: verify `SignupService` and `MariaDbSignupRepository` before proposing future changes [Task 1]
- symptom: the first search for the AD implementation misses the real file -> cause: the service lives under `Services/ActiveDirectory/`, not a flatter path guess -> fix: search for `LdapActiveDirectoryService` and confirm the actual file path before citing it [Task 1]
- symptom: PDF analysis stalls in the default Python environment -> cause: `pypdf` or the initial tool path is unavailable there -> fix: use the bundled Codex runtime Python and extract/search text from the PDF [Task 2]
- symptom: future docs accidentally read like implemented behavior -> cause: speculative architecture was not clearly separated from current code truth -> fix: label the pack as future/spec/runbook material and keep current-vs-future distinctions explicit [Task 3][Task 4]
- symptom: the local admin signup page/API fails with `Unknown column 'signup_pending.customer_type'` -> cause: MariaDB lacks migration 034 -> fix: apply `034_v038_identity_alignment.sql` before diagnosing the BFF/UI. [Task 6]
- symptom: `npm run test:api` is described as green for V0.38 -> cause: it built but failed at `tests/api-internal/Program.cs:2786` / `VerifyDownloadsAsync` (`Le portail doit filtrer les téléchargements selon les droits actifs`) -> fix: report this as an unresolved downloads validation outside the signup/password lot; do not claim the full API suite passes. [Task 6]

