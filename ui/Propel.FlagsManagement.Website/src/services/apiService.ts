import { config } from '../config/environment';

// Base API configuration
const API_BASE_URL = config.API_BASE_URL;

// Types matching the API DTOs
export interface FeatureFlagDto {
	key: string;
	name: string;
	description: string;
	status: string;
	createdAt: string;
	updatedAt: string;
	createdBy: string;
	updatedBy: string;
	expirationDate?: string;
	scheduledEnableDate?: string;
	scheduledDisableDate?: string;
	windowStartTime?: string;
	windowEndTime?: string;
	timeZone?: string;
	windowDays?: string[];
	percentageEnabled: number;
	targetingRules: TargetingRule[];
	enabledUsers: string[];
	disabledUsers: string[];
	variations: Record<string, any>;
	defaultVariation: string;
	tags: Record<string, string>;
	isPermanent: boolean;
}

export interface PagedFeatureFlagsResponse {
	items: FeatureFlagDto[];
	totalCount: number;
	page: number;
	pageSize: number;
	totalPages: number;
	hasNextPage: boolean;
	hasPreviousPage: boolean;
}

export interface GetFlagsParams {
	page?: number;
	pageSize?: number;
	status?: string;
	tagKeys?: string[];
	tags?: string[]; // Format: ["key:value", "key2:value2", "keyOnly"]
}

export interface TargetingRule {
	attribute: string;
	operator: string;
	values: string[];
	variation: string;
}

export interface CreateFeatureFlagRequest {
	key: string;
	name: string;
	description?: string;
	status?: string;
	expirationDate?: string;
	scheduledEnableDate?: string;
	scheduledDisableDate?: string;
	windowStartTime?: string;
	windowEndTime?: string;
	timeZone?: string;
	windowDays?: string[];
	percentageEnabled?: number;
	targetingRules?: TargetingRule[];
	enabledUsers?: string[];
	disabledUsers?: string[];
	variations?: Record<string, any>;
	defaultVariation?: string;
	tags?: Record<string, string>;
	isPermanent?: boolean;
}

export interface ModifyFlagRequest {
	name?: string;
	description?: string;
	expirationDate?: string;
	targetingRules?: TargetingRule[];
	enabledUsers?: string[];
	disabledUsers?: string[];
	variations?: Record<string, any>;
	defaultVariation?: string;
	tags?: Record<string, string>;
	isPermanent?: boolean;
}

export interface EnableFlagRequest {
	reason: string;
}

export interface DisableFlagRequest {
	reason: string;
}

export interface ScheduleFlagRequest {
	enableDate: string;
	disableDate?: string;
	removeSchedule: boolean;
}

export interface SetTimeWindowRequest {
	windowStartTime: string;
	windowEndTime: string;
	timeZone: string;
	windowDays: number[];
	removeTimeWindow: boolean;
}

export interface SetPercentageRequest {
	percentage: number;
}

export interface UserAccessRequest {
	userIds: string[];
}

// API Error handling
export class ApiError extends Error {
	constructor(
		message: string,
		public status: number,
		public response?: any
	) {
		super(message);
		this.name = 'ApiError';
	}
}

// Token management
class TokenManager {
	private static readonly TOKEN_KEY = config.JWT_STORAGE_KEY;

	static getToken(): string | null {
		try {
			const token = localStorage.getItem(this.TOKEN_KEY);
			console.log(`Getting token from localStorage (key: ${this.TOKEN_KEY}):`, token ? 'Present' : 'Missing');
			if (token) {
				console.log('Token value:', `${token.substring(0, 20)}...`);
			}
			return token;
		} catch (error) {
			console.error('Error retrieving token:', error);
			return null;
		}
	}

	static setToken(token: string): void {
		try {
			console.log(`Setting token in localStorage (key: ${this.TOKEN_KEY}):`, token ? `${token.substring(0, 20)}...` : 'null');
			localStorage.setItem(this.TOKEN_KEY, token);

			// Verify it was set
			const stored = localStorage.getItem(this.TOKEN_KEY);
			console.log('Verification - token stored:', stored ? 'Yes' : 'No');

			if (stored !== token) {
				console.error('ERROR: Stored token does not match input token!');
			}
		} catch (error) {
			console.error('Error setting token:', error);
		}
	}

	static removeToken(): void {
		try {
			localStorage.removeItem(this.TOKEN_KEY);
			console.log(`Token removed from localStorage (key: ${this.TOKEN_KEY})`);
		} catch (error) {
			console.error('Error removing token:', error);
		}
	}
}

// HTTP client with authentication
async function apiRequest<T>(
	endpoint: string,
	options: RequestInit = {}
): Promise<T> {
	const token = TokenManager.getToken();
	const url = `${API_BASE_URL}${endpoint}`;

	console.log(`API Request: ${options.method || 'GET'} ${url}`);
	console.log(`Token available: ${token ? 'Yes' : 'No'}`);

	const headers: HeadersInit = {
		'Content-Type': 'application/json',
		'Authorization': token ? `Bearer ${token}` : '',
		...(options.headers || {}),
	};

	const requestConfig: RequestInit = {
		...options,
		headers,
	};

	if (token) {
		console.log(`Authorization header set: Bearer ${token.substring(0, 20)}...`);
	} else {
		console.log('No token available - Authorization header NOT set');
		console.log(`Checked localStorage key: ${config.JWT_STORAGE_KEY}`);
		console.log(`Available localStorage keys:`, Object.keys(localStorage));
	}

	console.log('Final request headers:', headers);

	try {
		const response = await fetch(url, requestConfig);

		console.log(`API Response: ${response.status} ${response.statusText}`);

		if (!response.ok) {
			let errorMessage = `HTTP ${response.status}: ${response.statusText}`;

			try {
				const errorData = await response.json();
				if (errorData.message) {
					errorMessage = errorData.message;
				} else if (errorData.errors) {
					errorMessage = Object.values(errorData.errors).flat().join(', ');
				}
				throw new ApiError(errorMessage, response.status, errorData);
			} catch (jsonError) {
				// If JSON parsing fails, throw with original message
				throw new ApiError(errorMessage, response.status);
			}
		}

		// Handle 204 No Content responses
		if (response.status === 204) {
			return null as T;
		}

		const data = await response.json();
		console.log('API Response Data:', data);
		return data;
	} catch (error) {
		console.error('API Request Error:', error);
		if (error instanceof ApiError) {
			throw error;
		}
		throw new ApiError(
			error instanceof Error ? error.message : 'Network error occurred',
			0
		);
	}
}

// Helper function to build query parameters
function buildQueryParams(params: GetFlagsParams): URLSearchParams {
	const searchParams = new URLSearchParams();

	if (params.page) searchParams.append('page', params.page.toString());
	if (params.pageSize) searchParams.append('pageSize', params.pageSize.toString());
	if (params.status) searchParams.append('status', params.status);

	// Handle tag filtering
	if (params.tags && params.tags.length > 0) {
		params.tags.forEach(tag => searchParams.append('tags', tag));
	} else if (params.tagKeys && params.tagKeys.length > 0) {
		params.tagKeys.forEach(key => searchParams.append('tagKeys', key));
	}

	return searchParams;
}

// Helper functions for time zone handling
export const getTimeZones = (): string[] => {
	return [
		'UTC',
		'America/New_York',
		'America/Chicago',
		'America/Denver',
		'America/Los_Angeles',
		'Europe/London',
		'Europe/Paris',
		'Europe/Berlin',
		'Asia/Tokyo',
		'Asia/Shanghai',
		'Asia/Kolkata',
		'Australia/Sydney'
	];
};

export const getDaysOfWeek = (): { value: string; label: string }[] => {
	return [
		{ value: 'Sunday', label: 'Sunday' },
		{ value: 'Monday', label: 'Monday' },
		{ value: 'Tuesday', label: 'Tuesday' },
		{ value: 'Wednesday', label: 'Wednesday' },
		{ value: 'Thursday', label: 'Thursday' },
		{ value: 'Friday', label: 'Friday' },
		{ value: 'Saturday', label: 'Saturday' }
	];
};

// API Service
export const apiService = {
	// Authentication
	auth: {
		setToken: TokenManager.setToken,
		removeToken: TokenManager.removeToken,
		getToken: TokenManager.getToken,
	},

	// Health checks
	health: {
		live: () => apiRequest<{ status: string }>('/health/live'),
		ready: () => apiRequest<{ status: string }>('/health/ready'),
	},

	// Feature flags CRUD
	flags: {
		// Get paged flags (new default)
		getPaged: (params: GetFlagsParams = {}) => {
			const searchParams = buildQueryParams(params);
			const query = searchParams.toString();
			return apiRequest<PagedFeatureFlagsResponse>(`/feature-flags${query ? `?${query}` : ''}`);
		},

		// Get all flags (for backward compatibility)
		getAll: () => apiRequest<FeatureFlagDto[]>('/feature-flags/all'),

		// Get specific flag
		get: (key: string) => apiRequest<FeatureFlagDto>(`/feature-flags/${key}`),

		// Create flag
		create: (request: CreateFeatureFlagRequest) =>
			apiRequest<FeatureFlagDto>('/feature-flags', {
				method: 'POST',
				body: JSON.stringify(request),
			}),

		// Update flag
		update: (key: string, request: ModifyFlagRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}`, {
				method: 'PUT',
				body: JSON.stringify(request),
			}),

		// Delete flag
		delete: (key: string) =>
			apiRequest<void>(`/feature-flags/${key}`, {
				method: 'DELETE',
			}),

		// Search flags with new tag filtering
		search: (params: { tag?: string; status?: string; tagKey?: string; tagValue?: string } = {}) => {
			const queryParams: GetFlagsParams = {
				page: 1,
				pageSize: 100, // Large page size for legacy compatibility
				status: params.status
			};

			// Handle different tag filtering approaches
			if (params.tag) {
				queryParams.tags = [params.tag];
			} else if (params.tagKey) {
				queryParams.tagKeys = [params.tagKey];
			}

			return apiService.flags.getPaged(queryParams).then(result => result.items);
		},

		// Search by tags (multiple tags)
		searchByTags: (tags: Record<string, string>, options: { page?: number; pageSize?: number } = {}) => {
			const tagStrings = Object.entries(tags).map(([key, value]) =>
				value ? `${key}:${value}` : key
			);

			return apiService.flags.getPaged({
				page: options.page || 1,
				pageSize: options.pageSize || 10,
				tags: tagStrings
			});
		},

		// Get expiring flags
		getExpiring: (days: number = 7) =>
			apiRequest<FeatureFlagDto[]>(`/feature-flags/expiring?days=${days}`),
	},

	// Flag operations
	operations: {
		// Enable flag
		enable: (key: string, request: EnableFlagRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/enable`, {
				method: 'POST',
				body: JSON.stringify(request),
			}),

		// Disable flag
		disable: (key: string, request: DisableFlagRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/disable`, {
				method: 'POST',
				body: JSON.stringify(request),
			}),

		// Schedule flag
		schedule: (key: string, request: ScheduleFlagRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/schedule`, {
				method: 'POST',
				body: JSON.stringify(request),
			}),

		// Set time window
		setTimeWindow: (key: string, request: SetTimeWindowRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/time-window`, {
					method: 'POST',
					body: JSON.stringify(request),
			}),

		// Set percentage
		setPercentage: (key: string, request: SetPercentageRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/percentage`, {
				method: 'POST',
				body: JSON.stringify(request),
			}),

		// User access management
		enableUsers: (key: string, request: UserAccessRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/users/enable`, {
				method: 'POST',
				body: JSON.stringify(request),
			}),

		disableUsers: (key: string, request: UserAccessRequest) =>
			apiRequest<FeatureFlagDto>(`/feature-flags/${key}/users/disable`, {
				method: 'POST',
				body: JSON.stringify(request),
			}),
	},

	// Flag evaluation
	evaluation: {
		// Evaluate single flag
		evaluate: (key: string, userId?: string, attributes?: Record<string, any>) => {
			const params = new URLSearchParams();
			if (userId) params.append('userId', userId);
			if (attributes) params.append('attributes', JSON.stringify(attributes));

			const query = params.toString();
			return apiRequest<any>(`/feature-flags/evaluate/${key}${query ? `?${query}` : ''}`);
		},

		// Evaluate multiple flags
		evaluateMultiple: (request: {
			flagKeys: string[];
			userId?: string;
			attributes?: Record<string, any>;
		}) =>
			apiRequest<Record<string, any>>('/feature-flags/evaluate', {
				method: 'POST',
				body: JSON.stringify(request),
			}),
	},
};

export default apiService;