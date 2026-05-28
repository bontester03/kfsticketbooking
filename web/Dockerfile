# Single Dockerfile builds all three apps from the monorepo, then a runtime stage selects
# one via the APP build-arg. Builders share the install layer so the cache survives.
ARG APP=portal

FROM node:20-alpine AS base
RUN corepack enable && corepack prepare pnpm@9.7.0 --activate
WORKDIR /repo

FROM base AS deps
COPY pnpm-workspace.yaml package.json .npmrc ./
COPY turbo.json tsconfig.base.json ./
COPY packages ./packages
COPY apps ./apps
RUN pnpm install --frozen-lockfile=false

FROM deps AS build
ARG APP
RUN pnpm --filter ${APP} build

FROM nginx:1.27-alpine AS runtime
ARG APP
RUN rm -rf /usr/share/nginx/html/*

# Render nginx.conf with ${API_HOST}:${API_PORT} at container start. The official
# nginx:alpine entrypoint runs envsubst on /etc/nginx/templates/*.template.
# Defaults below match docker-compose (service name `api`); on Railway override
# API_HOST=api.railway.internal as a service variable.
COPY apps/${APP}/nginx.conf /etc/nginx/templates/default.conf.template
ENV API_HOST=api \
    API_PORT=8080 \
    NGINX_ENVSUBST_FILTER=^(API_HOST|API_PORT)$

COPY --from=build /repo/apps/${APP}/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost/ || exit 1
