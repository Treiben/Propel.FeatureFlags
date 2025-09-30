import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
	plugins: [react()],
	server: {
		host: '0.0.0.0',
		port: 5173, // or 3000, 8080, any port > 1024
		proxy: {
			'/api': {
				target: process.env.VITE_API_URL || 'http://propel.featureflags.dashboard.api:8080',
				changeOrigin: true,
				secure: false,
			}
		}
	},
	preview: {
		host: '0.0.0.0',
		port: 8080 // also change preview port
	},
	//to run from docker container
	//server: {
	//  host: '0.0.0.0', // Allow external connections (needed for Docker)
	//  port: 80,
	//  proxy: {
	//    '/api': {
	//          target: process.env.VITE_API_URL || 'http://propel.featureflags.dashboard.api:8080',
	//      changeOrigin: true,
	//      secure: false,
	//    }
	//  }
	//},
	//preview: {
	//  host: '0.0.0.0',
	//  port: 80
	//},
	build: {
		outDir: 'dist',
		emptyOutDir: true,
		sourcemap: false, // Disable source maps for production
		rollupOptions: {
			output: {
				manualChunks: {
					vendor: ['react', 'react-dom'],
					utils: ['axios', 'lucide-react']
				}
			}
		}
	}
})
