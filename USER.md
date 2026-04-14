# Test Accounts

## Keycloak Admin Console

- **URL**: http://localhost:18080/admin
- **Username**: admin
- **Password**: admin

## Owner Realm (`owner`)

| Username    | Password    | Roles        |
|-------------|-------------|--------------|
| testuser    | testpassword | admin, user |
| owneradmin  | owneradmin   | admin       |

## Partner Alpha Realm (`partner-alpha`)

| Username    | Password    | Roles |
|-------------|-------------|-------|
| alphaadmin  | alphaadmin   | admin |

## Partner Beta Realm (`partner-beta`)

| Username    | Password    | Roles |
|-------------|-------------|-------|
| betaadmin   | betaadmin    | admin |

## Token Request

All realms use the same client:

- **Client ID**: wrapsfer-api
- **Client Secret**: wrapsfer-api-secret
- **Grant Type**: password

```
POST http://localhost:18080/realms/{realm}/protocol/openid-connect/token
```
