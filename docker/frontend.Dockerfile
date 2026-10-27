# Angular frontend: build with Node, serve the static files with nginx.
# Build context: ./linked. The app calls the API gateway at http://localhost:5088/api (published by docker-compose).

FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
COPY --from=build /app/dist/linked/browser /usr/share/nginx/html
# "dockerfiles" is a second build context (./docker), declared in docker-compose.yml.
COPY --from=dockerfiles nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
