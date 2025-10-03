import { config } from '../config/environment';

const API_BASE_URL = config.API_BASE_URL;

// Enums matching C# backend
export enum EvaluationMode {
	Off = 0,
	On = 1,
	Scheduled = 2,
	TimeWindow = 3,
	UserTargeted = 4,
	UserRolloutPercentage = 5,
	TenantRolloutPercentage = 6,
	TenantTargeted = 7,
	TargetingRules = 8
}

export enum Scope {
	Global = 0,
	Application = 2
}

export enum TargetingOperator {
	Equals = 0,
	NotEquals = 1,
	Contains = 2,
	NotContains = 3,
	In = 4,
	NotIn = 5,
	GreaterThan = 6,
	LessThan = 7
}

// Type definitions matching C# DTOs exactly
export interface AuditInfo {
	timestampUtc?: string;
	actor?: string;
}

export interface FlagSchedule {
	enableOnUtc?: string;
	disableOnUtc?: string;
}

export interface TimeWindow {
	startOn?: string; // TimeOnly as string "HH:mm:ss"
	stopOn?: string;
	timeZone: string;
	daysActive?: number[];
}

export interface AccessControl {
	allowed?: string[];
	blocked?: string[];
	rolloutPercentage?: number;
}

export interface Variations {
	values: Record<string, any>;
	defaultVariation: string;
}

export interface FeatureFlagDto {
	key: string;
	name: string;
	description: string;
	modes: EvaluationMode[];
	created: AuditInfo;
	updated?: AuditInfo;
	schedule?: FlagSchedule;
	timeWindow?: TimeWindow;
	userAccess?: AccessControl;
	tenantAccess?: AccessControl;
	targetingRules: string; // JSON string
	variations: Variations;
	tags: Record<string, string>;
	expirationDate?: string;
	isPermanent: boolean;
	applicationName?: string;
	applicationVersion?: string;
	scope: Scope;
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
	modes?: EvaluationMode[];
	expiringInDays?: number;
	tagKeys?: string[];
	tags?: string[]; // Format: ["key:value", "key2:value2"]
	applicationName?: string;
	scope?: Scope;
}

export interface TargetingRule {
	attribute: string;
	operator: TargetingOperator;
	values: string[];
	variation: string;
}

// Request objects matching C# exactly
export interface CreateFeatureFlagRequest {
	key: string;
	name: string;
	description?: string;
	tags?: Record<string, string>;
}

export interface UpdateFlagRequest {
	name?: string;
	description?: string;
	tags?: Record<string, string>;
	expirationDate?: string;
	notes?: string;
}

export interface ToggleFlagRequest {
	evaluationMode: EvaluationMode;
	notes?: string;
}

export interface UpdateScheduleRequest {
	enableOn?: string;
	disableOn?: string;
	notes?: string;
}

export interface UpdateTimeWindowRequest {
	startOn: string; // TimeOnly as string "HH:mm:ss"
	endOn: string;
	timeZone: string;
	daysActive: number[];
	removeTimeWindow: boolean;
	notes?: string;
}

export interface ManageUserAccessRequest {
	allowed?: string[];
	blocked?: string[];
	rolloutPercentage?: number;
	notes?: string;
}

export interface ManageTenantAccessRequest {
	allowed?: string[];
	blocked?: string[];
	rolloutPercentage?: number;
	notes?: string;
}

export interface UpdateTargetingRulesRequest {
	targetingRules?: TargetingRule[];
	removeTargetingRules: boolean;
	notes?: string;
}

export interface UpdateVariationsRequest {
	variations?: Array<{ key: string; value: string }> | undefined;
	defaultVariation: string;
	removeVariations: boolean;
	notes?: string;
}

export interface EvaluationResult {
	isEnabled: boolean;
	variation: string;
	reason: string;
	metadata: Record<string, any>;
}

export interface ScopeHeaders {
	scope: string; // "Global" or "Application"
	applicationName?: string;
	applicationVersion?: string;
}

// Helper functions
export const getTargetingOperatorLabel = (operator: number | string): string => {
	const operatorMap: Record<number, string> = {
		0: 'Equals',
		1: 'NotEquals',
		2: 'Contains',
		3: 'NotContains',
		4: 'In',
		5: 'NotIn',
		6: 'GreaterThan',
		7: 'LessThan'
	};
	return typeof operator === 'number' ? operatorMap[operator] || 'Equals' : operator || 'Equals';
};

export const getTargetingOperators = (): { value: TargetingOperator; label: string; description: string }[] => [
	{ value: TargetingOperator.Equals, label: 'Equals', description: 'Exact match' },
	{ value: TargetingOperator.NotEquals, label: 'Not Equals', description: 'Does not match exactly' },
	{ value: TargetingOperator.Contains, label: 'Contains', description: 'Contains the value' },
	{ value: TargetingOperator.NotContains, label: 'Not Contains', description: 'Does not contain the value' },
	{ value: TargetingOperator.In, label: 'In', description: 'Value is in the list' },
	{ value: TargetingOperator.NotIn, label: 'Not In', description: 'Value is not in the list' },
	{ value: TargetingOperator.GreaterThan, label: 'Greater Than', description: 'Numeric value is greater' },
	{ value: TargetingOperator.LessThan, label: 'Less Than', description: 'Numeric value is less' }
];

export const parseTargetingRules = (targetingRulesJson: string): TargetingRule[] => {
	try {
		if (!targetingRulesJson || targetingRulesJson.trim() === '') return [];
		const parsed = JSON.parse(targetingRulesJson);
		return Array.isArray(parsed) ? parsed : (parsed && typeof parsed === 'object' ? [parsed] : []);
	} catch (error) {
		console.error('Failed to parse targeting rules JSON:', error);
		return [];
	}
};

export const stringifyTargetingRules = (targetingRules: TargetingRule[]): string => {
	try {
		return JSON.stringify(targetingRules || []);
	} catch (error) {
		console.error('Failed to stringify targeting rules:', error);
		return '[]';
	}
};

export const hasValidTargetingRulesJson = (targetingRulesJson: string): boolean => {
	try {
		if (!targetingRulesJson || targetingRulesJson.trim() === '') return false;
		const parsed = JSON.parse(targetingRulesJson);
		return Array.isArray(parsed) ? parsed.length > 0 : !!(parsed && typeof parsed === 'object');
	} catch {
		return false;
	}
};

export const getTimeZones = (): string[] => [
	'UTC', 'America/New_York', 'America/Chicago', 'America/Denver', 'America/Los_Angeles',
	'Europe/London', 'Europe/Paris', 'Europe/Berlin', 'Asia/Tokyo', 'Asia/Shanghai',
	'Asia/Kolkata', 'Australia/Sydney'
];

export const getDaysOfWeek = (): { value: number; label: string }[] => [
	{ value: 0, label: 'Sunday' },
	{ value: 1, label: 'Monday' },
	{ value: 2, label: 'Tuesday' },
	{ value: 3, label: 'Wednesday' },
	{ value: 4, label: 'Thursday' },
	{ value: 5, label: 'Friday' },
	{ value: 6, label: 'Saturday' }
];

export const getEvaluationModes = (): { value: EvaluationMode; label: string }[] => [
	{ value: EvaluationMode.Off, label: 'Disabled' },
	{ value: EvaluationMode.On, label: 'Enabled' },
	{ value: EvaluationMode.Scheduled, label: 'Scheduled' },
	{ value: EvaluationMode.TimeWindow, label: 'Time Window' },
	{ value: EvaluationMode.UserTargeted, label: 'User Targeted' },
	{ value: EvaluationMode.UserRolloutPercentage, label: 'User Rollout Percentage' },
	{ value: EvaluationMode.TenantRolloutPercentage, label: 'Tenant Rollout Percentage' },
	{ value: EvaluationMode.TenantTargeted, label: 'Tenant Targeted' },
	{ value: EvaluationMode.TargetingRules, label: 'Targeting Rules' }
];

// Date converter for nested structure
class DateTimeConverter {
	static utcToLocal(utcDateString?: string): string | undefined {
		if (!utcDateString) return undefined;
		try {
			const utcDate = new Date(utcDateString);
			if (isNaN(utcDate.getTime())) return utcDateString;
			return utcDate.toISOString();
		} catch {
			return utcDateString;
		}
	}

	static localToUtc(localDateString?: string): string | undefined {
		if (!localDateString) return undefined;
		try {
			const localDate = new Date(localDateString);
			if (isNaN(localDate.getTime())) return localDateString;
			return localDate.toISOString();
		} catch {
			return localDateString;
		}
	}

	static convertFeatureFlagDtoToLocal(dto: FeatureFlagDto): FeatureFlagDto {
		return {
			...dto,
			created: {
				...dto.created,
				timestampUtc: this.utcToLocal(dto.created.timestampUtc)
			},
			updated: dto.updated ? {
				...dto.updated,
				timestampUtc: this.utcToLocal(dto.updated.timestampUtc)
			} : undefined,
			schedule: dto.schedule ? {
				enableOnUtc: this.utcToLocal(dto.schedule.enableOnUtc),
				disableOnUtc: this.utcToLocal(dto.schedule.disableOnUtc)
			} : undefined,
			expirationDate: this.utcToLocal(dto.expirationDate)
		};
	}

	static convertRequestToUtc<T extends Record<string, any>>(request: T): T {
		const converted = { ...request };
		if ('expirationDate' in converted && typeof converted.expirationDate === 'string') {
			(converted as any).expirationDate = this.localToUtc(converted.expirationDate as string);
		}
		if ('enableOn' in converted && typeof converted.enableOn === 'string') {
			(converted as any).enableOn = this.localToUtc(converted.enableOn as string);
		}
		if ('disableOn' in converted && typeof converted.disableOn === 'string') {
			(converted as any).disableOn = this.localToUtc(converted.disableOn as string);
		}
		return converted;
	}
}

// Token management
class TokenManager {
	private static readonly TOKEN_KEY = config.JWT_STORAGE_KEY;

	static getToken(): string | null {
		try {
			return localStorage.getItem(this.TOKEN_KEY);
		} catch (error) {
			console.error('Error retrieving token:', error);
			return null;
		}
	}

	static setToken(token: string): void {
		try {
			localStorage.setItem(this.TOKEN_KEY, token);
		} catch (error) {
			console.error('Error setting token:', error);
		}
	}

	static removeToken(): void {
		try {
			localStorage.removeItem(this.TOKEN_KEY);
		} catch (error) {
			console.error('Error removing token:', error);
		}
	}
}

// API Error handling
export class ApiError extends Error {
	constructor(
		message: string,
		public status: number,
		public title?: string,
		public detail?: string,
		public response?: any
	) {
		super(message);
		this.name = 'ApiError';
	}
}

// HTTP client
async function apiRequest<T>(
	endpoint: string,
	options: RequestInit = {},
	scopeHeaders?: ScopeHeaders
): Promise<T> {
	const token = TokenManager.getToken();
	const url = `${API_BASE_URL}${endpoint}`;

	const headers: HeadersInit = {
		'Content-Type': 'application/json',
		...(token && { 'Authorization': `Bearer ${token}` }),
		...(scopeHeaders && {
			'X-Scope': scopeHeaders.scope,
			...(scopeHeaders.applicationName && { 'X-Application-Name': scopeHeaders.applicationName }),
			...(scopeHeaders.applicationVersion && { 'X-Application-Version': scopeHeaders.applicationVersion })
		}),
		...(options.headers || {})
	};

	const requestConfig: RequestInit = {
		...options,
		headers
	};

	try {
		const response = await fetch(url, requestConfig);

		if (!response.ok) {
			let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
			let errorTitle: string | undefined;
			let errorDetail: string | undefined;
			let errorData: any;

			try {
				errorData = await response.json();
				
				// RFC 7807 ProblemDetails format (what HttpProblemFactory returns)
				if (errorData.detail) {
					errorDetail = errorData.detail;
					errorMessage = errorData.detail;
				}
				
				if (errorData.title) {
					errorTitle = errorData.title;
				}
				
				// Fallback to other formats
				if (!errorDetail && errorData.message) {
					errorMessage = errorData.message;
					errorDetail = errorData.message;
				}
				
				// Validation errors format
				if (errorData.errors && typeof errorData.errors === 'object') {
					const validationMessages = Object.entries(errorData.errors)
						.map(([field, messages]) => {
							const messageArray = Array.isArray(messages) ? messages : [messages];
							return `${field}: ${messageArray.join(', ')}`;
						})
						.join('; ');
					errorMessage = validationMessages;
					errorDetail = validationMessages;
				}
				
				throw new ApiError(errorMessage, response.status, errorTitle, errorDetail, errorData);
			} catch (jsonError) {
				// If JSON parsing fails or ApiError was thrown, re-throw or create new ApiError
				if (jsonError instanceof ApiError) {
					throw jsonError;
				}
				throw new ApiError(errorMessage, response.status);
			}
		}

		if (response.status === 204) return null as T;

		return await response.json();
	} catch (error) {
		if (error instanceof ApiError) throw error;
		throw new ApiError(
			error instanceof Error ? error.message : 'Network error occurred',
			0
		);
	}
}

function buildQueryParams(params: GetFlagsParams): URLSearchParams {
	const searchParams = new URLSearchParams();
	if (params.page) searchParams.append('page', params.page.toString());
	if (params.pageSize) searchParams.append('pageSize', params.pageSize.toString());
	if (params.expiringInDays) searchParams.append('expiringInDays', params.expiringInDays.toString());
	if (params.modes?.length) {
		params.modes.forEach(mode => searchParams.append('modes', mode.toString()));
	}
	if (params.tags?.length) {
		params.tags.forEach(tag => searchParams.append('tags', tag));
	}
	if (params.tagKeys?.length) {
		params.tagKeys.forEach(key => searchParams.append('tagKeys', key));
	}
	if (params.applicationName) {
		searchParams.append('applicationName', params.applicationName);
	}
	if (params.scope !== undefined) {
		searchParams.append('scope', params.scope.toString());
	}
	return searchParams;
}

// Add this interface near the other request interfaces
export interface SearchFeatureFlagRequest {
	key?: string;
	name?: string;
	description?: string;
}

// API Service
export const apiService = {
	auth: {
		setToken: TokenManager.setToken,
		removeToken: TokenManager.removeToken,
		getToken: TokenManager.getToken
	},

	health: {
		live: () => apiRequest<{ status: string }>('/health/live'),
		ready: () => apiRequest<{ status: string }>('/health/ready')
	},

	flags: {
		getPaged: async (params: GetFlagsParams = {}) => {
			const searchParams = buildQueryParams(params);
			const query = searchParams.toString();
			const response = await apiRequest<PagedFeatureFlagsResponse>(`/feature-flags${query ? `?${query}` : ''}`);
			return {
				...response,
				items: response.items.map(flag => DateTimeConverter.convertFeatureFlagDtoToLocal(flag))
			};
		},

		getAll: async () => {
			const flags = await apiRequest<FeatureFlagDto[]>('/feature-flags/all');
			return flags.map(flag => DateTimeConverter.convertFeatureFlagDtoToLocal(flag));
		},

		get: async (key: string, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}`, {}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		search: async (request: SearchFeatureFlagRequest) => {
			const searchParams = new URLSearchParams();
			if (request.key) searchParams.append('key', request.key);
			if (request.name) searchParams.append('name', request.name);
			if (request.description) searchParams.append('description', request.description);

			const query = searchParams.toString();
			const flags = await apiRequest<FeatureFlagDto[]>(`/feature-flags/search${query ? `?${query}` : ''}`);
			return flags.map(flag => DateTimeConverter.convertFeatureFlagDtoToLocal(flag));
		},

		create: async (request: CreateFeatureFlagRequest) => {
			const flag = await apiRequest<FeatureFlagDto>('/feature-flags', {
				method: 'POST',
				body: JSON.stringify(request)
			});
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		update: async (key: string, request: UpdateFlagRequest, scopeHeaders: ScopeHeaders) => {
			const utcRequest = DateTimeConverter.convertRequestToUtc(request);
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}`, {
				method: 'PUT',
				body: JSON.stringify(utcRequest)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		delete: (key: string, scopeHeaders: ScopeHeaders) =>
			apiRequest<void>(`/feature-flags/${key}`, { method: 'DELETE' }, scopeHeaders)
	},

	operations: {
		toggle: async (key: string, request: ToggleFlagRequest, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/toggle`, {
				method: 'POST',
				body: JSON.stringify(request)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		schedule: async (key: string, request: UpdateScheduleRequest, scopeHeaders: ScopeHeaders) => {
			const utcRequest = DateTimeConverter.convertRequestToUtc(request);
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/schedule`, {
				method: 'POST',
				body: JSON.stringify(utcRequest)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		setTimeWindow: async (key: string, request: UpdateTimeWindowRequest, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/time-window`, {
				method: 'POST',
				body: JSON.stringify(request)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		updateUserAccess: async (key: string, request: ManageUserAccessRequest, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/users`, {
				method: 'POST',
				body: JSON.stringify(request)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		updateTenantAccess: async (key: string, request: ManageTenantAccessRequest, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/tenants`, {
				method: 'POST',
				body: JSON.stringify(request)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		updateTargetingRules: async (key: string, request: UpdateTargetingRulesRequest, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/targeting-rules`, {
				method: 'POST',
				body: JSON.stringify(request)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		},

		updateVariations: async (key: string, request: UpdateVariationsRequest, scopeHeaders: ScopeHeaders) => {
			const flag = await apiRequest<FeatureFlagDto>(`/feature-flags/${key}/variations`, {
				method: 'POST',
				body: JSON.stringify(request)
			}, scopeHeaders);
			return DateTimeConverter.convertFeatureFlagDtoToLocal(flag);
		}
	},

	evaluation: {
		evaluate: async (key: string, scopeHeaders: ScopeHeaders, userId?: string, tenantId?: string, attributes?: Record<string, any>) => {
			const params = new URLSearchParams();
			if (userId) params.append('userId', userId);
			if (tenantId) params.append('tenantId', tenantId);
			if (attributes) params.append('kvAttributes', JSON.stringify(attributes));

			const query = params.toString();
			return apiRequest<EvaluationResult>(`/feature-flags/evaluate/${key}${query ? `?${query}` : ''}`, {}, scopeHeaders);
		}
	}
};

export default apiService;