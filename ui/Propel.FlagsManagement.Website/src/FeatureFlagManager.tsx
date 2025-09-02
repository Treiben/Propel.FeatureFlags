import { useState } from 'react';
import { AlertCircle, Filter, Plus, Settings, X } from 'lucide-react';
import { useFeatureFlags } from './hooks/useFeatureFlags';
import type { CreateFeatureFlagRequest, GetFlagsParams } from './services/apiService';

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
        selectFlag,
        createFlag,
        enableFlag,
        disableFlag,
        scheduleFlag,
        setPercentage,
        loadFlagsPage,
        filterFlags,
        deleteFlag,
        clearError,
    } = useFeatureFlags();

    const [showCreateForm, setShowCreateForm] = useState(false);
    const [showFilters, setShowFilters] = useState(false);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState<string | null>(null);
    const [deletingFlag, setDeletingFlag] = useState(false);
    const [filters, setFilters] = useState<{
        status: string;
        tagKeys: string[];
        tagValues: string[];
    }>({
        status: '',
        tagKeys: [''],
        tagValues: [''],
    });

    // Handler functions
    const quickToggle = async (flag: any) => {
        try {
            const reason = `Quick toggle via UI by user`;
            if (flag.status === 'Enabled') {
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
            await setPercentage(flag.key, percentage);
        } catch (error) {
            console.error('Failed to set percentage:', error);
        }
    };

    const handleScheduleFlag = async (flag: any, enableDate: string, disableDate?: string) => {
        try {
            await scheduleFlag(flag.key, enableDate, disableDate);
        } catch (error) {
            console.error('Failed to schedule flag:', error);
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

    // Wrapper function to match CreateFlagModal's expected signature
    const handleCreateFlag = async (request: CreateFeatureFlagRequest): Promise<void> => {
        await createFlag(request);
    };

    const applyFilters = async () => {
        const params: GetFlagsParams = {
            page: 1,
            pageSize: pageSize,
        };

        if (filters.status && filters.status !== '') {
            params.status = filters.status;
        }

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
            status: '',
            tagKeys: [''],
            tagValues: [''],
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
        <div className="max-w-7xl mx-auto p-6 bg-gray-50 min-h-screen">
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

            <div className="flex justify-between items-center mb-6">
                <div>
                    <h1 className="text-2xl font-bold text-gray-900">Feature Flags</h1>
                    <p className="text-gray-600">Manage feature releases and rollouts</p>
                </div>
                <div className="flex gap-3">
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

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Flag List */}
                <div className="space-y-4">
                    <div className="flex justify-between items-center">
                        <h2 className="text-lg font-semibold text-gray-900">
                            Flags ({totalCount} total)
                        </h2>
                    </div>

                    <div className="space-y-3">
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

                {/* Flag Details */}
                <div>
                    {selectedFlag ? (
                        <>
                            <h2 className="text-lg font-semibold text-gray-900 mb-4">Flag Details</h2>
                            <FlagDetails
                                flag={selectedFlag}
                                onToggle={quickToggle}
                                onSetPercentage={handleSetPercentage}
                                onSchedule={handleScheduleFlag}
                                onDelete={(key) => setShowDeleteConfirm(key)}
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