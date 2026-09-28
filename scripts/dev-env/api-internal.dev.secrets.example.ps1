# Source optionnelle hors Git : copier ce fichier hors du depot sous un nom
# explicite, par exemple kermaria-client-platform.api-dev.secrets.ps1.
#
# Le script Install-ApiInternalDev.ps1 n'execute jamais ce fichier. Il accepte
# uniquement des affectations litterales $env:DEV_API_* pour les secrets ci-dessous.
# Ne pas ajouter de variables generiques (SQL_*, AD_*, KOXO_*...) ni de commandes.

# $env:DEV_API_SQL_HOST = '<hote-dev>'
# $env:DEV_API_SQL_PORT = '<port-dev>'
# $env:DEV_API_SQL_USERNAME = '<compte-dev>'
# $env:DEV_API_SQL_PASSWORD = '<secret>'
# $env:DEV_API_SERVICE_AUTH_TOKEN = '<secret>'
# $env:DEV_API_STRIPE_SECRET_KEY = '<cle-stripe-test>'
# $env:DEV_API_STRIPE_PUBLISHABLE_KEY = '<cle-publique-stripe-test>'
# $env:DEV_API_STRIPE_WEBHOOK_SECRET = '<secret-webhook-stripe-test>'
# $env:DEV_API_AD_SERVICE_ACCOUNT_USERNAME = '<compte-ad-dev>'
# $env:DEV_API_AD_SERVICE_ACCOUNT_PASSWORD = '<secret>'
# $env:DEV_API_SMTP_USERNAME = '<identifiant-smtp>'
# $env:DEV_API_SMTP_PASSWORD = '<secret>'
# $env:DEV_API_KOXO_SYNC_WEBHOOK_TOKEN = '<secret>'
# $env:DEV_API_KOXO_PENDING_PASSWORD_KEY = '<cle-de-scellement-dev>'
