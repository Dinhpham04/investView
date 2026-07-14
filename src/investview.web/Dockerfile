FROM node:24-alpine AS build
WORKDIR /app

COPY src/investview.web/package*.json ./
RUN npm ci

COPY src/investview.web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY src/investview.web/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
