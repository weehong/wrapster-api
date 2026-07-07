# Optimized Keycloak image: `kc.sh build` bakes in the DB vendor + health so the
# runtime `start --optimized` skips the ~80s Quarkus augmentation on every boot.
FROM quay.io/keycloak/keycloak:26.2.5 AS builder
ENV KC_DB=postgres
ENV KC_HEALTH_ENABLED=true
RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:26.2.5
COPY --from=builder /opt/keycloak/ /opt/keycloak/
