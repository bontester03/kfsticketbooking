# Custom Caddy build that bundles the rate-limit module.
# The default caddy:2-alpine doesn't include any rate-limiting; we add
# github.com/mholt/caddy-ratelimit via xcaddy so we can rate-limit the
# /auth/login endpoint and stop accidental retry storms during the event.

FROM caddy:2-builder AS builder
RUN xcaddy build \
    --with github.com/mholt/caddy-ratelimit

FROM caddy:2-alpine
COPY --from=builder /usr/bin/caddy /usr/bin/caddy
