import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (relative) => readFileSync(resolve(root, relative), "utf8");

const review = read("../api-internal/Services/Provisioning/BillingV2ProvisioningReadinessReview.cs");
const provisioning = read("../api-internal/Services/Provisioning/BillingV2ProvisioningService.cs");
const program = read("../api-internal/Program.cs");
const tests = read("../../tests/api-internal/BillingV2ProvisioningReadinessReviewTests.cs");

const evaluatorStart = review.indexOf("EvaluateClientReadinessAsync(");
const reviewStart = review.indexOf("ReviewClientReadinessAsync(", evaluatorStart + 1);
const evaluator = review.slice(evaluatorStart, reviewStart);
const reviewMethod = review.slice(reviewStart, review.indexOf("LoadReadinessReviewSubjectAsync", reviewStart));
const persistenceStart = review.indexOf("private async Task PersistReadinessReviewAsync(");
const persistence = review.slice(persistenceStart, review.indexOf("BillingV2ProvisioningReadinessReviewSubject", persistenceStart));
const previewRouteStart = program.indexOf('"/internal/admin/billing-v2/provisioning-readiness/{customerId}/preview"');
const previewRoute = program.slice(previewRouteStart, program.indexOf('app.MapPost(', previewRouteStart));

assert(provisioning.includes("EvaluateClientReadinessAsync"),
  "Le contrat de provisioning doit exposer la primitive de preview non persistante.");
assert(evaluator.includes("BillingV2ProvisioningReadinessReviewPolicy.Evaluate"),
  "La preview doit reutiliser la policy de review existante.");
assert(!evaluator.includes("PersistReadinessReviewAsync")
  && !evaluator.includes("ApplyAsync(")
  && !evaluator.includes("ExecutePerUserAsync")
  && !evaluator.includes("INSERT INTO")
  && !evaluator.includes("UPDATE billing_v2")
  && !evaluator.includes("DELETE FROM"),
  "La preview ne doit ni persister la readiness ni lancer provisioning, KoXo ou AD.");
assert(reviewMethod.includes("EvaluateClientReadinessAsync")
  && reviewMethod.includes("PersistReadinessReviewAsync"),
  "La review persistante doit seulement persister le verdict de la preview commune.");
assert(persistence.includes("INSERT INTO billing_v2_provisioning_client_readiness")
  && persistence.includes("ON DUPLICATE KEY UPDATE")
  && persistence.includes("evaluation.Ready")
  && persistence.includes("evaluation.ReviewStatus")
  && persistence.includes("evaluation.UnresolvedMismatchCount"),
  "La review persistante doit continuer a materialiser success ou failed depuis le verdict commun.");
assert(previewRouteStart > 0
  && previewRoute.includes("ResolvePortalSessionAsync")
  && previewRoute.includes("PortalRoles.InternalAdmin")
  && previewRoute.includes("EvaluateClientReadinessAsync"),
  "La preview doit rester dans l'API admin interne et exiger un administrateur.");
assert(!previewRoute.includes("IAuditService")
  && !previewRoute.includes("RecordAsync")
  && !previewRoute.includes("ReviewClientReadinessAsync"),
  "La route de preview ne doit pas creer d'audit ni persister une review.");
assert(tests.includes("PreviewSuccessAndPersistedReviewShareDecision")
  && tests.includes("PreviewFailureAndPersistedReviewShareReasons")
  && tests.includes("PersistenceUnavailableRemainsFailClosed"),
  "Les contrats purs doivent couvrir les verdicts success, failed et SQL indisponible.");

console.log("Contrat preview/review de readiness Billing V2 vérifié.");
