import { useState, useEffect, useCallback } from 'react';
import {
    apiService,
    type FeatureFlagDto,
    type CreateFeatureFlagRequest,
    type UpdateFlagRequest,
    type PagedFeatureFlagsResponse,
    type GetFlagsParams,
    type ScopeHeaders,
    type ToggleFlagRequest,
    type UpdateScheduleRequest,
    type UpdateTimeWindowRequest,
    type ManageUserAccessRequest,
    type ManageTenantAccessRequest,
    type UpdateTargetingRulesRequest,
    type UpdateVariationsRequest,
    type EvaluationResult,
    type SearchFeatureFlagRequest,
    EvaluationMode,
    ApiError
} from '../services/apiService';
import { config } from '../config/environment';

export interface UseFeatureFlagsState {
    flags: FeatureFlagDto[];
    loading: boolean;
    error: string | null;
    selectedFlag: FeatureFlagDto | null;
    totalCount: number;
    currentPage: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
    currentFilters: GetFlagsParams;
    evaluationResults: Record<string, EvaluationResult>;
    evaluationLoading: Record<string, boolean>;
    searchResults: FeatureFlagDto[];  // Changed from searchResult: FeatureFlagDto | null
    searchLoading: boolean;
}

export interface UseFeatureFlagsActions {
    loadFlags: (params?: GetFlagsParams) => Promise<void>;
    loadFlagsPage: (page: number, params?: Omit<GetFlagsParams, 'page'>) => Promise<void>;
    getFlag: (key: string, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    refreshSelectedFlag: (scopeHeaders: ScopeHeaders) => Promise<void>;
    selectFlag: (flag: FeatureFlagDto | null) => void;
    createFlag: (request: CreateFeatureFlagRequest) => Promise<FeatureFlagDto>;
    updateFlag: (key: string, request: UpdateFlagRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    deleteFlag: (key: string, scopeHeaders: ScopeHeaders) => Promise<void>;
    toggleFlag: (key: string, mode: EvaluationMode, notes: string, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    scheduleFlag: (key: string, request: UpdateScheduleRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    setTimeWindow: (key: string, request: UpdateTimeWindowRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    updateUserAccess: (key: string, request: ManageUserAccessRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    updateTenantAccess: (key: string, request: ManageTenantAccessRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    updateTargetingRules: (key: string, request: UpdateTargetingRulesRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    updateVariations: (key: string, request: UpdateVariationsRequest, scopeHeaders: ScopeHeaders) => Promise<FeatureFlagDto>;
    filterFlags: (params: GetFlagsParams) => Promise<void>;
    searchFlag: (request: SearchFeatureFlagRequest) => Promise<void>;
    clearSearch: () => void;
    clearError: () => void;
    resetPagination: () => void;
    evaluateFlag: (key: string, scopeHeaders: ScopeHeaders, userId?: string, tenantId?: string, attributes?: Record<string, any>) => Promise<EvaluationResult>;
}

export function useFeatureFlags(): UseFeatureFlagsState & UseFeatureFlagsActions {
    const [state, setState] = useState<UseFeatureFlagsState>({
        flags: [],
        loading: true,
        error: null,
        selectedFlag: null,
        totalCount: 0,
        currentPage: 1,
        pageSize: 10,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
        currentFilters: {},
        evaluationResults: {},
        evaluationLoading: {},
        searchResults: [],  // Changed from searchResult: null
        searchLoading: false
    });

    const updateState = (updates: Partial<UseFeatureFlagsState>) => {
        setState(prev => ({ ...prev, ...updates }));
    };

    const handleError = (error: unknown, operation: string) => {
        console.error(`Error during ${operation}:`, error);
        const message = error instanceof ApiError
            ? error.message
            : `Failed to ${operation}. Please try again.`;
        updateState({ error: message, loading: false, searchLoading: false });
    };

    const updateFlagInState = (updatedFlag: FeatureFlagDto) => {
        setState(prev => ({
            ...prev,
            flags: prev.flags.map(flag =>
                flag.key === updatedFlag.key ? updatedFlag : flag
            ),
            selectedFlag: prev.selectedFlag?.key === updatedFlag.key
                ? updatedFlag
                : prev.selectedFlag,
            searchResults: prev.searchResults.map(flag =>  // Changed from searchResult
                flag.key === updatedFlag.key ? updatedFlag : flag
            )
        }));
    };

    const updateStateFromPagedResponse = (response: PagedFeatureFlagsResponse) => {
        updateState({
            flags: response.items,
            totalCount: response.totalCount,
            currentPage: response.page,
            pageSize: response.pageSize,
            totalPages: response.totalPages,
            hasNextPage: response.hasNextPage,
            hasPreviousPage: response.hasPreviousPage,
            loading: false
        });
    };

    const loadFlags = useCallback(async (params: GetFlagsParams = {}) => {
        try {
            updateState({ loading: true, error: null });

            const defaultParams = {
                page: 1,
                pageSize: 10,
                ...params
            };

            updateState({ currentFilters: defaultParams });

            const response = await apiService.flags.getPaged(defaultParams);
            updateStateFromPagedResponse(response);
        } catch (error) {
            handleError(error, 'load flags');
        }
    }, []);

    const loadFlagsPage = useCallback(async (page: number, params: Omit<GetFlagsParams, 'page'> = {}) => {
        try {
            updateState({ loading: true, error: null });

            const pageParams = {
                ...state.currentFilters,
                ...params,
                page,
                pageSize: state.pageSize
            };

            const response = await apiService.flags.getPaged(pageParams);
            updateStateFromPagedResponse(response);
        } catch (error) {
            handleError(error, 'load flags page');
        }
    }, [state.pageSize, state.currentFilters]);

    const getFlag = useCallback(async (key: string, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const flag = await apiService.flags.get(key, scopeHeaders);
            return flag;
        } catch (error) {
            handleError(error, 'get flag');
            throw error;
        }
    }, []);

    const searchFlag = useCallback(async (request: SearchFeatureFlagRequest): Promise<void> => {
        try {
            updateState({ searchLoading: true, error: null, searchResults: [] });  // Changed from searchResult: null
            const flags = await apiService.flags.search(request);
            updateState({ searchResults: flags, searchLoading: false });  // Changed from searchResult
        } catch (error) {
            handleError(error, 'search flag');
        }
    }, []);

    const clearSearch = useCallback(() => {
        updateState({ searchResults: [], searchLoading: false });  // Changed from searchResult: null
    }, []);

    const refreshSelectedFlag = useCallback(async (scopeHeaders: ScopeHeaders): Promise<void> => {
        if (!state.selectedFlag?.key) return;

        try {
            updateState({ error: null });
            const refreshedFlag = await apiService.flags.get(state.selectedFlag.key, scopeHeaders);
            updateState({ selectedFlag: refreshedFlag });
        } catch (error) {
            handleError(error, 'refresh selected flag');
        }
    }, [state.selectedFlag?.key]);

    const selectFlag = useCallback((flag: FeatureFlagDto | null) => {
        updateState({ selectedFlag: flag });
    }, []);

    const createFlag = useCallback(async (request: CreateFeatureFlagRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const newFlag = await apiService.flags.create(request);
            await loadFlagsPage(state.currentPage);
            return newFlag;
        } catch (error) {
            handleError(error, 'create flag');
            throw error;
        }
    }, [loadFlagsPage, state.currentPage]);

    const updateFlag = useCallback(async (key: string, request: UpdateFlagRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.flags.update(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update flag');
            throw error;
        }
    }, []);

    const deleteFlag = useCallback(async (key: string, scopeHeaders: ScopeHeaders): Promise<void> => {
        try {
            updateState({ error: null });
            await apiService.flags.delete(key, scopeHeaders);
            await loadFlagsPage(state.currentPage);

            if (state.selectedFlag?.key === key) {
                updateState({ selectedFlag: null });
            }

            // Remove from search results if present
            setState(prev => ({
                ...prev,
                searchResults: prev.searchResults.filter(flag => flag.key !== key)  // Changed from searchResult
            }));
        } catch (error) {
            handleError(error, 'delete flag');
            throw error;
        }
    }, [loadFlagsPage, state.currentPage, state.selectedFlag]);

    const toggleFlag = useCallback(async (key: string, mode: EvaluationMode, notes: string, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const request: ToggleFlagRequest = { evaluationMode: mode, notes };
            const updatedFlag = await apiService.operations.toggle(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'toggle flag');
            throw error;
        }
    }, []);

    const scheduleFlag = useCallback(async (key: string, request: UpdateScheduleRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.schedule(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'schedule flag');
            throw error;
        }
    }, []);

    const setTimeWindow = useCallback(async (key: string, request: UpdateTimeWindowRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.setTimeWindow(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'set time window');
            throw error;
        }
    }, []);

    const updateUserAccess = useCallback(async (key: string, request: ManageUserAccessRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.updateUserAccess(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update user access');
            throw error;
        }
    }, []);

    const updateTenantAccess = useCallback(async (key: string, request: ManageTenantAccessRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.updateTenantAccess(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update tenant access');
            throw error;
        }
    }, []);

    const updateTargetingRules = useCallback(async (key: string, request: UpdateTargetingRulesRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.updateTargetingRules(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update targeting rules');
            throw error;
        }
    }, []);

    const updateVariations = useCallback(async (key: string, request: UpdateVariationsRequest, scopeHeaders: ScopeHeaders): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.updateVariations(key, request, scopeHeaders);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update variations');
            throw error;
        }
    }, []);

    const filterFlags = useCallback(async (params: GetFlagsParams): Promise<void> => {
        try {
            updateState({ loading: true, error: null });

            const filterParams = {
                page: 1,
                pageSize: state.pageSize,
                ...params
            };

            updateState({ currentFilters: filterParams });

            const response = await apiService.flags.getPaged(filterParams);
            updateStateFromPagedResponse(response);
        } catch (error) {
            handleError(error, 'filter flags');
        }
    }, [state.pageSize]);

    const evaluateFlag = useCallback(async (key: string, scopeHeaders: ScopeHeaders, userId?: string, tenantId?: string, attributes?: Record<string, any>): Promise<EvaluationResult> => {
        try {
            updateState({
                evaluationLoading: {
                    ...state.evaluationLoading,
                    [key]: true
                }
            });

            const result = await apiService.evaluation.evaluate(key, scopeHeaders, userId, tenantId, attributes);

            updateState({
                evaluationResults: {
                    ...state.evaluationResults,
                    [key]: result
                },
                evaluationLoading: {
                    ...state.evaluationLoading,
                    [key]: false
                }
            });

            return result;
        } catch (error) {
            updateState({
                evaluationLoading: {
                    ...state.evaluationLoading,
                    [key]: false
                }
            });

            handleError(error, 'evaluate flag');
            throw error;
        }
    }, [state.evaluationLoading, state.evaluationResults]);

    const clearError = useCallback(() => {
        updateState({ error: null });
    }, []);

    const resetPagination = useCallback(() => {
        updateState({
            currentPage: 1,
            totalCount: 0,
            totalPages: 0,
            hasNextPage: false,
            hasPreviousPage: false,
            currentFilters: {}
        });
    }, []);

    useEffect(() => {
        loadFlags();
    }, [loadFlags]);

    return {
        ...state,
        loadFlags,
        loadFlagsPage,
        getFlag,
        refreshSelectedFlag,
        selectFlag,
        createFlag,
        updateFlag,
        deleteFlag,
        toggleFlag,
        scheduleFlag,
        setTimeWindow,
        updateUserAccess,
        updateTenantAccess,
        updateTargetingRules,
        updateVariations,
        filterFlags,
        searchFlag,
        clearSearch,
        clearError,
        resetPagination,
        evaluateFlag
    };
}