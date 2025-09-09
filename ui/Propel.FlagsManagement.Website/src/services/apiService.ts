import { config } from '../config/environment';

// Base API configuration
const API_BASE_URL = config.API_BASE_URL;

// Types matching the API DTOs - Updated to match FeatureFlagResponse from C#
export interface FeatureFlagDto {
	key: string;
	name: string;
	description: string;
	evaluationModes: number[]; // Changed from string[] to number[] to match FlagEvaluationMode[]
	createdAt: string;
	updatedAt?: string; // Made optional to match C# DateTime?
	createdBy: string;
	updatedBy?: string; // Made optional to match C# string?
	expirationDate?: string;
	scheduledEnableDate?: string;
	scheduledDisableDate?: string;
	windowStartTime?: string;
	windowEndTime?: string;
	timeZone?: string;
	windowDays?: number[]; // Changed from string[] to number[] to match DayOfWeek[]
	userRolloutPercentage: number; // Renamed from userPercentageRollout to match C#
	allowedUsers: string[];
	blockedUsers: string[];
	tenantRolloutPercentage: number; // Renamed from tenantPercentageRollout to match C#
	allowedTenants: string[];
	blockedTenants: string[];
	targetingRules: TargetingRule[];
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

// Updated to match GetFeatureFlagRequest from C#
export interface GetFlagsParams {
	page?: number;
	pageSize?: number;
	modes?: number[]; // FlagEvaluationMode[] as numbers
	expiringInDays?: number; // Changed from string to number to match C# int?
	tagKeys?: string[];
	tags?: string[]; // Format: ["key:value", "key2:value2", "keyOnly"]
}

export interface TargetingRule {
	attribute: string;
	operator: string;
	values: string[];
	variation: string;
}

// Updated CreateFeatureFlagRequest to match C# endpoint exactly
export interface CreateFeatureFlagRequest {
	key: string;
	name: string;
	description?: string;
	expirationDate?: string;
	tags?: Record<string, string>;
	isPermanent?: boolean;
}

// Updated ModifyFlagRequest to match UpdateFlagRequest from C#
export interface ModifyFlagRequest {
	name?: string;
	description?: string;
	tags?: Record<string, string>;
	isPermanent?: boolean;
	expirationDate?: string;
}

export interface EnableFlagRequest {
	reason: string;
}

export interface DisableFlagRequest {
	reason: string;
}

// Updated ScheduleFlagRequest to match UpdateScheduleRequest from C#
export interface ScheduleFlagRequest {
	enableDate: string;
	disableDate?: string;
	removeSchedule: boolean;
}

// Updated SetTimeWindowRequest to match UpdateTimeWindowRequest from C#
export interface SetTimeWindowRequest {
	windowStartTime: string;
	windowEndTime: string;
	timeZone: string;
	windowDays: number[];
	removeTimeWindow: boolean;
}

// Updated UserAccessRequest to match ManageUserAccessRequest from C#
export interface UserAccessRequest {
	allowedUsers?: string[];
	blockedUsers?: string[];
	percentage?: number;
}

export interface TenantAccessRequest {
	allowedTenants?: string[];
	blockedTenants?: string[];
	percentage?: number;
}

export interface EvaluationResult {
	isEnabled: boolean;
	variation: string;
	reason: string;
	metadata: Record<string, any>;
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

// UTC to Local Time Conversion Utilities
class DateTimeConverter {
	/**
	 * Converts a UTC date string to local time
	 * @param utcDateString - UTC date string in ISO format
	 * @returns Local date string in ISO format, or undefined if input is null/undefined
	 */
	static utcToLocal(utcDateString?: string): string | undefined {
		if (!utcDateString) return undefined;
		
		try {
			// Parse the UTC date and convert to local time
			const utcDate = new Date(utcDateString);
			
			// Verify the date is valid
			if (isNaN(utcDate.getTime())) {
				console.warn(`Invalid date string received: ${utcDateString}`);
				return utcDateString; // Return original if invalid
			}
			
			// Return as local ISO string
			return utcDate.toISOString();
		} catch (error) {
			console.error(`Error converting UTC date to local: ${utcDateString}`, error);
			return utcDateString; // Return original on error
		}
	}

	/**
	 * Converts a local date string to UTC for API requests
	 * @param localDateString - Local date string in ISO format
	 * @returns UTC date string in ISO format, or undefined if input is null/undefined
	 */
	static localToUtc(localDateString?: string): string | undefined {
		if (!localDateString) return undefined;
		
		try {
			const localDate = new Date(localDateString);
			
			if (isNaN(localDate.getTime())) {
				console.warn(`Invalid date string provided: ${localDateString}`);
				return localDateString;
			}
			
			// Convert to UTC ISO string
			return localDate.toISOString();
		} catch (error) {
			console.error(`Error converting local date to UTC: ${localDateString}`, error);
			return localDateString;
		}
	}

	/**
	 * Converts all UTC date fields in a FeatureFlagDto to local time
	 * @param dto - The FeatureFlagDto with UTC dates
	 * @returns FeatureFlagDto with local dates
	 */
	static convertFeatureFlagDtoToLocal(dto: FeatureFlagDto): FeatureFlagDto {
		return {
			...dto,
			createdAt: this.utcToLocal(dto.createdAt) || dto.createdAt,
			updatedAt: this.utcToLocal(dto.updatedAt),
			expirationDate: this.utcToLocal(dto.expirationDate),
			scheduledEnableDate: this.utcToLocal(dto.scheduledEnableDate),
			scheduledDisableDate: this.utcToLocal(dto.scheduledDisableDate),
		};
	}

	/**
	 * Converts all local date fields in request objects to UTC for API calls
	 * @param request - Request object that may contain date fields
	 * @returns Request object with UTC dates
	 */
	static convertRequestToUtc<T extends Record<string, any>>(request: T): T {
		const converted = { ...request };
		
		// Convert common date fields that might be present in requests
		if ('expirationDate' in converted && typeof converted.expirationDate === 'string') {
			(converted as any).expirationDate = this.localToUtc(converted.expirationDate as string);
		}
		if ('enableDate' in converted && typeof converted.enableDate === 'string') {
			(converted as any).enableDate = this.localToUtc(converted.enableDate as string);
		}
		if ('disableDate' in converted && typeof converted.disableDate === 'string') {
			(converted as any).disableDate = this.localToUtc(converted.disableDate as string);
		}
		
		return converted;
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
	if (params.expiringInDays) searchParams.append('expiringInDays', params.expiringInDays.toString());

	// Handle modes filtering
	if (params.modes && params.modes.length > 0) {
		params.modes.forEach(mode => searchParams.append('modes', mode.toString()));
	}
	// Handle tag filtering
	if (params.tags && params.tags.length > 0) {
		params.tags.forEach(tag => searchParams.append('tags', tag));
	}
	if (params.tagKeys && params.tagKeys.length > 0) {
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

export const getDaysOfWeek = (): { value: number; label: string }[] => {
	return [
		{ value: 0, label: 'Sunday' },
		{ value: 1, label: 'Monday' },
		{ value: 2, label: 'Tuesday' },
		{ value: 3, label: 'Wednesday' },
		{ value: 4, label: 'Thursday' },
		{ value: 5, label: 'Friday' },
		{ value: 6, label: 'Saturday' }
	];
};

export const getEvaluationModes = (): { value: number; label: string }[] => {
	return [
		{ value: 0, label: 'Disabled' },
		{ value: 1, label: 'Enabled' },
		{ value: 2, label: 'Scheduled' },
		{ value: 3, label: 'Time Window' },
		{ value: 4, label: 'User Targeted' },
		{ value: 5, label: 'User Rollout Percentage' },
		{ value: 6, label: 'Tenant Rollout Percentage' },
		{ value: 7, label: 'Tenant Targeted' }
	]
};

// Export the DateTimeConverter for use in other modules
export { DateTimeConverter };

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

	// Feature flags CRUD - Updated to use /feature-flags endpoints with date conversion
	flags: {
		// Get paged flags (updated endpoint)
		getPaged: async (params: GetFlagsParams = {}) => {
			const searchParams = buildQueryParams(params);
			const query = searchParams.toString();
			const response = await apiRequest<PagedFeatureFlagsResponse>(`/feature-flags${query ? `?${query}` : ''}`);
			
			// Convert UTC dates to local time for all flags
			return {
				...response,
				items: response.items.map(flag => DateTimeConverter.convertFeatureFlagDtoToLocal(flag))
			};
		},

		// Get all flags (updated endpoint)
		getAll: async () => {
			const flags = await apiRequest<FeatureFlagDto[]>('/feature-flags/all');
			return flags.map(flag => DateTimeConverter.convertFeatureFlagDtoToLocal(flag));
		},

		// Get specific flag (updated endpoint)
		get: async (key: string) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}`);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Create flag
		create: async (request: CreateFeatureFlagRequest) => {
			const utcRequest = DateTimeConverter.convertRequestToUtc(request);
			const flag = await apiRequest<FeatureFlagDto>('/feature-flags', {
				method: 'POST',
				body: JSON.stringify(utcRequest),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Update flag - Updated endpoint
		update: async (key: string, request: ModifyFlagRequest) => {
			const utcRequest = DateTimeConverter.convertRequestToUtc(request);
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}`, {
				method: 'PUT',
				body: JSON.stringify(utcRequest),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Delete flag
		delete: (key: string) =>
			apiRequest<void>(`/feature-flags/${key}`, {
				method: 'DELETE',
			}),

		// Search flags with new tag filtering
		search: async (params: { tag?: string; expiringInDays?: number, modes?: number[]; tagKey?: string; tagValue?: string } = {}) => {
			const queryParams: GetFlagsParams = {
				page: 1,
				pageSize: 100, // Large page size for legacy compatibility
				modes: params.modes,
				expiringInDays: params.expiringInDays
			};

			// Handle different tag filtering approaches
			if (params.tag) {
				queryParams.tags = [params.tag];
			} else if (params.tagKey) {
				queryParams.tagKeys = [params.tagKey];
			}

			const result = await apiService.flags.getPaged(queryParams);
			return result.items;
		},

		// Search by tags (multiple tags)
		searchByTags: async (tags: Record<string, string>, options: { page?: number; pageSize?: number } = {}) => {
			const tagStrings = Object.entries(tags).map(([key, value]) =>
				value ? `${key}:${value}` : key
			);

			return apiService.flags.getPaged({
				page: options.page || 1,
				pageSize: options.pageSize || 10,
				tags: tagStrings
			});
		}
	},

	// Flag operations - Updated endpoints with date conversion
	operations: {
		// Enable flag
		enable: async (key: string, request: EnableFlagRequest) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/enable`, {
				method: 'POST',
				body: JSON.stringify(request),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Disable flag
		disable: async (key: string, request: DisableFlagRequest) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/disable`, {
				method: 'POST',
				body: JSON.stringify(request),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Schedule flag - Updated endpoint
		schedule: async (key: string, request: ScheduleFlagRequest) => {
			const utcRequest = DateTimeConverter.convertRequestToUtc(request);
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/schedule`, {
				method: 'POST',
				body: JSON.stringify(utcRequest),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Set time window - Updated endpoint
		setTimeWindow: async (key: string, request: SetTimeWindowRequest) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/time-window`, {
					method: 'POST',
					body: JSON.stringify(request),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Updated consolidated user access management endpoint
		updateUserAccess: async (key: string, request: UserAccessRequest) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/users`, {
				method: 'POST',
				body: JSON.stringify(request),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		// Updated consolidated tenant access management endpoint
		updateTenantAccess: async (key: string, request: TenantAccessRequest) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/tenants`, {
				method: 'POST',
				body: JSON.stringify(request),
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},
	},

	// Flag evaluation
	evaluation: {
		// Evaluate single flag
		evaluate: async (key: string, userId?: string, attributes?: Record<string, any>) => {
			const params = new URLSearchParams();
			if (userId) params.append('userId', userId);
			if (attributes) params.append('attributes', JSON.stringify(attributes));

			const query = params.toString();
			const response = await apiRequest<Record<string, EvaluationResult>>(`/feature-flags/evaluate/${key}${query ? `?${query}` : ''}`);
			
			// Extract the single result from the dictionary
			return response[key];
		},

		// Evaluate multiple flags
		evaluateMultiple: (request: {
			flagKeys: string[];
			userId?: string;
			attributes?: Record<string, any>;
		}) =>
			apiRequest<Record<string, EvaluationResult>>('/feature-flags/evaluate', {
				method: 'POST',
				body: JSON.stringify(request),
			}),
	},
};

export default apiService;