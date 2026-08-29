import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const adminBff = await read("lib/admin-bff.ts");
const adUsersRoute = await read("app/api/admin/ad/users/route.ts");
const adGroupsRoute = await read("app/api/admin/ad/groups/route.ts");
const adCreateUserRoute = await read(
  "app/api/admin/customers/[customerReference]/ad/users/route.ts",
);
const downloadsRoute = await read("app/api/admin/downloads/route.ts");
const downloadDetailRoute = await read("app/api/admin/downloads/[id]/route.ts");
const downloadFileRoute = await read("app/api/admin/downloads/[id]/file/route.ts");
const downloadCategoriesRoute = await read(
  "app/api/admin/download-categories/route.ts",
);
const internalApi = await read("lib/internal-api.ts");
const settingsAuditRoute = await read("app/api/admin/settings/audit/route.ts");
const settingsPermissionsRoute = await read(
  "app/api/admin/settings/permissions/route.ts",
);
const settingsAuditCenter = await read("components/AdminSettingsAuditCenter.tsx");
const settingsPermissionMigration = await read(
  "../api-internal/Migrations/MariaDb/079_configuration_permissions_fail_closed.sql",
);
const settingsFederation = await read("components/AdminSettingsFederation.tsx");
const billingFormules = await read("lib/billing-v2-formules.ts");

async function readRepo(path) {
  return readFile(new URL(`../../../${path}`, import.meta.url), "utf8");
}
const settingsDirectoryRoute = await read(
  "app/api/admin/settings/directory/route.ts",
);
const directoryCenter = await read("components/AdminDirectoryCenter.tsx");
const appShell = await read("components/AppShell.tsx");
const settingsPage = await read("app/admin/settings/page.tsx");
const settingsRoute = await read("app/api/admin/settings/route.ts");
const settingsMutationRoute = await read("app/api/admin/settings/[key]/route.ts");
const settingsStatusRoute = await read("app/api/admin/settings/status/route.ts");
const settingsCenter = await read("components/AdminSettingsCenter.tsx");
const communicationsPage = await read("app/admin/settings/messages/page.tsx");
const communicationsCenter = await read("components/AdminCommunicationsCenter.tsx");
const communicationsRoute = await read("app/api/admin/communications/route.ts");
const communicationsEmailRoute = await read(
  "app/api/admin/communications/email/[key]/route.ts",
);
const communicationsEmailTestRoute = await read(
  "app/api/admin/communications/email/[key]/test/route.ts",
);
const communicationsEmailRestoreRoute = await read(
  "app/api/admin/communications/email/[key]/restore-default/route.ts",
);
const communicationsNotificationRoute = await read(
  "app/api/admin/communications/notification/[key]/route.ts",
);
const communicationsSnippetRoute = await read(
  "app/api/admin/communications/snippet/[key]/route.ts",
);
const communicationsBff = await read("lib/admin-communications-bff.ts");
const snippetDefaults = await read("lib/system-snippet-defaults.ts");
const snippetsServer = await read("lib/system-snippets.ts");
const contactForm = await read("components/ContactForm.tsx");

assert.match(adminBff, /getInternalSession/);
assert.match(adminBff, /session\.user\.role !== "internal_admin"/);
assert.match(adminBff, /getInternalAdminData/);
assert.match(adminBff, /ACCESS_DENIED/);
assert.match(adminBff, /hasValidCsrfToken/);
assert.match(adminBff, /CSRF_FORBIDDEN/);
assert.doesNotMatch(
  adminBff,
  /localStorage|sessionStorage|NEXT_PUBLIC_INTERNAL_API_URL/i,
);

assert.match(adUsersRoute, /!customerReference/);
assert.match(adUsersRoute, /isValidPortalIdentifier/);
assert.match(adGroupsRoute, /!customerReference/);
assert.match(adGroupsRoute, /isValidPortalIdentifier/);
assert.match(adCreateUserRoute, /parseAdUserCreatePayload/);
assert.match(adCreateUserRoute, /INVALID_REQUEST/);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /\/internal\/admin\//);
assert.match(internalApi, /getAdminCustomer/);
assert.match(internalApi, /getAdminDownloads/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(appShell, /effectiveSession\?\.user\.role === "internal_admin"/);
assert.match(downloadsRoute, /handleAdminGet/);
assert.match(downloadsRoute, /handleAdminMutation/);
assert.match(downloadDetailRoute, /handleAdminGet/);
assert.match(downloadDetailRoute, /handleAdminMutation/);
assert.match(downloadFileRoute, /hasValidCsrfToken/);
assert.match(downloadCategoriesRoute, /handleAdminGet/);
assert.match(downloadCategoriesRoute, /handleAdminMutation/);
assert.match(settingsPage, /await requireAdminSession\(\)/);
assert.match(settingsRoute, /handleAdminGet/);
assert.match(settingsStatusRoute, /handleAdminGet/);
assert.match(settingsMutationRoute, /handleAdminMutation/);
assert.match(settingsMutationRoute, /expectedVersion/);
assert.match(settingsPermissionMigration, /settings\.read/);
assert.match(settingsPermissionMigration, /settings\.billing\.write/);
assert.match(settingsPermissionMigration, /users\.role = 'internal_admin'/);
assert.match(settingsCenter, /beforeunload/);
assert.match(settingsCenter, /SETTINGS_VERSION_CONFLICT/);
assert.doesNotMatch(settingsCenter, /SQL_PASSWORD|SERVICE_AUTH_TOKEN|ClientSecret/);

for (const route of [
  "overview",
  "customers",
  "customers/[customerReference]",
  "support-requests",
  "service-requests",
  "sessions",
  "audit-logs",
]) {
  const source = await read(`app/api/admin/${route}/route.ts`);
  assert.match(source, /handleAdminGet/);
}

const customerDetailRoute = await read(
  "app/api/admin/customers/[customerReference]/route.ts",
);
assert.match(customerDetailRoute, /isValidPortalIdentifier/);
assert.match(customerDetailRoute, /INVALID_REQUEST/);

for (const page of [
  "page.tsx",
  "customers/page.tsx",
  "customers/[customerReference]/page.tsx",
  "support-requests/page.tsx",
  "service-requests/page.tsx",
  "sessions/page.tsx",
  "audit-logs/page.tsx",
  "downloads/page.tsx",
  "downloads/new/page.tsx",
  "downloads/[id]/page.tsx",
  "downloads/categories/page.tsx",
]) {
  const source = await read(`app/admin/${page}`);
  assert.match(
    source,
    /await requireAdminSession\(\)/,
    `La page admin ${page} doit exiger le rôle internal_admin.`,
  );
  assert.doesNotMatch(
    source,
    /sessionToken|passwordHash|SQL_PASSWORD|INTERNAL_API_URL/,
  );
}

const customerDetailPage = await read(
  "app/admin/customers/[customerReference]/page.tsx",
);
assert.match(customerDetailPage, /getAdminCustomer/);
assert.match(customerDetailPage, /Isolation métier/);
assert.match(customerDetailPage, /Documents commerciaux associés/);
assert.match(customerDetailPage, /Audits récents du client/);
assert.match(customerDetailPage, /Active Directory V0\.18/);
assert.match(customerDetailPage, /controlled_write/);

// --- Messages & communications ---------------------------------------------
// Les gabarits transitent par le meme BFF controle que le reste de
// l'administration : session, role, CSRF, puis relais vers API-INTERNAL.
assert.match(communicationsPage, /await requireAdminSession\(\)/);
assert.match(communicationsPage, /getAdminCommunicationTemplates/);
assert.doesNotMatch(
  communicationsPage,
  /sessionToken|passwordHash|SQL_PASSWORD|INTERNAL_API_URL/,
);
assert.match(communicationsRoute, /handleAdminGet/);
assert.match(communicationsRoute, /\/internal\/admin\/communications/);
for (const route of [
  communicationsEmailRoute,
  communicationsNotificationRoute,
  communicationsSnippetRoute,
]) {
  assert.match(route, /handleAdminMutation/);
  assert.match(route, /expectedVersion/);
  assert.match(route, /isTemplateKey/);
}
assert.match(communicationsEmailRestoreRoute, /handleTemplateRestore/);
assert.match(communicationsEmailTestRoute, /handleAdminMutation/);
assert.match(communicationsBff, /import "server-only"/);
assert.match(communicationsBff, /handleAdminMutation/);
assert.match(communicationsBff, /\^\[a-z\]\[a-z0-9_\.\]/);
assert.doesNotMatch(communicationsBff, /SERVICE_AUTH_TOKEN|INTERNAL_API_URL/);

// L'interface doit exposer la liste fermee des variables, l'apercu, la
// restauration du modele de code et l'historique (specification, section 8.1).
assert.match(communicationsCenter, /Variables autorisées/);
assert.match(communicationsCenter, /Restaurer le modèle par défaut/);
assert.match(communicationsCenter, /\/preview/);
assert.match(communicationsCenter, /RevisionHistory/);
assert.match(communicationsCenter, /TEMPLATE_VERSION_CONFLICT/);
assert.match(communicationsCenter, /votre propre adresse/);

// Les textes systeme publics gardent un repli de code : jamais de chaine vide
// si API-INTERNAL est indisponible.
assert.match(snippetDefaults, /contact_form_confirmation/);
assert.match(snippetDefaults, /contact_form_privacy_notice/);
assert.match(snippetDefaults, /mergeSystemSnippets/);
assert.doesNotMatch(snippetDefaults, /import "server-only"/);
assert.match(snippetsServer, /import "server-only"/);
assert.match(snippetsServer, /getPublicSystemSnippets/);
assert.match(contactForm, /SYSTEM_SNIPPET_DEFAULTS\.contact_form_confirmation/);
assert.match(contactForm, /SYSTEM_SNIPPET_DEFAULTS\.contact_form_privacy_notice/);
assert.match(internalApi, /\/internal\/public\/system-snippets/);

// Audit de configuration : la liste des filtres transmis reste fermee cote BFF.
// Recopier la chaine de requete telle quelle laisserait passer des parametres
// inconnus vers API-INTERNAL.
for (const filter of [
  "from",
  "to",
  "actor",
  "category",
  "risk",
  "outcome",
  "correlationId",
  "target",
  "limit",
]) {
  assert.match(settingsAuditRoute, new RegExp(`"${filter}"`));
}
assert.match(settingsAuditRoute, /handleAdminGet/);
assert.match(settingsAuditRoute, /\/internal\/admin\/settings\/audit/);
assert.match(settingsPermissionsRoute, /handleAdminGet/);
assert.match(
  settingsPermissionsRoute,
  /\/internal\/admin\/settings\/permissions/,
);

// La page d'audit affiche l'avertissement du serveur plutot que de filtrer
// elle-meme : un filtre normalise cote portail pourrait diverger de la regle
// serveur et laisser croire a une recherche exhaustive.
assert.match(settingsAuditCenter, /audit\.warning/);
assert.match(settingsAuditCenter, /truncated/);
assert.match(settingsAuditCenter, /Refus.e sans attribution/);

// Federation : le Centre pointe vers les modules deja autorites, sans recreer
// un second editeur de CMS.
for (const href of [
  "/admin/content",
  "/admin/editorial",
  "/admin/catalog",
  "/admin/downloads",
  "/admin/backups",
  "/admin/koxo",
  "/admin/email-log",
]) {
  assert.match(settingsFederation, new RegExp(href.replace(/\//g, "\\/")));
}

// Presentation commerciale : le catalogue prime sur le libelle code, et aucun
// calcul de prix ne remonte cote portail.
assert.match(billingFormules, /resolveServiceBenefit/);
assert.match(billingFormules, /preset\.description\?\.trim\(\)/);
assert.match(billingFormules, /service\?\.description\?\.trim\(\)/);
assert.match(snippetDefaults, /checkout_not_open_yet/);
assert.match(snippetDefaults, /checkout_temporarily_unavailable/);

// Annuaire : la page reste en lecture. Une mutation ici permettrait d'elargir
// la portee d'ecriture sur un annuaire de production depuis un navigateur.
assert.match(settingsDirectoryRoute, /handleAdminGet/);
assert.match(settingsDirectoryRoute, /\/internal\/admin\/settings\/directory/);
assert.doesNotMatch(settingsDirectoryRoute, /handleAdminMutation|export function (POST|PUT|PATCH|DELETE)/);
assert.doesNotMatch(directoryCenter, /"use client"/);
assert.match(directoryCenter, /valeur jamais transmise/);
assert.match(directoryCenter, /writesNotice/);

// Atomicité mutation + révision. Une valeur appliquée sans trace est
// indistinguable d'une valeur jamais modifiée : c'est exactement ce qu'un audit
// de configuration doit pouvoir trancher.
const atomicWrites = [
  ["apps/api-internal/Data/Repositories/MariaDbApplicationSettingsRepository.cs", "TryApplyAsync", "application_setting_revisions"],
  ["apps/api-internal/Data/Repositories/MariaDbCommunicationTemplateRepository.cs", "SaveAsync", "revision"],
  ["apps/api-internal/Data/Repositories/MariaDbDemoContentTemplateRepository.cs", "TrySaveAsync", "demo_content_template_revisions"],
];
for (const [file, method, revisionMarker] of atomicWrites) {
  const source = await readRepo(file);
  const start = source.indexOf(method);
  assert.notEqual(start, -1, `${file} doit exposer ${method}.`);
  assert.match(
    source,
    /BeginTransactionAsync/,
    `${file} doit écrire la mutation et sa révision dans une transaction.`,
  );
  assert.ok(
    source.includes(revisionMarker),
    `${file} doit inscrire la révision dans la même unité de travail.`,
  );
  assert.match(
    source,
    /FOR UPDATE/,
    `${file} doit vérifier la version sous verrou, pas sur une lecture antérieure.`,
  );
}

// Les dépôts n'exposent plus d'historisation séparée : la séparation est
// justement ce qui permettait la mutation sans trace.
for (const file of [
  "apps/api-internal/Data/Repositories/IApplicationSettingsRepository.cs",
  "apps/api-internal/Data/Repositories/ICommunicationTemplateRepository.cs",
  "apps/api-internal/Data/Repositories/IDemoContentTemplateRepository.cs",
]) {
  const source = await readRepo(file);
  assert.doesNotMatch(
    source,
    /Task AddRevisionAsync|Task Add\w+RevisionAsync/,
    `${file} ne doit plus exposer d'écriture de révision indépendante.`,
  );
}

// Amorce des modèles de démonstration : tout ou rien. Une table à moitié peuplée
// est ensuite considérée comme faisant autorité, et les modèles manquants
// deviennent invisibles sans possibilité de réamorcer.
const demoRepository = await readRepo(
  "apps/api-internal/Data/Repositories/MariaDbDemoContentTemplateRepository.cs",
);
assert.match(
  demoRepository,
  /TryImportAsync/,
  "L'amorce des modèles de démonstration doit être une seule opération.",
);
assert.match(
  demoRepository,
  /SELECT COUNT\(\*\) FROM demo_content_templates FOR UPDATE/,
  "La vacuité de la table doit être vérifiée dans la transaction d'amorce.",
);

// Concurrence fiscale : la version attendue est vérifiée sous verrou, dans la
// transaction qui écrit. Vérifiée en amont, deux administrateurs partis du même
// écran passaient tous les deux sans voir de conflit.
const fiscalRepository = await readRepo(
  "apps/api-internal/Data/Repositories/MariaDbFiscalPolicyRepository.cs",
);
assert.match(fiscalRepository, /BeginTransactionAsync/);
assert.match(fiscalRepository, /FOR UPDATE/);
assert.match(fiscalRepository, /expectedVersion/);
const fiscalService = await readRepo("apps/api-internal/Services/FiscalPolicyService.cs");
assert.match(
  fiscalService,
  /FiscalMentionAddOutcome\.VersionConflict/,
  "Le conflit de version fiscal doit venir du dépôt, pas d'un décompte lu en amont.",
);

// La version d'un régime ne redescend jamais. Le décompte des mentions ne peut
// pas jouer ce rôle : `TryDeleteScheduledAsync` supprime réellement une ligne,
// donc après « ajout, ajout, annulation » le décompte retrouve sa valeur d'avant
// et un `expectedVersion` périmé redevient acceptable — sur un texte imprimé sur
// des factures. Le verrou porte donc sur une ligne présente, ce qui le rend
// aussi indépendant des verrous d'intervalle, absents en READ COMMITTED.
assert.match(
  fiscalRepository,
  /fiscal_policy_regime_versions/,
  "La version fiscale doit être une colonne monotone, pas un décompte de lignes.",
);
assert.match(
  fiscalRepository,
  /DELETE FROM fiscal_policy_mentions[\s\S]{0,900}?BumpRegimeVersionAsync/,
  "Une annulation doit incrémenter la version dans la même transaction.",
);
// Mentions et version viennent de la MÊME lecture. Assemblées séparément, elles
// peuvent décrire deux instants : l'administrateur repart alors avec un
// `expectedVersion` correspondant à un écran qu'il n'a jamais vu.
assert.match(
  fiscalService,
  /GetSnapshotAsync/,
  "La vue d'administration doit venir d'une seule unité de lecture.",
);
assert.doesNotMatch(
  fiscalService,
  /GetRegimeVersionsAsync/,
  "Assembler mentions et version par deux lectures rouvre la fenêtre d'incohérence.",
);
assert.match(
  fiscalRepository,
  /BeginTransactionAsync\([\s\S]{0,80}?IsolationLevel\.RepeatableRead/,
  "Le snapshot fiscal doit lire les deux tables dans le même instantané.",
);

// Les permissions du Centre sont fail-closed : sans attribution explicite,
// personne n'y accède. Le bootstrap permissif reste borné à l'éditorial.
const editorialRepository = await readRepo(
  "apps/api-internal/Data/Repositories/MariaDbEditorialRepository.cs",
);
assert.match(
  editorialRepository,
  /SettingsPermissionRegistry\.Contains/,
  "Une permission du Centre sans attribution doit être refusée, pas ouverte par amorçage.",
);

console.log("Vérification du contrat d'administration BFF réussie.");
