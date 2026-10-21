# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package descriptors
COPY package*.json ./
RUN npm install --no-audit

# Copy source code and build
COPY . .
RUN npm run build

# Nginx production stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
