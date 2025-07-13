# Build stage
FROM node:20-alpine as build

WORKDIR /app
COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .

RUN yarn build

# Production stage
FROM nginx:alpine

# Copy the built assets from build stage
COPY --from=build /app/build /usr/share/nginx/html

# Copy default config.js for local dev (will be overwritten in container)
COPY config.js /usr/share/nginx/html/config.js

# Copy custom nginx config to listen on port 80
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy entrypoint script for runtime config injection
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/entrypoint.sh"] 