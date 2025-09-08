import { useState, useEffect, useCallback } from 'react';
import {
    apiService,
    type FeatureFlagDto,
    type CreateFeatureFlagRequest,
    type ModifyFlagRequest,
    type PagedFeatureFlagsResponse,
    type GetFlagsParams,
    ApiError,
    type SetTimeWindowRequest,
    type ScheduleFlagRequest,
    type EvaluationResult,
    type UserAccessRequest
} from '../services/apiService';
import { config } from '../config/environment';

export interface UseFeatureFlagsState {
    flags: FeatureFlagDto[];
    loading: boolean;
    error: string | null;
    selectedFlag: FeatureFlagDto | null;
    // Pagination state
    totalCount: number;
    currentPage: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
    // Current filters state
    currentFilters: GetFlagsParams;
    // Evaluation state
    evaluationResults: Record<string, EvaluationResult>;
    evaluationLoading: Record<string, boolean>;
}

export interface UseFeatureFlagsActions {
    loadFlags: (params?: GetFlagsParams) => Promise<void>;
    loadFlagsPage: (page: number, params?: Omit<GetFlagsParams, 'page'>) => Promise<void>;
    getFlag: (key: string) => Promise<FeatureFlagDto>;
    refreshSelectedFlag: () => Promise<void>;
    selectFlag: (flag: FeatureFlagDto | null) => void;
    createFlag: (request: CreateFeatureFlagRequest) => Promise<FeatureFlagDto>;
    updateFlag: (key: string, request: ModifyFlagRequest) => Promise<FeatureFlagDto>;
    deleteFlag: (key: string) => Promise<void>;
    enableFlag: (key: string, reason: string) => Promise<FeatureFlagDto>;
    disableFlag: (key: string, reason: string) => Promise<FeatureFlagDto>;
    scheduleFlag: (key: string, request: ScheduleFlagRequest) => Promise<FeatureFlagDto>;
	setTimeWindow: (key: string, request: SetTimeWindowRequest) => Promise<FeatureFlagDto>;
    updateUserAccess: (key: string, request: UserAccessRequest) => Promise<FeatureFlagDto>;
    searchFlags: (params?: { tag?: string; status?: string }) => Promise<void>;
    filterFlags: (params: GetFlagsParams) => Promise<void>;
    clearError: () => void;
    resetPagination: () => void;
    // Evaluation actions
    evaluateFlag: (key: string, userId?: string, attributes?: Record<string, any>) => Promise<EvaluationResult>;
    evaluateMultipleFlags: (flagKeys: string[], userId?: string, attributes?: Record<string, any>) => Promise<Record<string, EvaluationResult>>;
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
    });

    const updateState = (updates: Partial<UseFeatureFlagsState>) => {
        setState(prev => ({ ...prev, ...updates }));
    };

    const handleError = (error: unknown, operation: string) => {
        console.error(`Error during ${operation}:`, error);
        const message = error instanceof ApiError 
            ? error.message 
            : `Failed to ${operation}. Please try again.`;
        updateState({ error: message, loading: false });
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
            console.log('Starting to load flags with params:', params);
            console.log('API Base URL:', config.API_BASE_URL);
            console.log('Token:', apiService.auth.getToken() ? 'Present' : 'Missing');

            updateState({ loading: true, error: null });

            const defaultParams = {
                page: 1,
                pageSize: 10,
                ...params
            };

            // Store current filters
            updateState({ currentFilters: defaultParams });

            const response = await apiService.flags.getPaged(defaultParams);
            console.log('Paged flags loaded successfully:', response);
            updateStateFromPagedResponse(response);
        } catch (error) {
            console.error('Failed to load flags:', error);
            handleError(error, 'load flags');
        }
    }, []);

    const loadFlagsPage = useCallback(async (page: number, params: Omit<GetFlagsParams, 'page'> = {}) => {
        try {
            updateState({ loading: true, error: null });

            // Merge current filters with new params, but use current filters as base
            const pageParams = {
                ...state.currentFilters,
                ...params,
                page,
                pageSize: state.pageSize,
            };

            const response = await apiService.flags.getPaged(pageParams);
            updateStateFromPagedResponse(response);
        } catch (error) {
            handleError(error, 'load flags page');
        }
    }, [state.pageSize, state.currentFilters]);

    const getFlag = useCallback(async (key: string): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // This will return a converted DTO with local times
            const flag = await apiService.flags.get(key);
            return flag;
        } catch (error) {
            handleError(error, 'get flag');
            throw error;
        }
    }, []);

    const refreshSelectedFlag = useCallback(async (): Promise<void> => {
        if (!state.selectedFlag?.key) {
            return;
        }
        
        try {
            updateState({ error: null });
            // Get the fresh converted DTO with local times
            const refreshedFlag = await apiService.flags.get(state.selectedFlag.key);
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
            // Returns converted DTO with local times
            const newFlag = await apiService.flags.create(request);
            
            // Reload the current page to maintain pagination consistency
            await loadFlagsPage(state.currentPage);
            
            return newFlag;
        } catch (error) {
            handleError(error, 'create flag');
            throw error;
        }
    }, [loadFlagsPage, state.currentPage]);

    const updateFlag = useCallback(async (key: string, request: ModifyFlagRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // Returns converted DTO with local times
            const updatedFlag = await apiService.flags.update(key, request);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update flag');
            throw error;
        }
    }, []);

    const deleteFlag = useCallback(async (key: string): Promise<void> => {
        try {
            updateState({ error: null });
            await apiService.flags.delete(key);
            
            // Reload the current page to maintain pagination consistency
            await loadFlagsPage(state.currentPage);
            
            // Clear selection if deleted flag was selected
            if (state.selectedFlag?.key === key) {
                updateState({ selectedFlag: null });
            }
        } catch (error) {
            handleError(error, 'delete flag');
            throw error;
        }
    }, [loadFlagsPage, state.currentPage, state.selectedFlag]);

    const enableFlag = useCallback(async (key: string, reason: string): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // Returns converted DTO with local times
            const updatedFlag = await apiService.operations.enable(key, { reason });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'enable flag');
            throw error;
        }
    }, []);

    const disableFlag = useCallback(async (key: string, reason: string): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // Returns converted DTO with local times
            const updatedFlag = await apiService.operations.disable(key, { reason });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'disable flag');
            throw error;
        }
    }, []);

    const scheduleFlag = useCallback(async (key: string, request: ScheduleFlagRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // Returns converted DTO with local times
            const updatedFlag = await apiService.operations.schedule(key, request);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'schedule flag');
            throw error;
        }
    }, []);

    const setTimeWindow = useCallback(async (key: string, request: SetTimeWindowRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // Returns converted DTO with local times
            const updatedFlag = await apiService.operations.setTimeWindow(key, request);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'set time window');
            throw error;
        }
    }, []);

    const updateUserAccess = useCallback(async (key: string, request: UserAccessRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            // Returns converted DTO with local times
            const updatedFlag = await apiService.operations.updateUserAccess(key, request);
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'update user access');
            throw error;
        }
    }, []);

    const searchFlags = useCallback(async (params?: { tag?: string; status?: string }): Promise<void> => {
        try {
            updateState({ loading: true, error: null });
            // Returns array of converted DTOs with local times
            const flags = await apiService.flags.search(params);
            updateState({
                flags,
                loading: false,
                // Reset pagination for search results
                totalCount: flags.length,
                currentPage: 1,
                totalPages: 1,
                hasNextPage: false,
                hasPreviousPage: false,
                currentFilters: {} // Reset filters for search
            });
        } catch (error) {
            handleError(error, 'search flags');
        }
    }, []);

    const filterFlags = useCallback(async (params: GetFlagsParams): Promise<void> => {
        try {
            updateState({ loading: true, error: null });
            
            const filterParams = {
                page: 1, // Reset to first page when filtering
                pageSize: state.pageSize,
                ...params
            };
            
            // Store the new filters
            updateState({ currentFilters: filterParams });
            
            // Returns response with converted DTOs with local times
            const response = await apiService.flags.getPaged(filterParams);
            updateStateFromPagedResponse(response);
        } catch (error) {
            handleError(error, 'filter flags');
        }
    }, [state.pageSize]);

    const evaluateFlag = useCallback(async (key: string, userId?: string, attributes?: Record<string, any>): Promise<EvaluationResult> => {
        try {
            // Set loading state for this specific flag
            updateState({ 
                evaluationLoading: { 
                    ...state.evaluationLoading, 
                    [key]: true 
                } 
            });

            const result = await apiService.evaluation.evaluate(key, userId, attributes);
            
            // Update evaluation results
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
            // Clear loading state on error
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

    const evaluateMultipleFlags = useCallback(async (flagKeys: string[], userId?: string, attributes?: Record<string, any>): Promise<Record<string, EvaluationResult>> => {
        try {
            // Set loading state for all flags
            const loadingState = flagKeys.reduce((acc, key) => ({ ...acc, [key]: true }), {});
            updateState({ 
                evaluationLoading: { 
                    ...state.evaluationLoading, 
                    ...loadingState 
                } 
            });

            const results = await apiService.evaluation.evaluateMultiple({
                flagKeys,
                userId,
                attributes
            });
            
            // Update evaluation results and clear loading states
            const clearLoadingState = flagKeys.reduce((acc, key) => ({ ...acc, [key]: false }), {});
            updateState({ 
                evaluationResults: { 
                    ...state.evaluationResults, 
                    ...results 
                },
                evaluationLoading: { 
                    ...state.evaluationLoading, 
                    ...clearLoadingState 
                }
            });

            return results;
        } catch (error) {
            // Clear loading state on error
            const clearLoadingState = flagKeys.reduce((acc, key) => ({ ...acc, [key]: false }), {});
            updateState({ 
                evaluationLoading: { 
                    ...state.evaluationLoading, 
                    ...clearLoadingState 
                } 
            });
            
            handleError(error, 'evaluate multiple flags');
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

    // Load flags on mount
    useEffect(() => {
        console.log('useFeatureFlags effect triggered - loading flags...');
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
        enableFlag,
        disableFlag,
        scheduleFlag,
        setTimeWindow,
        updateUserAccess,
        searchFlags,
        filterFlags,
        clearError,
        resetPagination,
        evaluateFlag,
        evaluateMultipleFlags,
    };
}