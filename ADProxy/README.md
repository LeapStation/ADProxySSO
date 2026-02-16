ADProxy - AAD to Born-in-Belgium (BiB) proxy

Overview

ADProxy is a small ASP.NET Core Razor Pages app that acts as a proxy between a local Azure AD (B2C) tenancy and the Born-in-Belgium (BiB) Professionals platform, following the flow documented at https://leapstation.eu/doc/bib.

The app authenticates users with Azure AD (OpenID Connect), then requests a machine-to-machine token from an OAuth token endpoint (configured under TokenSettings) to call the BiB endpoint /epd/access-ad. The BiB API responds with a URL which the user is redirected to.

What I changed

- Added robust logging and diagnostic support for authentication failures: when an OpenID Connect authentication failure happens the app stores a JSON blob describing the error in the distributed memory cache and redirects the user to /Error?errorid={id}. The Error page reads the cached details and displays them to aid debugging.
- Configured local settings to use HTTPS port 5001 and set the default bibUrl to https://localhost:5001 (edit as needed).
- Added placeholders and example configuration values in `appsettings.local.json`.

Important configuration keys

- AzureAd: Section used by Microsoft.Identity.Web for OpenID Connect. Common keys include:
  - Instance: Azure AD B2C instance URL (e.g. https://<tenant>.b2clogin.com)
  - ClientId: The client id of the registered app
  - Domain: The tenant domain
  - CallbackPath: OIDC callback path
  - SignUpSignInPolicyId / EditProfilePolicyId: B2C policies if using B2C

- TokenSettings: Used to request a machine-to-machine token to call BiB
  - ClientId
  - ClientSecret
  - ClientEndpoint: Token endpoint URL (e.g. https://login.microsoftonline.com/<tenant>/oauth2/v2.0/token)
  - ClientScope: Scope to request (for client credentials, usually the API app id URI + '/.default')

- bibUrl: The base URL for the BiB platform (used to POST to {bibUrl}/epd/access-ad). For local testing, set to https://localhost:5001.

- TokenCacheMinutes: How long to cache the client token in distributed cache (minutes).

Running locally (Development)

1. Ensure `appsettings.local.json` contains your Azure AD and TokenSettings values. For local testing we configure Kestrel to listen on https://localhost:5001.

2. From the project folder (where ADProxy.csproj lives) run:

```powershell
dotnet run --project ADProxy.csproj
```

This will start the app; open https://localhost:5001 in your browser. Ensure you have a valid certificate for localhost or trust the dev certificate (dotnet dev-certs https --trust).

Error diagnostics

When an authentication failure happens the app will:
- Generate an error id (GUID)
- Store a JSON blob under the key `error:{errorid}` in the configured IDistributedCache (by default this is an in-memory cache for local runs)
- Redirect the user to `/Error?errorid={errorid}` where the page will display the error id and the JSON (if available)

This helps correlate logs and user-visible errors during testing.

Security notes

- `appsettings.local.json` may contain secrets (ClientSecret). Never commit secrets to source control. Use environment variables or a secret store (KeyVault) in production.
- The in-memory distributed cache is only suitable for single-instance development scenarios. For production use a shared cache (Redis, SQL distributed cache, etc.) so multiple instances share error state.

Next steps / suggestions

- Add structured logging sinks (e.g. Seq, Application Insights) for better observability.
- Replace DistributedMemoryCache with Redis for production if you run multiple instances.
- Harden error display to avoid leaking sensitive data in production (the /Error page currently displays raw JSON for debugging).


