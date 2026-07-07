These partner realm exports are static examples only.

The normal local and production setup imports only `keycloak/realm-export.json` for the owner realm. Partner realms should be created through `POST /api/v1/partners`, which keeps the database partner record and the Keycloak realm in sync.
