import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";

/**
 * Contrat de cycle de vie Billing V2 : retour fournisseur et resiliation.
 *
 * Remplace `verify-subscription-return-idempotency.mjs`, qui protegeait
 * `lib/subscription-return.ts` — un helper du parcours legacy supprime avec la
 * migration 071. Ce script protege le parcours reellement vivant.
 *
 * Ce qui est verifie ici est STRUCTUREL : ou vit l'autorite, quelles sources
 * sont lues, dans quel ordre les gardes se posent, et ce que le portail n'a pas
 * le droit de conclure seul. Le COMPORTEMENT de la resiliation (fin de terme,
 * immediat, ancre manquante, conflit, environnement, echec fournisseur,
 * idempotence) est verifie par la suite C# `--billing-v2-cancellation`, qui
 * l'exerce reellement.
 */

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

function exists(path) {
  return existsSync(new URL(`../${path}`, import.meta.url));
}

const failures = [];
function check(label, run) {
  try {
    run();
  } catch (error) {
    failures.push([label, error.message]);
  }
}

/** Corps d'une methode C#, du nom donne jusqu'a la fin du fichier. */
function from(source, marker) {
  const index = source.indexOf(marker);
  assert.notEqual(index, -1, `Ancre introuvable dans la source : ${marker}`);
  return source.slice(index);
}

/** Corps borne : de `marker` jusqu'a `end` exclu. */
function between(source, marker, end) {
  const start = source.indexOf(marker);
  assert.notEqual(start, -1, `Ancre introuvable : ${marker}`);
  const stop = source.indexOf(end, start);
  assert.notEqual(stop, -1, `Borne introuvable apres ${marker} : ${end}`);
  return source.slice(start, stop);
}

const returnRoute = await read("app/api/subscriptions/billing-v2/return/route.ts");
const clientCancelRoute = await read("app/api/subscriptions/[id]/cancel/route.ts");
const adminCancelRoute = await read(
  "app/api/admin/subscriptions/[id]/cancel/route.ts",
);
const anchorResolverCs = await read(
  "../../apps/api-internal/Services/BillingV2ProviderAnchorResolver.cs",
);
const cancellationPolicyCs = await read(
  "../../apps/api-internal/Services/BillingV2SubscriptionCancellation.cs",
);
const cancellationServiceCs = await read(
  "../../apps/api-internal/Services/BillingV2SubscriptionCancellationService.cs",
);
const cancellationDispatcherCs = await read(
  "../../apps/api-internal/Services/BillingV2CancellationOutboxDispatcher.cs",
);
const cancellationExecutorCs = await read(
  "../../apps/api-internal/Services/BillingV2ProviderCancellationExecutor.cs",
);
const inboundEventCs = await read(
  "../../apps/api-internal/Services/BillingV2ProviderInboundEventService.cs",
);
const renewalCs = await read(
  "../../apps/api-internal/Services/BillingV2RenewalService.cs",
);
const recurringMutationCs = await read(
  "../../apps/api-internal/Services/BillingV2StripeRecurringMutationDispatcher.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");

// --- 1. Un seul parcours de retour, et il ne fait pas autorite. ------------

check("le retour fournisseur est revalide par l API interne", () => {
  assert.match(returnRoute, /import "server-only"/);
  assert.match(
    returnRoute,
    /internal\/portal\/billing-v2\/provider-return/,
    "Le retour doit etre confirme par API-INTERNAL.",
  );
  // Un retour navigateur est declaratif : il dit « je reviens de Stripe »,
  // pas « j'ai paye ». Le seul fait que la redirection ait eu lieu ne prouve
  // rien ; c'est le refetch fournisseur cote API qui conclut.
  assert.doesNotMatch(
    returnRoute,
    /amountCents|priceAmountCents|totalCents/,
    "Aucun montant ne doit transiter par l URL de retour.",
  );
  assert.match(
    returnRoute,
    /BILLING_V2_PROVIDER_EVENT_ALREADY_PROCESSED/,
    "Un retour rejoue doit rester un succes : le parcours est idempotent.",
  );
});

check("les parcours de retour heritees ont disparu", () => {
  for (const dead of [
    "app/api/subscriptions/return/route.ts",
    "app/api/subscriptions/stripe/return/route.ts",
    "lib/subscription-return.ts",
    "scripts/verify-subscription-return-idempotency.mjs",
  ]) {
    assert.equal(
      exists(dead),
      false,
      `${dead} appelait un endpoint interne supprime : il ne doit pas revenir.`,
    );
  }
});

// --- 2. Le BFF de resiliation n est pas une autorite fournisseur. ----------

check("les BFF de resiliation sont minces", () => {
  for (const [label, source] of [
    ["client", clientCancelRoute],
    ["admin", adminCancelRoute],
  ]) {
    assert.doesNotMatch(
      source,
      /api\.stripe\.com|api-m\.paypal\.com|api-m\.sandbox\.paypal\.com/,
      `La route ${label} ne doit contacter aucun fournisseur.`,
    );
    assert.doesNotMatch(
      source,
      /cancelStripeSubscription|cancelPayPalSubscription|scheduleStripeSubscriptionCancellationAtPeriodEnd/,
      `La route ${label} ne doit plus porter la logique fournisseur.`,
    );
    assert.doesNotMatch(
      source,
      /BILLING_V2_CANCELLATION_NOT_AVAILABLE/,
      `La route ${label} ne doit plus refuser un abonnement Billing V2 :`
        + " c est le seul systeme d abonnement restant.",
    );
    assert.match(
      source,
      /internal\/(portal|admin)\/subscriptions\//,
      `La route ${label} doit deleguer a API-INTERNAL.`,
    );
  }
});

// --- 3. L ancre fournisseur est resolue une seule fois, sur trois sources. -

check("les trois sources autoritaires de l ancre sont lues", () => {
  const reader = from(anchorResolverCs, "ReadCandidatesAsync");
  for (const table of [
    "billing_v2_payment_agreements",
    "billing_v2_provider_checkout_sessions",
    "billing_v2_payment_attempts",
  ]) {
    assert.ok(
      reader.includes(table),
      `${table} porte un provider_subscription_id dans des scenarios que les`
        + " autres tables ne couvrent pas : l omettre declarerait « sans"
        + " fournisseur » un abonnement qui preleve.",
    );
  }
  assert.match(
    reader,
    /attempt\.status = 'succeeded'/,
    "Seule une tentative REGLEE fait foi : une tentative echouee peut porter"
      + " un identifiant qui n a jamais rien encaisse.",
  );
});

check("aucun appelant ne reimplemente la resolution de l ancre", () => {
  for (const [label, source] of [
    ["le renouvellement", renewalCs],
    ["la mutation recurrente", recurringMutationCs],
    ["la resiliation", cancellationServiceCs],
  ]) {
    assert.ok(
      source.includes("BillingV2ProviderAnchorReader"),
      `${label} doit passer par le resolveur partage.`,
    );
    assert.doesNotMatch(
      source,
      /FROM billing_v2_provider_checkout_sessions|billing_v2_provider_checkout_sessions WHERE/,
      `${label} ne doit plus lire les sessions de checkout directement :`
        + " trois requetes divergentes finissent par ne plus repondre la meme"
        + " chose sur le meme contrat.",
    );
  }
});

check("un desaccord entre sources echoue en ferme", () => {
  const policy = between(
    anchorResolverCs,
    "public static BillingV2ProviderAnchorResolution Resolve(",
    "private static int PriorityOf",
  );
  assert.match(
    policy,
    /distinct\.Count > 1/,
    "La resolution doit comparer les candidats, pas en prendre un.",
  );
  assert.match(
    policy,
    /BillingV2ProviderAnchorOutcome\.Conflict/,
    "Un desaccord doit produire un resultat explicite.",
  );
  assert.doesNotMatch(
    policy,
    /OrderByDescending|\.Last\(\)/,
    "Choisir « la plus recente » reviendrait a agir sur un objet fournisseur"
      + " possiblement etranger au contrat.",
  );
});

// --- 3 bis. La cadence contractuelle vient de l autorite V2.1. -------------

const recurringLookup = from(
  anchorResolverCs,
  "public static async Task<bool> HasRecurringComponentAsync",
);

check("la cadence se lit sur la vue des composantes effectives", () => {
  assert.ok(
    recurringLookup.includes(
      "billing_v2_subscription_item_effective_price_components",
    ),
    "La migration designe cette vue comme unique point SQL de lecture du prix"
      + " contractuel : elle projette aussi les items legacy_single en"
      + " composante virtuelle, donc rien n est perdu a la lire.",
  );
});

check("la cadence ne revient jamais aux colonnes miroir de l item", () => {
  assert.doesNotMatch(
    recurringLookup,
    /item\.service_price_id/,
    "Sur un item componentized, item.service_price_id n est qu un miroir de"
      + " compatibilite : il porte au mieux une composante et ne decrit pas le"
      + " contrat. Un item mensuel dont le miroir est ponctuel serait declare"
      + " non-recurrent, et sa resiliation cloturerait localement un abonnement"
      + " que le fournisseur continue de prelever.",
  );
  assert.doesNotMatch(
    recurringLookup,
    /billing_v2_service_prices/,
    "Le catalogue de prix n est pas le contrat : la cadence contractuelle est"
      + " celle du snapshot de composantes, pas celle du tarif courant.",
  );
});

check("les deux fenetres effectives sont appliquees, pas une seule", () => {
  const policy = between(
    anchorResolverCs,
    "public static bool IsEffectiveRecurring(",
    "private static bool IsActive(",
  );
  for (const [fragment, why] of [
    ["row.ItemStatus", "le statut de l item"],
    ["row.ItemEffectiveFrom", "la fenetre de l item"],
    ["row.ComponentStatus", "le statut de la composante"],
    ["row.ComponentEffectiveFrom", "la fenetre de la composante"],
    ["BillingV2BillingCadences.Monthly", "la cadence mensuelle"],
  ]) {
    assert.ok(
      policy.includes(fragment),
      `${why} doit entrer dans la decision : sur un item componentized, la`
        + " composante mensuelle peut etre retiree sans que l item bouge.",
    );
  }
});

// --- 4. L etat local ne ment jamais sur ce que fait le fournisseur. --------

check("un statut cancelled n est pose que quand rien ne peut plus etre facture", () => {
  const resolveBody = from(
    cancellationPolicyCs,
    "public static BillingV2CancellationPlan Resolve(",
  );
  const cancelledOccurrences = resolveBody.match(/"cancelled"/g) ?? [];
  assert.equal(
    cancelledOccurrences.length,
    1,
    "La policy ne doit poser `cancelled` que dans le cas sans fournisseur.",
  );
  assert.match(
    resolveBody,
    /NoProviderReasonCode/,
    "Le cas sans abonnement fournisseur doit etre explicite.",
  );
  assert.match(
    resolveBody,
    /"pending_cancellation"/,
    "Toute resiliation impliquant un fournisseur passe par pending_cancellation.",
  );
});

check("une ancre absente ne vaut pas achat ponctuel", () => {
  const resolveBody = from(
    cancellationPolicyCs,
    "public static BillingV2CancellationPlan Resolve(",
  );
  // L'ordre compte : la composante recurrente doit etre testee AVANT de
  // conclure « rien a resilier ». Une ecriture manquee produit exactement la
  // meme absence d'ancre qu'un vrai one-shot.
  const recurringIndex = resolveBody.indexOf("context.HasRecurringComponent");
  const cancelledIndex = resolveBody.indexOf('"cancelled"');
  assert.ok(
    recurringIndex !== -1,
    "La decision doit lire le snapshot de composantes, pas seulement l ancre.",
  );
  assert.ok(
    recurringIndex < cancelledIndex,
    "La composante recurrente doit etre verifiee AVANT de poser `cancelled` :"
      + " sinon une ancre perdue cloture un abonnement encore preleve.",
  );
  assert.match(
    resolveBody,
    /AnchorMissingReasonCode/,
    "Une ancre manquante sur un contrat recurrent doit avoir son propre motif.",
  );
  assert.match(
    cancellationPolicyCs,
    /AnchorMissingReasonCode\s*=\s*\n?\s*BillingV2ProviderAnchorPolicy\.MissingReasonCode/,
    "Le motif doit etre celui du resolveur, pas une chaine recopiee.",
  );
  assert.match(
    anchorResolverCs,
    /MissingReasonCode\s*=\s*\n?\s*"BILLING_V2_CANCELLATION_PROVIDER_ANCHOR_MISSING"/,
    "Le code d erreur attendu par l exploitation doit rester stable.",
  );
});

check("le snapshot contractuel est lu, pas devine depuis le statut", () => {
  const body = from(
    cancellationServiceCs,
    "private static async Task<BillingV2CancellationContext?> ReadSnapshotAsync",
  );
  for (const column of ["current_period_ends_at", "started_at", "renews_at"]) {
    assert.ok(
      body.includes(column),
      `${column} doit etre lu : « suspended » ne dit pas si une periode payee`
        + " court encore.",
    );
  }
  assert.match(
    body,
    /SpecifyKind/,
    "MariaDB rend ces dates en Unspecified : sans SpecifyKind la comparaison"
      + " avec UtcNow derive de deux heures en ete.",
  );
  assert.match(
    from(cancellationPolicyCs, "public static bool HasRunningPaidPeriod"),
    /CurrentPeriodEndsAtUtc > nowUtc/,
    "La decision immediat / fin de terme doit se prendre sur les dates.",
  );
});

check("l ecriture locale et la demande fournisseur sont atomiques", () => {
  const body = from(
    cancellationServiceCs,
    "public async Task<BillingV2CancellationOutcome> RequestCancellationAsync",
  );
  const txIndex = body.indexOf("BeginTransactionAsync");
  const statusIndex = body.indexOf("ApplyLocalStatusAsync");
  const enqueueIndex = body.indexOf("EnqueueAsync");
  const commitIndex = body.indexOf("CommitAsync");
  assert.ok(txIndex !== -1, "La demande doit ouvrir une transaction.");
  assert.ok(
    txIndex < statusIndex && statusIndex < commitIndex,
    "Le statut local doit etre ecrit DANS la transaction.",
  );
  assert.ok(
    txIndex < enqueueIndex && enqueueIndex < commitIndex,
    "Les evenements d outbox doivent etre ecrits DANS la meme transaction :"
      + " sinon un abonnement peut afficher « en resiliation » sans qu aucun"
      + " appel fournisseur ne suive.",
  );
  assert.match(
    body,
    /foreach \(var action in plan\.ProviderActions\)/,
    "Une demande peut porter PLUSIEURS gestes : la fin de terme PayPal en"
      + " demande deux.",
  );
});

check("une revue manuelle n ecrit aucun statut", () => {
  const body = from(
    cancellationServiceCs,
    "public async Task<BillingV2CancellationOutcome> RequestCancellationAsync",
  );
  const reviewIndex = body.indexOf("plan.RequiresManualReview");
  const txIndex = body.indexOf("BeginTransactionAsync");
  assert.ok(
    reviewIndex !== -1 && reviewIndex < txIndex,
    "Le refus de conclure doit sortir AVANT toute ecriture de statut :"
      + " l abonnement doit rester exactement tel qu il etait.",
  );
});

// --- 5. La fin de terme PayPal est reellement executee au terme. -----------

check("la fin de terme PayPal planifie une vraie resiliation", () => {
  const actions = from(cancellationPolicyCs, "private static IReadOnlyList<BillingV2CancellationAction> TermEndActions");
  assert.match(
    actions,
    /SuspendPendingTermEnd/,
    "Suspendre immediatement empeche un renouvellement accidentel.",
  );
  assert.match(
    actions,
    /CancelAtTerm,\s*\n?\s*periodEndsAtUtc/,
    "La resiliation finale doit etre datee du terme contractuel, pas laissee"
      + " a un lifecycle implicite.",
  );
});

check("le geste differe est persiste, jamais minute en memoire", () => {
  const enqueue = from(cancellationServiceCs, "private static async Task<bool> EnqueueAsync");
  assert.match(
    enqueue,
    /COALESCE\(@available_at, UTC_TIMESTAMP\(6\)\)/,
    "L echeance doit etre ecrite dans la ligne d outbox.",
  );
  assert.doesNotMatch(
    cancellationPolicyCs + cancellationServiceCs + cancellationDispatcherCs,
    /Timer|Task\.Delay\([^)]*periodEnd|DelayUntil/,
    "Un minuteur en memoire serait perdu au redemarrage : le terme doit vivre"
      + " en base.",
  );
  assert.match(
    from(cancellationDispatcherCs, "ReadPendingEventsAsync"),
    /available_at <= UTC_TIMESTAMP\(6\)/,
    "C est ce filtre qui garde l evenement dormant jusqu au terme.",
  );
});

check("seuls les gestes terminaux clotent l abonnement", () => {
  assert.match(
    cancellationDispatcherCs,
    /if \(result\.Succeeded\s*\n?\s*&& BillingV2CancellationOperations\.ClosesLocalSubscription\(/,
    "Le passage a cancelled exige une acceptation fournisseur ET un geste qui"
      + " rend l abonnement definitivement non facturable.",
  );
  const closes = between(
    cancellationPolicyCs,
    "public static bool ClosesLocalSubscription",
    "public static bool IsKnown",
  );
  assert.match(
    closes,
    /CancelImmediate or CancelAtTerm/,
    "Une suspension se leve et une promesse de non-renouvellement laisse la"
      + " periode courir : ni l une ni l autre ne clot quoi que ce soit.",
  );
  const markCancelledCount =
    (cancellationDispatcherCs.match(/MarkCancelledAsync\(/g) ?? []).length;
  assert.equal(
    markCancelledCount,
    2,
    "MarkCancelledAsync ne doit avoir qu un seul appel, sous la garde de succes.",
  );
  assert.match(
    cancellationDispatcherCs,
    /Local status stays pending_cancellation/,
    "Un echec doit etre journalise comme tel, sans masquer le risque de"
      + " facturation.",
  );
});

check("notre propre suspension ne devient pas un impaye", () => {
  const kinds = from(inboundEventCs, "private static BillingV2ProviderEventKind? ResolveEventKind");
  const expectedIndex = kinds.indexOf('when IsExpectedSuspension(state)');
  const failedIndex = kinds.indexOf(
    '"BILLING_V2_PROVIDER_SUBSCRIPTION_PAYMENT_FAILED"',
  );
  assert.ok(
    expectedIndex !== -1,
    "La suspension attendue doit etre distinguee de l incident de paiement.",
  );
  assert.ok(
    expectedIndex < failedIndex,
    "La branche « attendue » doit precede la branche generique, sinon elle"
      + " n est jamais atteinte.",
  );
  assert.match(
    from(inboundEventCs, "private static bool IsExpectedSuspension"),
    /"pending_cancellation"/,
    "Le seul marqueur d intention fiable est le statut local, qu aucun webhook"
      + " ne pose.",
  );
  // Le garde-fou inverse : une suspension inattendue doit toujours pouvoir
  // remonter en past_due, sinon on cache un vrai incident de paiement.
  assert.match(
    kinds,
    /"billing_v2\.subscription_payment_failed"\s*or "billing\.subscription\.suspended" =>[\s\S]{0,400}?SubscriptionStatus: "past_due"/,
    "Une suspension sans intention de resiliation reste un incident visible.",
  );
});

// --- 6. Aucun appel ne part dans le mauvais environnement. -----------------

check("l environnement d execution est verifie avant tout appel HTTP", () => {
  for (const [label, marker, end] of [
    [
      "Stripe",
      "private async Task<BillingV2ProviderCancellationResult> CancelStripeAsync",
      "private async Task<BillingV2ProviderCancellationResult> CancelPayPalAsync",
    ],
    [
      "PayPal",
      "private async Task<BillingV2ProviderCancellationResult> CancelPayPalAsync",
      "private async Task<BillingV2ProviderCancellationResult> SendAsync",
    ],
  ]) {
    const body = between(cancellationExecutorCs, marker, end);
    const checkIndex = body.indexOf(
      "BillingV2ProviderRuntimeEnvironmentPolicy.Check",
    );
    const requestIndex = body.indexOf("new HttpRequestMessage");
    const tokenIndex = body.indexOf("CreatePayPalAccessTokenAsync");
    assert.ok(
      checkIndex !== -1,
      `${label} doit comparer l environnement persiste a celui du processus.`,
    );
    assert.ok(
      requestIndex === -1 || checkIndex < requestIndex,
      `${label} : le controle doit preceder toute construction de requete.`,
    );
    assert.ok(
      tokenIndex === -1 || checkIndex < tokenIndex,
      `${label} : le controle doit preceder meme l obtention du jeton.`,
    );
  }
  assert.match(
    from(cancellationPolicyCs, "class BillingV2ProviderRuntimeEnvironmentPolicy"),
    /MismatchCode\s*=\s*\n?\s*"BILLING_V2_PROVIDER_RUNTIME_ENVIRONMENT_MISMATCH"/,
    "Le code de refus doit rester stable pour l exploitation.",
  );
  assert.match(
    from(cancellationPolicyCs, "class BillingV2ProviderRuntimeEnvironmentPolicy"),
    /Retryable: false/,
    "La configuration du processus ne se repare pas par un retry.",
  );
});

check("un 404 ne conclut la convergence qu apres ce controle", () => {
  const send = from(
    cancellationExecutorCs,
    "private async Task<BillingV2ProviderCancellationResult> SendAsync",
  );
  assert.match(
    send,
    /HttpStatusCode\.NotFound/,
    "Le 404 doit rester traite explicitement.",
  );
  assert.match(
    send,
    /BillingV2ProviderRuntimeEnvironmentPolicy/,
    "La raison pour laquelle un 404 vaut convergence doit rester ecrite la ou"
      + " la conclusion est tiree.",
  );
});

check("un rejet ambigu n est pas requalifie en succes", () => {
  const send = from(
    cancellationExecutorCs,
    "private async Task<BillingV2ProviderCancellationResult> SendAsync",
  );
  assert.match(
    send,
    /HttpStatusCode\.UnprocessableEntity/,
    "Le 422 PayPal doit etre traite a part : il couvre aussi bien « deja"
      + " fait » que « geste impossible ».",
  );
  assert.doesNotMatch(
    send,
    /UnprocessableEntity[\s\S]{0,120}new BillingV2ProviderCancellationResult\(\s*\n?\s*true/,
    "Un 422 ne doit jamais devenir un succes sans relecture de l etat reel.",
  );
  const probe = from(
    cancellationExecutorCs,
    "ProbePayPalConvergenceAsync",
  );
  assert.match(
    probe,
    /"CANCELLED" or "EXPIRED"/,
    "Seul un etat fournisseur non facturable prouve la convergence.",
  );
  assert.match(
    probe,
    /SuspendPendingTermEnd/,
    "Une suspension deja en place ne satisfait QUE la suspension demandee :"
      + " elle ne prouve pas une resiliation.",
  );
});

// --- 7. Sans executeur configure, rien n est promis. -----------------------

check("l executeur desactive echoue, il ne reussit pas silencieusement", () => {
  const disabled = from(
    cancellationPolicyCs,
    "public sealed class DisabledBillingV2ProviderCancellationExecutor",
  );
  assert.match(
    disabled,
    /new BillingV2ProviderCancellationResult\(\s*false/,
    "L executeur inerte doit renvoyer un echec.",
  );
  assert.match(
    disabled,
    /Retryable: true/,
    "La resiliation reste due : elle doit pouvoir repartir.",
  );
});

check("la chaine de resiliation est reellement cablee", () => {
  for (const registration of [
    "IBillingV2ProviderCancellationExecutor",
    "IBillingV2SubscriptionCancellationService",
    "IBillingV2CancellationOutboxDispatcher",
    "BillingV2CancellationOutboxWorker",
  ]) {
    assert.ok(
      programCs.includes(registration),
      `${registration} doit etre enregistre : un service non cable ne resilie rien.`,
    );
  }
});

if (failures.length > 0) {
  for (const [label, message] of failures) {
    console.error(`  FAIL ${label}`);
    console.error(`       ${message}`);
  }
  console.error(
    `\n${failures.length} verification(s) de contrat cycle de vie en echec.`,
  );
  process.exit(1);
}

console.log("Contrat cycle de vie Billing V2 verifie.");
