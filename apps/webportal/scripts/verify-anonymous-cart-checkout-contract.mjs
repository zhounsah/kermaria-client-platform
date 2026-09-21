import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const checkoutReview = await read("components/BillingV2CartCheckoutReview.tsx");
const signupRoute = await read("app/api/signup/route.ts");
const signupPage = await read("app/signup/page.tsx");
const signupVerifyPage = await read("app/signup/verify/page.tsx");
const signupForm = await read("components/SignupForm.tsx");
const loginPage = await read("app/login/page.tsx");
const signupServer = await read("lib/signup-server.ts");
const cartRoute = await read("app/api/billing-v2/cart/route.ts");
const cartCookie = await read("lib/cart-cookie.ts");
const routeConfig = await read("lib/public-route-config.ts");
const signupContracts = await read("../../apps/api-internal/Contracts/SignupContracts.cs");
const signupService = await read("../../apps/api-internal/Services/SignupService.cs");
const program = await read("../../apps/api-internal/Program.cs");
const cartService = await read("../../apps/api-internal/Services/BillingV2CartService.cs");
const checkoutRoute = await read("app/api/billing-v2/cart/checkout/route.ts");
const emailVerificationRoute = await read("app/api/auth/email-verification/route.ts");
const signupRepository = await read("../../apps/api-internal/Data/Repositories/ISignupRepository.cs");
const signupRepositoryMaria = await read("../../apps/api-internal/Data/Repositories/MariaDbSignupRepository.cs");
const signupMigration = await read("../../apps/api-internal/Migrations/MariaDb/095_signup_self_service_email_verification.sql");

assert.match(checkoutReview, /requestBffJson<AuthMeResponse>\("\/api\/auth\/me"/,
  "La souscription doit distinguer anonymat et session avant tout claim.");
assert.match(checkoutReview, /state === "anonymous"[\s\S]*Créer mon compte et continuer/,
  "L'etat anonyme doit faire de la creation de compte l'action principale.");
assert.match(checkoutReview, /Vous avez déjà un compte \?<\/?Link|Vous avez déjà un compte \? <Link/,
  "La connexion doit rester l'action secondaire.");
assert.match(checkoutReview, /if \(authenticated\)[\s\S]*command: "claim_current"/,
  "claim_current ne doit etre appele qu'apres authentification.");
assert.match(checkoutReview, /CART_CLAIM_CONFLICT[\s\S]*claim_conflict/,
  "Un conflit de claim doit rester explicite et ne jamais fusionner silencieusement.");
assert.match(checkoutReview, /CART_NOTHING_TO_CLAIM/,
  "Seul le resultat metier explicite d'absence de Cart anonyme peut etre un no-op UI.");
assert.doesNotMatch(checkoutReview, /CART_COMMAND_INVALID" &&/,
  "Une commande Cart invalide ne doit jamais etre assimilee a l'absence de Cart a claim.");
assert.match(checkoutReview, /setAccepted\(false\)/,
  "Toute reprise doit invalider une ancienne confirmation de prix locale.");
assert.doesNotMatch(checkoutReview, /Valable jusqu’au|formatQuoteExpiry/,
  "Le TTL technique ne doit pas etre expose publiquement.");
assert.match(checkoutReview, /state === "anonymous"[\s\S]*subscription-review-account-step[\s\S]*\) : \(/,
  "La checkbox et le POST checkout ne doivent apparaitre qu'apres claim.");
assert.match(checkoutReview, /buildAccountContinuation\("signup", cart\.id\)/,
  "La continuation signup doit conserver le cartId public cible.");
assert.match(checkoutReview, /buildAccountContinuation\("login", cart\.id\)/,
  "La continuation login doit conserver le cartId public cible.");
assert.match(routeConfig, /resolveSelfServiceCartSignupContinuation/,
  "La continuation Cart doit etre validee par la primitive existante de routage sur allowlist.");
assert.match(routeConfig, /pathname === "\/souscription"[\s\S]*searchParams\.size/,
  "La continuation souscription doit refuser les parametres inattendus.");
assert.match(signupPage, /flow === "cart_checkout"[\s\S]*resolveSelfServiceCartSignupContinuation/,
  "Le signup doit accepter seulement le flow Cart explicitement borne.");
assert.match(loginPage, /resolveSelfServiceCartSignupContinuation/,
  "Le login doit proposer le meme retour Cart controle.");
assert.match(signupForm, /selfServiceCart:[\s\S]*cartId: selfServiceCart\.cartId/,
  "Le formulaire ne transmet au BFF que l'identifiant public du Cart, jamais son secret.");
assert.match(signupRoute, /readAnonymousCartToken\(\)[\s\S]*command: "get"[\s\S]*anonymousToken/,
  "Le BFF doit prouver la possession du Cart par son cookie HttpOnly avant signup.");
assert.match(signupRoute, /ownedCart\.cart\?\.id !== selfServiceCart\.cartId[\s\S]*ownedCart\.cart\.status !== "open"/,
  "Le BFF doit refuser un Cart absent, non possede ou non open.");
assert.match(cartCookie, /getPortalFamilyCookieDomain/,
  "Le cookie Cart doit survivre au passage public/dashboard dans une meme famille de portails.");
assert.match(cartCookie, /httpOnly: true|\.\.\.getSessionCookieOptions\(\)/,
  "Le token Cart reste HttpOnly.");
assert.doesNotMatch(signupRoute, /cartToken|anonymousSessionHash/,
  "Le BFF signup ne doit exposer ni token Cart ni hash de possession.");
assert.match(signupContracts, /SignupSelfServiceCartIntent/,
  "L'API distingue explicitement l'intention signup Cart du paiement.");
assert.match(signupService, /CompleteSelfServiceCartAsync[\s\S]*CompleteSelfServiceAccountAsync/,
  "Le signup Cart reutilise la primitive customer/session existante sans domaine parallele.");
assert.match(signupService, /Cette primitive ne claim pas le Cart et[\s\S]*n'ouvre aucun checkout/,
  "La creation de compte ne doit ni claim ni demarrer le checkout.");
assert.match(program, /signup\.self_service_cart/,
  "La creation immediate Cart doit etre auditee distinctement.");
assert.match(checkoutRoute, /readPortalSessionToken[\s\S]*AUTH_REQUIRED/,
  "Le POST checkout direct reste refuse sans session authentifiee.");
assert.match(cartRoute, /result\.code === "CART_CLAIMED"[\s\S]*clearAnonymousCartToken/,
  "Le claim reussi doit supprimer le cookie anonyme precedent.");
assert.match(program, /"claim_current" when owner\.IsAuthenticated\s*=> new BillingV2CartMutationResult\("CART_NOTHING_TO_CLAIM"\)/,
  "Une session sans cookie Cart anonyme doit obtenir un resultat specifique plutot qu'une commande invalide.");
assert.match(cartService, /ClaimCurrentAsync[\s\S]*if \(cart is null\)[\s\S]*return new\("CART_NOTHING_TO_CLAIM"\)/,
  "Un token Cart valide mais sans Cart open doit etre un no-op explicite du service.");
const anonymousAccountStep = checkoutReview.slice(
  checkoutReview.indexOf('{state === "anonymous" ? ('),
  checkoutReview.indexOf(') : (', checkoutReview.indexOf('{state === "anonymous" ? (')),
);
assert(!anonymousAccountStep.includes("startCheckout"),
  "L'UI anonyme ne doit pas appeler checkout.");
assert.match(signupService, /SelfServiceFlow: selfServiceFlow[\s\S]*EmailVerified: false/,
  "Le self-service doit creer l'identite avant verification sans simuler MarkEmailVerifiedAsync.");
assert.match(signupService, /RotateSelfServiceVerificationTokenByEmailAsync[\s\S]*SendVerificationEmailSafelyAsync/,
  "Une adresse self-service non verifiee doit pouvoir renouveler son lien sans creer de second compte.");
assert.match(signupRepository, /GetPortalUserEmailVerificationStateAsync\(\s*string portalUserId/,
  "Le gate doit etre lie a l'identite portail de la session, pas au customer entier.");
assert.match(signupRepositoryMaria, /WHERE u\.id = @portal_user_id[\s\S]*approved_user_id = u\.id/,
  "La projection MariaDB doit evaluer le portal_user courant et son workflow self-service lie.");
assert.match(signupRepositoryMaria, /verification_token_hash = @hash[\s\S]*verification_token_expires_at = @expires_at/,
  "Le renvoi doit persister uniquement le hash d'un nouveau token et son TTL.");
assert.match(signupRepositoryMaria, /updatedAtUtc > resendAllowedBeforeUtc|updated_at DESC, id DESC/,
  "Le renvoi doit etre borne par une rotation atomique et un cooldown durable.");
assert.doesNotMatch(signupMigration, /UPDATE portal_users/,
  "095 ne doit pas marquer globalement les identites historiques comme verifiees.");
assert.match(emailVerificationRoute, /hasValidCsrfToken\(request\)[\s\S]*resendInternalEmailVerification/,
  "Le renvoi de verification doit conserver le CSRF commun du BFF.");
assert.match(checkoutReview, /emailVerificationRequired[\s\S]*Vérifiez votre adresse e-mail[\s\S]*Renvoyer l’e-mail de vérification/,
  "Une identite non verifiee doit voir une etape de verification sans checkbox ni CTA checkout.");
assert.doesNotMatch(checkoutReview, /Le montant sera vérifié une dernière fois avant le paiement\./,
  "La revue publique ne doit pas exposer une explication technique de revalidation.");
assert.match(checkoutReview, /notifyBillingV2CartChanged\(\)[\s\S]*loadRecovery\(cart\.id\)/,
  "Un checkout reussi doit demander au badge de relire le seul Cart open, sans rouvrir le Cart finalise.");
assert.match(signupVerifyPage, /result\.selfServiceFlow === "cart"[\s\S]*resolveSelfServiceCartSignupContinuation\(next\)/,
  "La continuation Cart est affichee seulement apres un resultat de verification self-service Cart confirme par le serveur.");
assert.match(signupVerifyPage, /Votre compte est prêt\. Vous pouvez maintenant reprendre votre commande\.[\s\S]*Continuer ma souscription/,
  "Le workflow Cart verifie doit rendre son CTA commercial dedie.");
assert.match(signupVerifyPage, /resolveSelfServiceVpsSignupContinuation\(next\)[\s\S]*Reprendre mon VPS/,
  "Le workflow VPS garde sa continuation distincte et validee.");
assert.match(signupVerifyPage, /en attente de validation par notre équipe/,
  "Le workflow standard conserve le message historique d'approbation humaine.");
assert.match(signupVerifyPage, /selfService \? \([\s\S]*continuation \?[\s\S]*href=\{continuation\}/,
  "Une continuation fournie par un parametre navigateur n'est jamais rendue sans revalidation par les helpers d'allowlist.");
assert.match(signupService, /BuildSelfServiceContinuationPath\(payload, selfServiceFlow\)[\s\S]*BuildUrl\("\/signup\/verify", verificationToken, continuationPath\)/,
  "Le lien de verification transporte uniquement la continuation commerciale relative deja validee, sans secret Cart.");
assert.match(signupService, /Guid\.TryParseExact\(payload\.SelfServiceCartIntent\?\.CartId, "D"[\s\S]*return \$"\/souscription\?cart=/,
  "La continuation Cart est construite depuis un identifiant public UUID, jamais depuis le token anonyme.");
assert.match(program, /"\/internal\/signup\/verify"[\s\S]*selfServiceFlow = result\.SelfServiceFlow is "cart" or "vps"/,
  "L'endpoint de verification ne projette que les deux workflows self-service autorises.");
assert.match(signupServer, /selfServiceFlow\?: "cart" \| "vps"[\s\S]*payload\?\.selfServiceFlow === "cart" \|\| payload\?\.selfServiceFlow === "vps"/,
  "Le BFF borne egalement la projection de flow avant le rendu public.");

console.log("Contrat tunnel anonyme -> compte -> claim Cart -> checkout verifie.");
