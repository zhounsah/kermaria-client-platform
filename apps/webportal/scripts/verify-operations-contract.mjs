import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const liveRoute = await read("app/api/health/live/route.ts");
const readyRoute = await read("app/api/health/ready/route.ts");
const runtimeConfig = await read("lib/runtime-config.ts");
const internalApi = await read("lib/internal-api.ts");
const nextConfig = await read("next.config.ts");
const robots = await read("app/robots.ts");

assert.match(liveRoute, /status:\s*"healthy"/);
assert.match(liveRoute, /check:\s*"live"/);
assert.match(liveRoute, /timestamp_utc/);
assert.doesNotMatch(liveRoute, /INTERNAL_API_URL|SERVICE_AUTH_TOKEN/);

assert.match(readyRoute, /checkInternalApiReadiness/);
assert.match(readyRoute, /validateSessionCookieConfiguration/);
assert.match(readyRoute, /status:\s*ready \? "healthy" : "unhealthy"/);
assert.match(readyRoute, /status:\s*ready \? 200 : 503/);
assert.doesNotMatch(readyRoute, /process\.env|INTERNAL_API_URL|SERVICE_AUTH_TOKEN/);

assert.match(runtimeConfig, /import "server-only"/);
assert.match(runtimeConfig, /process\.env\.INTERNAL_API_URL/);
assert.match(runtimeConfig, /process\.env\.SERVICE_AUTH_TOKEN/);
assert.match(runtimeConfig, /ALLOW_LOCAL_INTERNAL_API_URL/);
assert.doesNotMatch(
  runtimeConfig,
  /NEXT_PUBLIC_INTERNAL_API_URL|PUBLIC_INTERNAL_API_URL/,
);

assert.match(internalApi, /getInternalApiUrl/);
assert.match(internalApi, /getInternalServiceHeaders/);
assert.match(internalApi, /\/health\/ready/);

assert.match(nextConfig, /X-Content-Type-Options/);
assert.match(nextConfig, /X-Frame-Options/);
assert.match(nextConfig, /Content-Security-Policy/);
assert.match(nextConfig, /Referrer-Policy/);
assert.match(nextConfig, /Permissions-Policy/);
assert.match(nextConfig, /Cross-Origin-Opener-Policy/);
assert.match(nextConfig, /Cross-Origin-Resource-Policy/);
assert.match(nextConfig, /X-Robots-Tag/);
assert.match(nextConfig, /noindex, nofollow/);

assert.match(robots, /disallow:\s*"\/"/);

console.log("Vérification du contrat d'exploitation WEBPORTAL réussie.");
