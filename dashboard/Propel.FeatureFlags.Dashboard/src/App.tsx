import { useEffect, useState } from 'react';
import FeatureFlagManager from './FeatureFlagManager';
import { apiService } from './services/apiService';
import { config } from './config/environment';
import './index.css';

function App() {
	const [tokenReady, setTokenReady] = useState(false);

	useEffect(() => {
		// Set the JWT token for API requests
		const token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IkZ1bGxBY2Nlc3NVc2VyIiwic3ViIjoiRnVsbEFjY2Vzc1VzZXIiLCJqdGkiOiJjZDZkMzUxNiIsInNjb3BlIjpbInByb3BlbC1kYXNoYm9hcmQtYXBpIiwicmVhZCIsIndyaXRlIl0sIm5hbWUiOiJUZXN0IFVzZXIiLCJlbWFpbCI6InRlc3RAZXhhbXBsZS5jb20iLCJhdWQiOlsiaHR0cDovL2xvY2FsaG9zdDo1MDM4IiwiaHR0cHM6Ly9sb2NhbGhvc3Q6NzExMyJdLCJuYmYiOjE3NTkxNzcxMzEsImV4cCI6MTc2NzAzOTUzMSwiaWF0IjoxNzU5MTc3MTMyLCJpc3MiOiJkb3RuZXQtdXNlci1qd3RzIn0.pfxS09l3Rq_RZCzLuhVWuYlrmVxEWOFlj5n89ywspHA"

		console.log('App component mounting - setting token...');
		console.log('Using storage key:', config.JWT_STORAGE_KEY);

		// Clear any existing token first
		localStorage.removeItem(config.JWT_STORAGE_KEY);

		// Set the token using both methods to ensure consistency
		apiService.auth.setToken(token);
		localStorage.setItem(config.JWT_STORAGE_KEY, token);

		// Verify token was set with multiple checks
		const storedTokenViaService = apiService.auth.getToken();
		const storedTokenDirect = localStorage.getItem(config.JWT_STORAGE_KEY);

		console.log('Token verification via service:', storedTokenViaService ? 'SUCCESS' : 'FAILED');
		console.log('Token verification direct:', storedTokenDirect ? 'SUCCESS' : 'FAILED');
		console.log('Tokens match:', storedTokenViaService === storedTokenDirect ? 'YES' : 'NO');

		// Set ready state only after token is confirmed
		if (storedTokenViaService && storedTokenDirect && storedTokenViaService === storedTokenDirect) {
			console.log('Token setup complete - rendering FeatureFlagManager');
			setTokenReady(true);
		} else {
			console.error('Token setup failed - tokens do not match or are missing');
		}
	}, []);

	// Don't render FeatureFlagManager until token is ready
	if (!tokenReady) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<div className="text-center">
					<div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto mb-4"></div>
					<p className="text-gray-600">Setting up authentication...</p>
				</div>
			</div>
		);
	}

	return <FeatureFlagManager />;
}

export default App;
