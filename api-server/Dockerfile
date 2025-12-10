FROM node:20-alpine

# Set production defaults; PORT can be overridden at runtime.
ENV NODE_ENV=production \
    PORT=3000 \
    RADIO_BROWSER_AGENT="MauiPlayer/1.0 (api-server)"

WORKDIR /app

# Install only production dependencies.
COPY package*.json ./
RUN npm ci --omit=dev

# Copy the application source.
COPY server.js ./server.js

# Drop root privileges inside the container.
RUN chown -R node:node /app
USER node

EXPOSE 3000

# Lightweight runtime healthcheck (requires curl).
USER root
RUN apk add --no-cache curl
USER node
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD curl -f http://127.0.0.1:${PORT:-3000}/api/status || exit 1

CMD ["node", "server.js"]
