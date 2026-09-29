// Garde-fou DEV/PROD evalue une fois, au demarrage du serveur Node : une
// instance mal configuree s'arrete avant de servir la moindre requete. Le
// module n'est importe que sous Node (process.exit n'existe pas en Edge).
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { enforceDeploymentEnvironment } = await import(
      "./lib/deployment-environment-startup"
    );
    await enforceDeploymentEnvironment();
  }
}
