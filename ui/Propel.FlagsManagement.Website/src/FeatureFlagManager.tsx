import { useState } from 'react';
import { AlertCircle, Filter, Plus, Settings, X } from 'lucide-react';
import { useFeatureFlags } from './hooks/useFeatureFlags';
import type { CreateFeatureFlagRequest, GetFlagsParams, ModifyFlagRequest, UserAccessRequest, TenantAccessRequest, TargetingRulesRequest, TargetingRule } from './services/apiService';
import { getDayOfWeekNumber } from './utils/flagHelpers';

// Import components
import { FlagCard } from './components/FlagCard';
import { FlagDetails } from './components/FlagDetails';
import { FilterPanel } from './components/FilterPanel';
import { PaginationControls } from './components/PaginationControls';
import { CreateFlagModal } from './components/CreateFlagModal';
import { DeleteConfirmationModal } from './components/DeleteConfirmationModal';

const FeatureFlagManager = () => {
    const {
        flags,
        loading,
        error,
        selectedFlag,
        totalCount,
        currentPage,
        pageSize,
        totalPages,
        hasNextPage,
        hasPreviousPage,
        evaluationResults,
        evaluationLoading,
        selectFlag,
        createFlag,
        updateFlag,
        enableFlag,
        disableFlag,
        scheduleFlag,
        setTimeWindow,
        updateUserAccess,
        updateTenantAccess,
        updateTargetingRules,
        loadFlagsPage,
        filterFlags,
        deleteFlag,
        clearError,
        evaluateFlag,
    } = useFeatureFlags();

    const [showCreateForm, setShowCreateForm] = useState(false);
    const [showFilters, setShowFilters] = useState(false);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState<string | null>(null);
    const [deletingFlag, setDeletingFlag] = useState(false);
    const [filters, setFilters] = useState<{
        modes: number[];
        tagKeys: string[];
        tagValues: string[];
        expiringInDays?: number;
    }>({
        modes: [],
        tagKeys: [''],
        tagValues: [''],
        expiringInDays: undefined,
    });

    // Handler functions
    const quickToggle = async (flag: any) => {
        try {
            const reason = `Quick toggle via UI by user`;
            // Check if flag is currently enabled by looking at evaluationModes
            const isCurrentlyEnabled = flag.evaluationModes?.includes(1); // FlagEvaluationMode.Enabled = 1
            
            if (isCurrentlyEnabled) {
                await disableFlag(flag.key, reason);
            } else {
                await enableFlag(flag.key, reason);
            }
        } catch (error) {
            console.error('Failed to toggle flag:', error);
        }
    };

    const handleSetPercentage = async (flag: any, percentage: number) => {
        try {
            await updateUserAccess(flag.key, { percentage });
        } catch (error) {
            console.error('Failed to set percentage:', error);
        }
    };

    const handleEnableUsers = async (flag: any, userIds: string[]) => {
        try {
            const currentAllowedUsers = flag.allowedUsers || [];
            const updatedAllowedUsers = [...new Set([...currentAllowedUsers, ...userIds])];
            await updateUserAccess(flag.key, { allowedUsers: updatedAllowedUsers });
        } catch (error) {
            console.error('Failed to enable users:', error);
        }
    };

    const handleDisableUsers = async (flag: any, userIds: string[]) => {
        try {
            const currentBlockedUsers = flag.blockedUsers || [];
            const updatedBlockedUsers = [...new Set([...currentBlockedUsers, ...userIds])];
            await updateUserAccess(flag.key, { blockedUsers: updatedBlockedUsers });
        } catch (error) {
            console.error('Failed to disable users:', error);
        }
    };

    const handleScheduleFlag = async (flag: any, enableDate: string, disableDate?: string) => {
        try {
            await scheduleFlag(flag.key, {
                enableDate,
                disableDate,
                removeSchedule: false
            });
        } catch (error) {
            console.error('Failed to schedule flag:', error);
        }
    };

    const handleClearSchedule = async (flag: any) => {
        try {
            await scheduleFlag(flag.key, {
                enableDate: new Date().toISOString(), // Required but ignored when removing
                removeSchedule: true
            });
        } catch (error) {
            console.error('Failed to clear schedule:', error);
        }
    };

    const handleUpdateTimeWindow = async (flag: any, timeWindowData: {
        windowStartTime: string;
        windowEndTime: string;
        timeZone: string;
        windowDays: string[];
    }) => {
        try {
            // Convert string days to DayOfWeek numbers using helper function
            const windowDaysNumbers = timeWindowData.windowDays
                .map(day => getDayOfWeekNumber(day))
                .filter(day => day !== -1);

            await setTimeWindow(flag.key, {
                windowStartTime: timeWindowData.windowStartTime,
                windowEndTime: timeWindowData.windowEndTime,
                timeZone: timeWindowData.timeZone,
                windowDays: windowDaysNumbers,
                removeTimeWindow: false
            });
        } catch (error) {
            console.error('Failed to update time window:', error);
        }
    };

    const handleClearTimeWindow = async (flag: any) => {
        try {
            await setTimeWindow(flag.key, {
                windowStartTime: '00:00:00', // Required but ignored when removing
                windowEndTime: '23:59:59',   // Required but ignored when removing
                timeZone: 'UTC',             // Required but ignored when removing
                windowDays: [],              // Required but ignored when removing
                removeTimeWindow: true
            });
        } catch (error) {
            console.error('Failed to clear time window:', error);
        }
    };

    // Updated function to match the FlagDetails interface
    const handleUpdateTargetingRulesWrapper = async (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => {
        if (!selectedFlag) return;
        
        try {
            const request: TargetingRulesRequest = {
                targetingRules: targetingRules && targetingRules.length > 0 ? targetingRules : undefined,
                removeTargetingRules: removeTargetingRules || (!targetingRules || targetingRules.length === 0)
            };
            
            await updateTargetingRules(selectedFlag.key, request);
        } catch (error) {
            console.error('Failed to update targeting rules:', error);
        }
    };

    const handleUpdateFlag = async (flag: any, updates: {
        name?: string;
        description?: string;
        expirationDate?: string;
        isPermanent?: boolean;
        tags?: Record<string, string>;
    }) => {
        try {
            const updateRequest: ModifyFlagRequest = {
                ...updates
            };
            await updateFlag(flag.key, updateRequest);
        } catch (error) {
            console.error('Failed to update flag:', error);
        }
    };

    const handleDeleteFlag = async (flagKey: string) => {
        try {
            setDeletingFlag(true);
            await deleteFlag(flagKey);
            setShowDeleteConfirm(null);
        } catch (error) {
            console.error('Failed to delete flag:', error);
        } finally {
            setDeletingFlag(false);
        }
    };

    const handleEvaluateFlag = async (key: string, userId?: string, attributes?: Record<string, any>) => {
        try {
            return await evaluateFlag(key, userId, attributes);
        } catch (error) {
            console.error('Failed to evaluate flag:', error);
            throw error;
        }
    };

    // Wrapper function to match CreateFlagModal's expected signature
    const handleCreateFlag = async (request: CreateFeatureFlagRequest): Promise<void> => {
        await createFlag(request);
    };

    const applyFilters = async () => {
        const params: GetFlagsParams = {
            page: 1,
            pageSize: pageSize,
        };

        // Handle evaluation modes filter
        if (filters.modes && filters.modes.length > 0) {
            params.modes = filters.modes;
        }

        // Handle expiring in days filter
        if (filters.expiringInDays !== undefined && filters.expiringInDays > 0) {
            params.expiringInDays = filters.expiringInDays;
        }

        // Handle tag filters
        const tags: string[] = [];
        for (let i = 0; i < filters.tagKeys.length; i++) {
            const key = filters.tagKeys[i]?.trim();
            const value = filters.tagValues[i]?.trim();
            
            if (key) {
                if (value) {
                    tags.push(`${key}:${value}`);
                } else {
                    tags.push(key);
                }
            }
        }
        
        if (tags.length > 0) {
            params.tags = tags;
        }

        await filterFlags(params);
        setShowFilters(false);
    };

    const clearFilters = async () => {
        setFilters({
            modes: [],
            tagKeys: [''],
            tagValues: [''],
            expiringInDays: undefined,
        });
        await filterFlags({ page: 1, pageSize: pageSize });
        setShowFilters(false);
    };

    const goToPage = async (page: number) => {
        if (page >= 1 && page <= totalPages) {
            await loadFlagsPage(page);
        }
    };

    const goToPreviousPage = async () => {
        if (hasPreviousPage) {
            await goToPage(currentPage - 1);
        }
    };

    const goToNextPage = async () => {
        if (hasNextPage) {
            await goToPage(currentPage + 1);
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
            </div>
        );
    }

    return (
        <div className="max-w-[1600px] mx-auto p-8 bg-gray-50 min-h-screen">
            {/* Error display */}
            {error && (
                <div className="mb-6 bg-red-50 border border-red-200 rounded-lg p-4 flex items-center gap-3">
                    <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0" />
                    <div className="flex-1">
                        <p className="text-red-800">{error}</p>
                    </div>
                    <button 
                        onClick={clearError}
                        className="text-red-500 hover:text-red-700"
                    >
                        <X className="w-4 h-4" />
                    </button>
                </div>
            )}

            <div className="flex justify-between items-center mb-8">
                <div>
                    <h1 className="text-2xl font-bold text-gray-900">Feature Flags</h1>
                    <p className="text-gray-600">Manage feature releases and rollouts</p>
                </div>
                <div className="flex gap-4">
                    <button
                        onClick={() => setShowFilters(!showFilters)}
                        className="flex items-center gap-2 px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 font-medium"
                    >
                        <Filter className="w-4 h-4" />
                        Filters
                    </button>
                    <button
                        onClick={() => setShowCreateForm(true)}
                        className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-medium"
                    >
                        <Plus className="w-4 h-4" />
                        Create Flag
                    </button>
                </div>
            </div>

            {/* Filter Panel */}
            {showFilters && (
                <FilterPanel
                    filters={filters}
                    onFiltersChange={setFilters}
                    onApplyFilters={applyFilters}
                    onClearFilters={clearFilters}
                    onClose={() => setShowFilters(false)}
                />
            )}

            <div className="grid grid-cols-1 xl:grid-cols-5 gap-8">
                {/* Flag List - Takes up 2 columns */}
                <div className="xl:col-span-2 space-y-4">
                    <div className="flex justify-between items-center">
                        <h2 className="text-lg font-semibold text-gray-900">
                            Flags ({totalCount} total)
                        </h2>
                    </div>

                    <div className="space-y-4">
                        {flags.map((flag) => (
                            <FlagCard
                                key={flag.key}
                                flag={flag}
                                isSelected={selectedFlag?.key === flag.key}
                                onClick={() => selectFlag(flag)}
                                onDelete={(key) => setShowDeleteConfirm(key)}
                            />
                        ))}
                    </div>

                    {/* Pagination Controls */}
                    <PaginationControls
                        currentPage={currentPage}
                        totalPages={totalPages}
                        pageSize={pageSize}
                        totalCount={totalCount}
                        hasNextPage={hasNextPage}
                        hasPreviousPage={hasPreviousPage}
                        loading={loading}
                        onPageChange={goToPage}
                        onPreviousPage={goToPreviousPage}
                        onNextPage={goToNextPage}
                    />
                </div>

                {/* Flag Details - Takes up 3 columns */}
                <div className="xl:col-span-3">
                    {selectedFlag ? (
                        <>
                            <h2 className="text-lg font-semibold text-gray-900 mb-4">Flag Details</h2>
                            <FlagDetails
                                flag={selectedFlag}
                                onToggle={quickToggle}
                                onUpdateUserAccess={(allowedUsers, blockedUsers, percentage) => {
                                    const request: UserAccessRequest = {};
                                    if (allowedUsers !== undefined) request.allowedUsers = allowedUsers;
                                    if (blockedUsers !== undefined) request.blockedUsers = blockedUsers;
                                    if (percentage !== undefined) request.percentage = percentage;
                                    return updateUserAccess(selectedFlag.key, request);
                                }}
                                onUpdateTenantAccess={(allowedTenants, blockedTenants, percentage) => {
                                    const request: TenantAccessRequest = {};
                                    if (allowedTenants !== undefined) request.allowedTenants = allowedTenants;
                                    if (blockedTenants !== undefined) request.blockedTenants = blockedTenants;
                                    if (percentage !== undefined) request.percentage = percentage;
                                    return updateTenantAccess(selectedFlag.key, request);
                                }}
                                onUpdateTargetingRules={handleUpdateTargetingRulesWrapper}
                                onSchedule={handleScheduleFlag}
                                onClearSchedule={handleClearSchedule}
                                onUpdateTimeWindow={handleUpdateTimeWindow}
                                onClearTimeWindow={handleClearTimeWindow}
                                onUpdateFlag={handleUpdateFlag}
                                onDelete={(key) => setShowDeleteConfirm(key)}
                                onEvaluateFlag={handleEvaluateFlag}
                                evaluationResult={selectedFlag ? evaluationResults[selectedFlag.key] : undefined}
                                evaluationLoading={selectedFlag ? evaluationLoading[selectedFlag.key] || false : false}
                            />
                        </>
                    ) : (
                        <div className="bg-white border border-gray-200 rounded-lg p-8 text-center">
                            <Settings className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                            <h3 className="text-lg font-medium text-gray-900 mb-2">Select a Feature Flag</h3>
                            <p className="text-gray-600">Choose a flag from the list to view and manage its settings</p>
                        </div>
                    )}
                </div>
            </div>

            {/* Modals */}
            <CreateFlagModal
                isOpen={showCreateForm}
                onClose={() => setShowCreateForm(false)}
                onSubmit={handleCreateFlag}
            />

            <DeleteConfirmationModal
                isOpen={!!showDeleteConfirm}
                flagKey={showDeleteConfirm || ''}
                flagName={flags.find(f => f.key === showDeleteConfirm)?.name || ''}
                isDeleting={deletingFlag}
				onConfirm={() => showDeleteConfirm && handleDeleteFlag(showDeleteConfirm)}
                onCancel={() => setShowDeleteConfirm(null)}
            />
        </div>
    );
};

export default FeatureFlagManager;