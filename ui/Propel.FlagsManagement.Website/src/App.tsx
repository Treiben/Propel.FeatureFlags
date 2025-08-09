import { useEffect, useState } from 'react';
import FeatureFlagManager from './FeatureFlagManager';
import { apiService } from './services/apiService';
import { config } from './config/environment';
import './index.css';

function App() {
  const [tokenReady, setTokenReady] = useState(false);

  useEffect(() => {
    // Set the JWT token for API requests
    const token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IldyaXRlVXNlciIsInN1YiI6IldyaXRlVXNlciIsImp0aSI6IjkyZjZhNzNmIiwic2NvcGUiOlsiZmVhdHVyZXRvZ2dsZXNtYW5hZ2VtZW50YXBpIiwicmVhZCIsIndyaXRlIl0sImF1ZCI6WyJodHRwOi8vbG9jYWxob3N0OjUwMzgiLCJodHRwczovL2xvY2FsaG9zdDo3MTEzIl0sIm5iZiI6MTc1MzUwMDM3NCwiZXhwIjoxNzYxNDQ5MTc0LCJpYXQiOjE3NTM1MDAzNzQsImlzcyI6ImRvdG5ldC11c2VyLWp3dHMifQ.7_mWTTJ_Jbq1I6Kg65ulyEjTP5mb6VEfhJH1w3iOF0o";

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
