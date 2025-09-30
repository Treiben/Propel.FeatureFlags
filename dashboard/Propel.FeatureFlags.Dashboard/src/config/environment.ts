// Environment configuration for Vite (uses import.meta.env instead of process.env)
export const config = {
    // Use relative path when using Vite proxy, full URL otherwise
    API_BASE_URL: import.meta.env.VITE_API_URL || '/api',
    AUTH_ENABLED: import.meta.env.VITE_AUTH_ENABLED === 'true',
    JWT_STORAGE_KEY: 'feature_flags_jwt',
};

export default config;