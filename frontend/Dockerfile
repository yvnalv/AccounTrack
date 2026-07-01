# Accountrack SPA — build the Vue/Vite bundle, serve it with Nginx which also reverse-proxies /api to
# the API container (same-origin, so no CORS). This container is the single HTTP entry point; your
# external reverse proxy terminates TLS and routes a subdomain to it (DEPLOYMENT.md §2).

# --- build ---
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# --- runtime ---
FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
