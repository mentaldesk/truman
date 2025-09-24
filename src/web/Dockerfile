# Build stage
FROM node:20-alpine as build

WORKDIR /app
COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .

# Clear cache to ensure fresh build
RUN rm -rf .svelte-kit node_modules/.vite

RUN yarn build

# Production stage
FROM nginx:alpine

ARG APP_VERSION=dev
ARG GIT_SHA=unknown
ARG REPO_URL="https://github.com/mentaldesk/truman"
LABEL org.opencontainers.image.title="truman-web" \
      org.opencontainers.image.version="$APP_VERSION" \
      org.opencontainers.image.revision="$GIT_SHA" \
      org.opencontainers.image.source="$REPO_URL" \
      org.opencontainers.image.vendor="truman"

# Copy the built assets from build stage
COPY --from=build /app/build /usr/share/nginx/html

# Write version for runtime exposure
RUN echo "$APP_VERSION" > /usr/share/nginx/html/version.txt

# Copy default config.js for local dev (will be overwritten in container)
COPY static/config.js /usr/share/nginx/html/config.js

# Copy custom nginx config to listen on port 80
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy entrypoint script for runtime config injection
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/entrypoint.sh"]
