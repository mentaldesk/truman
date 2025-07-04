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

# Copy a custom nginx config if needed
# COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 3000

CMD ["nginx", "-g", "daemon off;"] 