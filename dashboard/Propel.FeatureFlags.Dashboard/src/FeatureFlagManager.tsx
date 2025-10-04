import { useState } from 'react';
import { Filter, Plus, Settings, Search } from 'lucide-react';
import { useFeatureFlags } from './hooks/useFeatureFlags';
import type {
    CreateFeatureFlagRequest,
    GetFlagsParams,
    UpdateFlagRequest,
    ManageUserAccessRequest,
    ManageTenantAccessRequest,
    UpdateTargetingRulesRequest,
    UpdateVariationsRequest,
    TargetingRule,
    ScopeHeaders,
    FeatureFlagDto,
    SearchFeatureFlagRequest
} from './services/apiService';
import { EvaluationMode, Scope } from './services/apiService';
import { getDayOfWeekNumber } from './utils/flagHelpers';

// Import components
import { Header } from './components/Header';
import { FlagCard } from './components/FlagCard';
import { FlagDetails } from './components/FlagDetails';
import { FilterPanel } from './components/FilterPanel';
import { SearchPanel } from './components/SearchPanel';
import { PaginationControls } from './components/PaginationControls';
import { CreateFlagModal } from './components/CreateFlagModal';
import { DeleteConfirmationModal } from './components/DeleteConfirmationModal';

const FeatureFlagManager = () => {
    const {
        flags,
        loading,
        selectedFlag,
        totalCount,
        currentPage,
        pageSize,
        totalPages,
        hasNextPage,
        hasPreviousPage,
        evaluationResults,
        evaluationLoading,
        searchResults,
        searchLoading,
        selectFlag,
        createFlag,
        updateFlag,
        toggleFlag,
        scheduleFlag,
        setTimeWindow,
        updateUserAccess,
        updateTenantAccess,
        updateTargetingRules,
        updateVariations,
        loadFlagsPage,
        filterFlags,
        searchFlag,
        clearSearch,
        deleteFlag,
        evaluateFlag,
    } = useFeatureFlags();

    const [showCreateForm, setShowCreateForm] = useState(false);
    const [showFilters, setShowFilters] = useState(false);
    const [showSearch, setShowSearch] = useState(false);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState<string | null>(null);
    const [deletingFlag, setDeletingFlag] = useState(false);
    const [hasSearched, setHasSearched] = useState(false);
    const [filters, setFilters] = useState<{
        modes: number[];
        tagKeys: string[];
        tagValues: string[];
        expiringInDays?: number;
        scope?: Scope;
        applicationName?: string;
    }>({
        modes: [],
        tagKeys: [''],
        tagValues: [''],
        expiringInDays: undefined,
    });

    // Extract scope headers from flag
    const getScopeHeaders = (flag: FeatureFlagDto): ScopeHeaders => ({
        scope: flag.scope === Scope.Global ? 'Global' : 'Application',
        applicationName: flag.applicationName,
        applicationVersion: flag.applicationVersion
    });

    // Handler functions
    const quickToggle = async (flag: FeatureFlagDto) => {
        const scopeHeaders = getScopeHeaders(flag);
        const isCurrentlyEnabled = flag.modes?.includes(EvaluationMode.On);
        const mode = isCurrentlyEnabled ? EvaluationMode.Off : EvaluationMode.On;
        await toggleFlag(flag.key, mode, 'Quick toggle via UI', scopeHeaders);
    };

    const handleScheduleFlag = async (flag: FeatureFlagDto, enableOn: string, disableOn?: string) => {
        const scopeHeaders = getScopeHeaders(flag);
        await scheduleFlag(flag.key, { enableOn, disableOn }, scopeHeaders);
    };

    const handleClearSchedule = async (flag: FeatureFlagDto) => {
        const scopeHeaders = getScopeHeaders(flag);
        await scheduleFlag(flag.key, {
            enableOn: undefined,
            disableOn: undefined
        }, scopeHeaders);
    };

    const handleUpdateTimeWindow = async (flag: FeatureFlagDto, timeWindowData: {
        startOn: string;
        endOn: string;
        timeZone: string;
        daysActive: string[];
    }) => {
        const scopeHeaders = getScopeHeaders(flag);
        const daysActiveNumbers = timeWindowData.daysActive
            .map(day => getDayOfWeekNumber(day))
            .filter(day => day !== -1);

        await setTimeWindow(flag.key, {
            startOn: timeWindowData.startOn,
            endOn: timeWindowData.endOn,
            timeZone: timeWindowData.timeZone,
            daysActive: daysActiveNumbers,
            removeTimeWindow: false
        }, scopeHeaders);
    };

    const handleClearTimeWindow = async (flag: FeatureFlagDto) => {
        const scopeHeaders = getScopeHeaders(flag);
        await setTimeWindow(flag.key, {
            startOn: '00:00:00',
            endOn: '23:59:59',
            timeZone: 'UTC',
            daysActive: [],
            removeTimeWindow: true
        }, scopeHeaders);
    };

    const handleUpdateTargetingRulesWrapper = async (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => {
        if (!selectedFlag) return;

        const scopeHeaders = getScopeHeaders(selectedFlag);
        const request: UpdateTargetingRulesRequest = {
            targetingRules: targetingRules && targetingRules.length > 0 ? targetingRules : undefined,
            removeTargetingRules: removeTargetingRules || (!targetingRules || targetingRules.length === 0)
        };

        await updateTargetingRules(selectedFlag.key, request, scopeHeaders);
    };

    const handleUpdateVariations = async (variations: Record<string, any>, defaultVariation: string) => {
        if (!selectedFlag) return;

        const scopeHeaders = getScopeHeaders(selectedFlag);
        
        // Convert variations object to array format expected by API
        const variationsArray = Object.entries(variations).map(([key, value]) => ({
            key,
            value: typeof value === 'string' ? value : JSON.stringify(value)
        }));

        const request: UpdateVariationsRequest = {
            variations: variationsArray.length > 0 ? variationsArray : undefined,
            defaultVariation,
            removeVariations: variationsArray.length === 0
        };

        await updateVariations(selectedFlag.key, request, scopeHeaders);
    };

    const handleClearVariations = async () => {
        if (!selectedFlag) return;

        const scopeHeaders = getScopeHeaders(selectedFlag);
        const request: UpdateVariationsRequest = {
            variations: undefined,
            defaultVariation: 'off',
            removeVariations: true
        };

        await updateVariations(selectedFlag.key, request, scopeHeaders);
    };

    const handleUpdateFlag = async (flag: FeatureFlagDto, updates: {
        name?: string;
        description?: string;
        expirationDate?: string;
        tags?: Record<string, string>;
        notes?: string;
    }) => {
        const scopeHeaders = getScopeHeaders(flag);
        const updateRequest: UpdateFlagRequest = { ...updates };
        await updateFlag(flag.key, updateRequest, scopeHeaders);
    };

    const handleDeleteFlag = async (flagKey: string) => {
        try {
            setDeletingFlag(true);
            const flag = flags.find(f => f.key === flagKey) || searchResults.find(f => f.key === flagKey);
            if (!flag) return;

            const scopeHeaders = getScopeHeaders(flag);
            await deleteFlag(flagKey, scopeHeaders);
            setShowDeleteConfirm(null);
        } finally {
            setDeletingFlag(false);
        }
    };

    const handleEvaluateFlag = async (key: string, userId?: string, tenantId?: string, attributes?: Record<string, any>) => {
        const flag = flags.find(f => f.key === key) || selectedFlag || searchResults.find(f => f.key === key);
        if (!flag) throw new Error('Flag not found');

        const scopeHeaders = getScopeHeaders(flag);
        return await evaluateFlag(key, scopeHeaders, userId, tenantId, attributes);
    };

    const handleCreateFlag = async (request: CreateFeatureFlagRequest): Promise<void> => {
        await createFlag(request);
        setShowCreateForm(false);
    };

    const handleSearch = async (request: SearchFeatureFlagRequest) => {
        await searchFlag(request);
        setHasSearched(true);
        setShowSearch(false);
    };

    const handleClearSearch = () => {
        clearSearch();
        setHasSearched(false);
        setShowSearch(false);
    };

    const applyFilters = async () => {
        const params: GetFlagsParams = {
            page: 1,
            pageSize: pageSize,
        };

        if (filters.modes && filters.modes.length > 0) {
            params.modes = filters.modes as EvaluationMode[];
        }

        if (filters.expiringInDays !== undefined && filters.expiringInDays > 0) {
            params.expiringInDays = filters.expiringInDays;
        }

        if (filters.scope !== undefined) {
            params.scope = filters.scope;
        }

        if (filters.applicationName) {
            params.applicationName = filters.applicationName;
        }

        const tagKeys: string[] = [];
        const tags: string[] = [];

        for (let i = 0; i < filters.tagKeys.length; i++) {
            const key = filters.tagKeys[i]?.trim();
            const value = filters.tagValues[i]?.trim();

            if (key) {
                if (value) {
                    tags.push(`${key}:${value}`);
                } else {
                    tagKeys.push(key);
                }
            }
        }

        if (tagKeys.length > 0) {
            params.tagKeys = tagKeys;
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
            scope: undefined,
            applicationName: undefined,
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
        if (hasPreviousPage) await goToPage(currentPage - 1);
    };

    const goToNextPage = async () => {
        if (hasNextPage) await goToPage(currentPage + 1);
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-gray-50">
                <Header />
                <div className="flex items-center justify-center min-h-[400px]">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gray-50">
            <Header />
            
            <div className="max-w-[1600px] mx-auto p-8">
                {/* Action Bar */}
                <div className="flex justify-end items-center mb-6">
                    <div className="flex gap-3">
                        <button
                            onClick={() => setShowSearch(!showSearch)}
                            className="flex items-center gap-2 px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 font-medium transition-colors"
                        >
                            <Search className="w-4 h-4" />
                            Search
                        </button>
                        <button
                            onClick={() => setShowFilters(!showFilters)}
                            className="flex items-center gap-2 px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 font-medium transition-colors"
                        >
                            <Filter className="w-4 h-4" />
                            Filters
                        </button>
                        <button
                            onClick={() => setShowCreateForm(true)}
                            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-medium transition-colors shadow-sm"
                        >
                            <Plus className="w-4 h-4" />
                            Create Flag
                        </button>
                    </div>
                </div>

                {showSearch && (
                    <SearchPanel
                        onSearch={handleSearch}
                        onClearSearch={handleClearSearch}
                        onClose={() => setShowSearch(false)}
                        loading={searchLoading}
                        hasResult={searchResults.length > 0}
                    />
                )}

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
                    <div className="xl:col-span-2 space-y-4">
                        <div className="flex justify-between items-center">
                            <h2 className="text-lg font-semibold text-gray-900">
                                {hasSearched 
                                    ? `Search Results (${searchResults.length})` 
                                    : `Flags (${totalCount} total)`}
                            </h2>
                            {hasSearched && (
                                <button
                                    onClick={handleClearSearch}
                                    className="text-sm text-blue-600 hover:text-blue-800"
                                >
                                    Back to all flags
                                </button>
                            )}
                        </div>

                        <div className="space-y-4">
                            {hasSearched ? (
                                searchResults.length > 0 ? (
                                    searchResults.map((flag) => (
                                        <FlagCard
                                            key={flag.key}
                                            flag={flag}
                                            isSelected={selectedFlag?.key === flag.key}
                                            onClick={() => selectFlag(flag)}
                                            onDelete={(key) => setShowDeleteConfirm(key)}
                                        />
                                    ))
                                ) : (
                                    <div className="bg-white border border-gray-200 rounded-lg p-8 text-center">
                                        <Search className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                                        <h3 className="text-lg font-medium text-gray-900 mb-2">No Results Found</h3>
                                        <p className="text-gray-600 mb-4">
                                            No feature flags match your search criteria.
                                        </p>
                                        <button
                                            onClick={handleClearSearch}
                                            className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
                                        >
                                            Clear Search
                                        </button>
                                    </div>
                                )
                            ) : (
                                flags.map((flag) => (
                                    <FlagCard
                                        key={flag.key}
                                        flag={flag}
                                        isSelected={selectedFlag?.key === flag.key}
                                        onClick={() => selectFlag(flag)}
                                        onDelete={(key) => setShowDeleteConfirm(key)}
                                    />
                                ))
                            )}
                        </div>

                        {!hasSearched && (
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
                        )}
                    </div>

                    <div className="xl:col-span-3">
                        {selectedFlag ? (
                            <>
                                <h2 className="text-lg font-semibold text-gray-900 mb-4">Flag Details</h2>
                                <FlagDetails
                                    flag={selectedFlag}
                                    onToggle={quickToggle}
                                    onUpdateUserAccess={(allowedUsers, blockedUsers, percentage) => {
                                        const scopeHeaders = getScopeHeaders(selectedFlag);
                                        const request: ManageUserAccessRequest = {};
                                        if (allowedUsers !== undefined) request.allowed = allowedUsers;
                                        if (blockedUsers !== undefined) request.blocked = blockedUsers;
                                        if (percentage !== undefined) request.rolloutPercentage = percentage;
                                        return updateUserAccess(selectedFlag.key, request, scopeHeaders);
                                    }}
                                    onUpdateTenantAccess={(allowedTenants, blockedTenants, percentage) => {
                                        const scopeHeaders = getScopeHeaders(selectedFlag);
                                        const request: ManageTenantAccessRequest = {};
                                        if (allowedTenants !== undefined) request.allowed = allowedTenants;
                                        if (blockedTenants !== undefined) request.blocked = blockedTenants;
                                        if (percentage !== undefined) request.rolloutPercentage = percentage;
                                        return updateTenantAccess(selectedFlag.key, request, scopeHeaders);
                                    }}
                                    onUpdateTargetingRules={handleUpdateTargetingRulesWrapper}
                                    onUpdateVariations={handleUpdateVariations}
                                    onClearVariations={handleClearVariations}
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

                <CreateFlagModal
                    isOpen={showCreateForm}
                    onClose={() => setShowCreateForm(false)}
                    onSubmit={handleCreateFlag}
                />

                <DeleteConfirmationModal
                    isOpen={!!showDeleteConfirm}
                    flagKey={showDeleteConfirm || ''}
                    flagName={flags.find(f => f.key === showDeleteConfirm)?.name || searchResults.find(f => f.key === showDeleteConfirm)?.name || ''}
                    isDeleting={deletingFlag}
                    onConfirm={() => showDeleteConfirm && handleDeleteFlag(showDeleteConfirm)}
                    onCancel={() => setShowDeleteConfirm(null)}
                />
            </div>
        </div>
    );
};

export default FeatureFlagManager;