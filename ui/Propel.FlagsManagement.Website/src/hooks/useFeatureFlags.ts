import { useState, useEffect, useCallback } from 'react';
import { 
    apiService, 
    type FeatureFlagDto, 
    type CreateFeatureFlagRequest, 
    type ModifyFlagRequest,
    ApiError 
} from '../services/apiService';
import { config } from '../config/environment';

export interface UseFeatureFlagsState {
    flags: FeatureFlagDto[];
    loading: boolean;
    error: string | null;
    selectedFlag: FeatureFlagDto | null;
}

export interface UseFeatureFlagsActions {
    loadFlags: () => Promise<void>;
    selectFlag: (flag: FeatureFlagDto | null) => void;
    createFlag: (request: CreateFeatureFlagRequest) => Promise<FeatureFlagDto>;
    updateFlag: (key: string, request: ModifyFlagRequest) => Promise<FeatureFlagDto>;
    deleteFlag: (key: string) => Promise<void>;
    enableFlag: (key: string, reason: string) => Promise<FeatureFlagDto>;
    disableFlag: (key: string, reason: string) => Promise<FeatureFlagDto>;
    scheduleFlag: (key: string, enableDate: string, disableDate?: string) => Promise<FeatureFlagDto>;
    setPercentage: (key: string, percentage: number) => Promise<FeatureFlagDto>;
    enableUsers: (key: string, userIds: string[]) => Promise<FeatureFlagDto>;
    disableUsers: (key: string, userIds: string[]) => Promise<FeatureFlagDto>;
    searchFlags: (params?: { tag?: string; status?: string }) => Promise<void>;
    clearError: () => void;
}

export function useFeatureFlags(): UseFeatureFlagsState & UseFeatureFlagsActions {
    const [state, setState] = useState<UseFeatureFlagsState>({
        flags: [],
        loading: true,
        error: null,
        selectedFlag: null,
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

    const loadFlags = useCallback(async () => {
        try {
            console.log('Starting to load flags...');
            console.log('API Base URL:', config.API_BASE_URL);
            console.log('Token:', apiService.auth.getToken() ? 'Present' : 'Missing');
            
            updateState({ loading: true, error: null });
            const flags = await apiService.flags.getAll();
            console.log('Flags loaded successfully:', flags);
            updateState({ flags, loading: false });
        } catch (error) {
            console.error('Failed to load flags:', error);
            handleError(error, 'load flags');
        }
    }, []);

    const selectFlag = useCallback((flag: FeatureFlagDto | null) => {
        updateState({ selectedFlag: flag });
    }, []);

    const createFlag = useCallback(async (request: CreateFeatureFlagRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const newFlag = await apiService.flags.create(request);
            setState(prev => ({
                ...prev,
                flags: [...prev.flags, newFlag],
            }));
            return newFlag;
        } catch (error) {
            handleError(error, 'create flag');
            throw error;
        }
    }, []);

    const updateFlag = useCallback(async (key: string, request: ModifyFlagRequest): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
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
            setState(prev => ({
                ...prev,
                flags: prev.flags.filter(flag => flag.key !== key),
                selectedFlag: prev.selectedFlag?.key === key ? null : prev.selectedFlag,
            }));
        } catch (error) {
            handleError(error, 'delete flag');
            throw error;
        }
    }, []);

    const enableFlag = useCallback(async (key: string, reason: string): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
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
            const updatedFlag = await apiService.operations.disable(key, { reason });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'disable flag');
            throw error;
        }
    }, []);

    const scheduleFlag = useCallback(async (key: string, enableDate: string, disableDate?: string): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.schedule(key, { enableDate, disableDate });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'schedule flag');
            throw error;
        }
    }, []);

    const setPercentage = useCallback(async (key: string, percentage: number): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.setPercentage(key, { percentage });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'set percentage');
            throw error;
        }
    }, []);

    const enableUsers = useCallback(async (key: string, userIds: string[]): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.enableUsers(key, { userIds });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        } catch (error) {
            handleError(error, 'enable users');
            throw error;
        }
    }, []);

    const disableUsers = useCallback(async (key: string, userIds: string[]): Promise<FeatureFlagDto> => {
        try {
            updateState({ error: null });
            const updatedFlag = await apiService.operations.disableUsers(key, { userIds });
            updateFlagInState(updatedFlag);
            return updatedFlag;
        }
        catch (error) {
            handleError(error, 'disable users');
            throw error;
        }
    }, []);

    const searchFlags = useCallback(async (params?: { tag?: string; status?: string }): Promise<void> => {
        try {
            updateState({ loading: true, error: null });
            const flags = await apiService.flags.search(params);
            updateState({ flags, loading: false });
        } catch (error) {
            handleError(error, 'search flags');
        }
    }, []);

    const clearError = useCallback(() => {
        updateState({ error: null });
    }, []);

    // Load flags on mount
    useEffect(() => {
        console.log('useFeatureFlags effect triggered - loading flags...');
        loadFlags();
    }, [loadFlags]);

    return {
        ...state,
        loadFlags,
        selectFlag,
        createFlag,
        updateFlag,
        deleteFlag,
        enableFlag,
        disableFlag,
        scheduleFlag,
        setPercentage,
        enableUsers,
        disableUsers,
        searchFlags,
        clearError,
    };
}