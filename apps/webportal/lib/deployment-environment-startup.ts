import {
  CONFIGURATION_EXIT_CODE,
  describeDeploymentEnvironment,
  findDeploymentEnvironmentViolations,
  findInternalApiIdentityViolation,
} from "./deployment-environment";

export async function enforceDeploymentEnvironment() {
  const violations = findDeploymentEnvironmentViolations();
  const apiIdentityViolation = violations.length === 0
    ? await findInternalApiIdentityViolation()
    : null;
  if (apiIdentityViolation) {
    violations.push(apiIdentityViolation);
  }

  if (violations.length > 0) {
    for (const violation of violations) {
      console.error(`FATAL ${violation}`);
    }
    console.error(
      `FATAL WEBPORTAL refuses to start: DEV/PROD separation violated (${violations.length} issue(s)). Exit code ${CONFIGURATION_EXIT_CODE}.`,
    );
    process.exit(CONFIGURATION_EXIT_CODE);
  }

  for (const line of describeDeploymentEnvironment()) {
    console.log(`Deployment environment | ${line}`);
  }
}
