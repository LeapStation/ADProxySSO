# ADProxy - AAD to Born-in-Belgium (BiB) proxy

### Overview

ADProxy is a small ASP.NET Core Razor Pages app that acts as a proxy between a local Azure AD (B2C) tenancy and LeapStation Healthcare services like Born-in-Belgium, CuraeConsent, Neoparent, ...

This is a backup service for use cases where direct EPD integration is not possible, and the user needs to authenticate with their Azure AD credentials to access BiB services.

Contact support@leapstation.eu for access credentials. For The Born-in-Belgium (BiB) Professionals platform, following the flow documented at https://leapstation.eu/doc/bib (the API is similar except that we do not pass patient information).

The app authenticates users with Azure AD (OpenID Connect), then requests a machine-to-machine token from an OAuth token endpoint (configured under TokenSettings) to call the BiB endpoint /epd/access-ad. The BiB / Neoparent API responds with a URL which the user is redirected to.

### Important configuration keys

- AzureAd: Section used by Microsoft.Identity.Web for OpenID Connect. Common keys include:
  - Instance: Azure AD B2C instance URL (e.g. https://<tenant>.b2clogin.com)
  - ClientId: The client id of the registered app
  - Domain: The tenant domain
  - CallbackPath: OIDC callback path
  - SignUpSignInPolicyId / EditProfilePolicyId: B2C policies if using B2C

- TokenSettings: Used to request a machine-to-machine token to call BiB / Neoparent / ...
  - ClientId
  - ClientSecret
  - ClientEndpoint: Token endpoint URL (e.g. https://login.microsoftonline.com/<tenant>/oauth2/v2.0/token)
  - ClientScope: Scope to request (for client credentials, usually the API app id URI + '/.default')

- serviceUrl: The base URL for the BiB / Neoparent / ... platform (used to POST to {serviceUrl}/epd/access-ad). For local testing, set to https://localhost:5001.

- TokenCacheMinutes: How long to cache the client token in distributed cache (minutes).

### Running locally (Development)

1. Ensure  your Azure AD and TokenSettings values are available for configuration (files or environment variables).

2. From the project folder (where ADProxy.csproj lives) run:

```powershell
dotnet run --project ADProxy.csproj
```

This will start the app; open the configured URL in the browser.

Error diagnostics

When an authentication failure happens the app will:
- Generate an error id (GUID)
- Store a JSON blob under the key `error:{errorid}` in the configured IDistributedCache (by default this is an in-memory cache for local runs)
- Redirect the user to `/Error?errorid={errorid}` where the page will display the error id and the JSON (if available)

This helps correlate logs and user-visible errors during testing.

### Customization
Depending on your setup it might be necessary to customize claim mapping (e.g. if BiB expects a specific claim for the user identifier), or inject additional claims (e.g. organization name). However, for most cases the basic information should be sufficient.