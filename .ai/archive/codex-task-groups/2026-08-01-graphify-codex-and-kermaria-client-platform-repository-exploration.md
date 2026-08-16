---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-01
---

# Task Group: Graphify/Codex and kermaria-client-platform repository exploration

scope: Installing and using Graphify from Codex/Windows to map the Kermaria repository, troubleshoot graph persistence, and trace the webportal BFF signup path into API-INTERNAL.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Graphify-based Kermaria code exploration on this Windows/Codex setup; recheck installed version, PATH, and graph artifact existence before treating prior output as current.

## Task 1: Install Graphify CLI and Codex integration, success

### rollout_summary_files

- rollout_summaries/2026-07-30T05-38-16-tRYg-graphify_codex_kermaria_code_only_workflow.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-30\heyy-tu-peux-installer-graphify-sur, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T07-38-16-019fb187-d4cc-7a20-af82-8695c4d43674.jsonl, updated_at=2026-07-30T06:13:12+00:00, thread_id=019fb187-d4cc-7a20-af82-8695c4d43674, Windows/Codex installation)

### keywords

- graphifyy, graphify 0.9.30, graphify install --platform codex, Python314\\Scripts, PATH, multi_agent, C:\\Users\\zhounsah\\.codex\\skills\\graphify\\SKILL.md

## Task 2: Build and query a Kermaria repository graph, partial then success

### rollout_summary_files

- rollout_summaries/2026-07-30T06-13-58-fEYE-graphify_kermaria_signup_flow.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T08-13-58-019fb1a8-835f-73f1-95a0-1fd8611a8ab8.jsonl, updated_at=2026-07-30T06:40:19+00:00, thread_id=019fb1a8-835f-73f1-95a0-1fd8611a8ab8, full-repository graph and reliable alternate output)
- rollout_summaries/2026-07-30T05-38-16-tRYg-graphify_codex_kermaria_code_only_workflow.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-30\heyy-tu-peux-installer-graphify-sur, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T07-38-16-019fb187-d4cc-7a20-af82-8695c4d43674.jsonl, updated_at=2026-07-30T06:13:12+00:00, thread_id=019fb187-d4cc-7a20-af82-8695c4d43674, code-only workflow and graph-loss symptom)

### keywords

- graphify extract . --code-only, graphify cluster-only . --no-label, graphify-out\\graph.json, .codex-tmp\\gfout, tree-sitter-sql, BrokenProcessPool, parallel=False, --budget 5000, graph file not found

## Task 3: Trace webportal signup through API-INTERNAL SignupService, success

### rollout_summary_files

- rollout_summaries/2026-07-30T06-13-58-fEYE-graphify_kermaria_signup_flow.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T08-13-58-019fb1a8-835f-73f1-95a0-1fd8611a8ab8.jsonl, updated_at=2026-07-30T06:40:19+00:00, thread_id=019fb1a8-835f-73f1-95a0-1fd8611a8ab8, code-backed BFF/API path)
- rollout_summaries/2026-07-30T05-38-16-tRYg-graphify_codex_kermaria_code_only_workflow.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-30\heyy-tu-peux-installer-graphify-sur, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T07-38-16-019fb187-d4cc-7a20-af82-8695c4d43674.jsonl, updated_at=2026-07-30T06:13:12+00:00, thread_id=019fb187-d4cc-7a20-af82-8695c4d43674, corroborating route/helper trace)

### keywords

- signup/route.ts, callInternalSignup(), INTERNAL_API_URL, getInternalServiceHeaders(), /internal/signup, ISignupService, SignupService.SubmitAsync, ProvisionActiveDirectoryAsync, WEBPORTAL/BFF, MariaDB

## Task 4: Configure Graphify fast-path and the `éco token` trigger, success

### rollout_summary_files

- rollout_summaries/2026-08-01T09-07-11-ZSJV-graphify_token_saving_trigger_kermaria.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T11-07-11-019fbc93-cea2-78f1-8cd8-690ca9fb170f.jsonl, updated_at=2026-08-01T09:16:06+00:00, thread_id=019fbc93-cea2-78f1-8cd8-690ca9fb170f, existing graph exposed through a junction)

### keywords

- éco token, eco token, AGENTS.md, graphify-out, gfout, junction, graph.json, 4617 nodes, 13738 edges, .git\\info\\exclude

## Task 5: Identify implemented Kermaria payment methods, uncertain

### rollout_summary_files

- rollout_summaries/2026-08-01T09-07-11-ZSJV-graphify_token_saving_trigger_kermaria.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T11-07-11-019fbc93-cea2-78f1-8cd8-690ca9fb170f.jsonl, updated_at=2026-08-01T09:16:06+00:00, thread_id=019fbc93-cea2-78f1-8cd8-690ca9fb170f, code-backed payment inventory)

### keywords

- createStripeOneShotCheckoutSession, createStripeSubscriptionCheckoutSession, createPayPalOrder, createPayPalSubscription, paymentMethod: "manual", mark-as-paid, "paypal" | "stripe" | "billing"

## User preferences

- when using Graphify, the user wants it “pour économiser des tokens” -> prefer local/AST `--code-only` extraction and avoid document LLM backends unless they are necessary [Task 1][Task 2]
- when a repository-size warning was raised, the user said “Ouais force sur C:\Users\zhounsah\Documents\Dev\kermaria-client-platform” -> once explicit authorization is given, proceed at that broader scope instead of repeatedly insisting on a narrower folder [Task 2]

## Reusable knowledge

- The July 30 Windows/Codex setup had `graphify 0.9.30`, `graphify install --platform codex`, Python 3.14.5 user scripts at `C:\Users\zhounsah\AppData\Roaming\Python\Python314\Scripts`, and `multi_agent = true` in `C:\Users\zhounsah\.codex\config.toml`; recheck rather than assuming this is permanent. [Task 1]
- The existing graph was exposed at the skill-expected `graphify-out\\graph.json` through a Windows junction to `.codex-tmp\\gfout`, with repository-local `.git\\info\\exclude` preventing graph artifacts from appearing in Git. Prefer the existing graph before rebuilding. `AGENTS.md` makes `éco token`/`eco token` a frugal-workflow trigger: reuse prior artifacts, limit reads/search scope, avoid long recaps, and retain necessary verification. [Task 4]
- Code inspection found Stripe card payments, PayPal, and bank transfer/manual payment. Stripe/PayPal support one-shot and subscription helpers; manual payment is `paymentMethod: "manual"` with admin `mark-as-paid`. [Task 5]
- For Kermaria, run the graph from `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform`, not the parent `Dev` directory. `graphify .` pulled in 129 documents needing an LLM; `graphify extract . --code-only` avoided that, and installing `graphifyy[sql]` added `tree-sitter-sql`. [Task 2]
- Before `query`, `explain`, or `path`, check `graphify-out\\graph.json`. If missing, recreate with `graphify extract . --code-only` and `graphify cluster-only . --no-label`; the successful alternate artifact location was `.codex-tmp\\gfout\\graph.json`. [Task 2]
- The observed signup path is public `POST()` in `apps/webportal/app/api/signup/route.ts` -> `callInternalSignup()` -> service-authenticated `INTERNAL_API_URL/internal/signup` -> `Program.cs` `ISignupService` -> `SignupService.SubmitAsync`. Lifecycle: `SubmitAsync` -> `VerifyEmailAsync` -> admin `ApproveAsync` -> `SetPasswordAsync`; AD provisioning happens at password setup via `ProvisionActiveDirectoryAsync`, not at submission. Keep the `AGENTS.md` boundary: browser -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB. [Task 3]

## Failures and how to do differently

- symptom: a new terminal cannot find Graphify after installation -> cause: it has not inherited the user PATH -> fix: open a new terminal/restart Codex or use the absolute `Python314\\Scripts\\graphify.exe` path. [Task 1]
- symptom: `error: graph file not found: ...\\graphify-out\\graph.json` -> cause: the graph artifact disappeared, not an invalid query -> fix: verify existence first, rebuild code-only, or use the known `.codex-tmp\\gfout` workaround. [Task 2]
- symptom: extraction invoked from Python stdin emits `BrokenProcessPool` or invalid `<stdin>` -> cause: Windows multiprocessing spawn -> fix: use `parallel=False` or execute a real Python script file. [Task 2]
- symptom: a broad graph query is truncated or `path "callInternalSignup()" "SignupService"` has no path -> cause: graph budgets and AST edges do not reliably cross the HTTP boundary -> fix: use targeted symbols plus `--budget 5000`, then `rg` `signup-server.ts`, `route.ts`, and `Program.cs` to bridge the call manually. [Task 2][Task 3]

