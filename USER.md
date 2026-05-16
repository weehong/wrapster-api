# Test Accounts

## Keycloak Admin Console

- **URL**: http://localhost:18080/admin
- **Username**: admin
- **Password**: admin

## Main Realm (`wrapsfer`)

| Username    | Password                          | Roles  |
|-------------|-----------------------------------|--------|
| owneradmin  | see `KEYCLOAK_OWNER_ADMIN_PASSWORD` | admin |

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

- **Client ID**: wrapsfer
- **Client Secret**: wrapsfer-secret
- **Grant Type**: password

```
POST http://localhost:18080/realms/{realm}/protocol/openid-connect/token
```
