# Test Accounts

## Keycloak Admin Console

- URL: `http://localhost:18080/admin`
- Username: `admin`
- Password: `admin`

## Owner Realm `wrapsfer`

| Username | Password | Roles |
|----------|----------|-------|
| owneradmin | `KEYCLOAK_OWNER_ADMIN_PASSWORD`, default `ChangeMe-12345!` | admin |

## Partner Realms

Partner realms are not imported by default. Create them through `POST /api/v1/partners` while authenticated as `owneradmin`.

Example partner admin accounts are determined by the request body you send to `POST /api/v1/partners`, for example `adminUsername` and `temporaryPassword`.

Static example realm exports are available under `keycloak/fixtures/`, but they are fixtures only and are not part of the normal local setup.

## Token Requests

All realms use the same API OIDC client:

- Client ID: `wrapsfer`
- Client secret: `wrapsfer-secret`
- Grant type: `password`

Direct Keycloak token endpoint:

```text
POST http://localhost:18080/realms/{realm}/protocol/openid-connect/token
```

API login endpoint:

```text
POST http://localhost:5222/api/v1/auth/login
```

For partner login through the API on localhost, include `X-Tenant-Realm: {partner-tenant-id}` so the API resolves the partner realm instead of the owner realm.
