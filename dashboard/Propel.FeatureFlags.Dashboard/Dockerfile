# Development Dockerfile for hot reload
FROM node:20-alpine

# Set the working directory
WORKDIR /app

# Add curl for debugging if needed
RUN apk add --no-cache curl

# Copy package.json and package-lock.json from the correct path
COPY ui/Propel.FeatureFlags.Dashboard/package*.json ./

# Install dependencies (including dev dependencies for development)
RUN npm ci

# Copy the rest of the application code from the correct path
COPY ui/Propel.FeatureFlags.Dashboard/ .

# Set NODE_ENV to development
ENV NODE_ENV=development

# Expose port 80 for Vite dev server
EXPOSE 80

# Start the Vite development server with hot reload
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "80"]