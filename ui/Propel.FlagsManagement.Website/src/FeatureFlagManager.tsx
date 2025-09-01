import { useState } from 'react';
import { Calendar, Clock, Users, Percent, Settings, Eye, EyeOff, X, Plus, AlertCircle, Filter, Search, ChevronLeft, ChevronRight, Trash2, Lock, Shield } from 'lucide-react';
import { useFeatureFlags } from './hooks/useFeatureFlags';
import {
    type CreateFeatureFlagRequest,
    type FeatureFlagDto,
    type GetFlagsParams
} from './services/apiService';

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

    const getStatusColor = (status: string) => {
        switch (status) {
            case 'Enabled': return 'bg-green-100 text-green-800';
            case 'Disabled': return 'bg-red-100 text-red-800';
            case 'Scheduled': return 'bg-blue-100 text-blue-800';
            case 'Percentage': return 'bg-yellow-100 text-yellow-800';
            case 'UserTargeted': return 'bg-purple-100 text-purple-800';
            case 'TimeWindow': return 'bg-indigo-100 text-indigo-800';
            default: return 'bg-gray-100 text-gray-800';
        }
    };

    const getStatusIcon = (status: string) => {
        switch (status) {
            case 'Enabled': return <Eye className="w-4 h-4" />;
            case 'Disabled': return <EyeOff className="w-4 h-4" />;
            case 'Scheduled': return <Calendar className="w-4 h-4" />;
            case 'Percentage': return <Percent className="w-4 h-4" />;
            case 'UserTargeted': return <Users className="w-4 h-4" />;
            case 'TimeWindow': return <Clock className="w-4 h-4" />;
            default: return <Settings className="w-4 h-4" />;
        }
    };

    const quickToggle = async (flag: FeatureFlagDto) => {
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

    const handleSetPercentage = async (flag: FeatureFlagDto, percentage: number) => {
        try {
            await setPercentage(flag.key, percentage);
        } catch (error) {
            console.error('Failed to set percentage:', error);
        }
    };

    const handleScheduleFlag = async (flag: FeatureFlagDto, enableDate: string, disableDate?: string) => {
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

    const formatDate = (dateString?: string) => {
        if (!dateString) return 'Not set';
        return new Date(dateString).toLocaleString();
    };

    // Helper function to safely check if tags exist and have content
    const hasValidTags = (tags: Record<string, string> | undefined | null): boolean => {
        return tags != null && typeof tags === 'object' && Object.keys(tags).length > 0;
    };

    // Helper function to safely get tag entries
    const getTagEntries = (tags: Record<string, string> | undefined | null): [string, string][] => {
        if (!hasValidTags(tags)) return [];
        return Object.entries(tags!);
    };

    // Filtering functions
    const applyFilters = async () => {
        const params: GetFlagsParams = {
            page: 1, // Reset to first page when filtering
            pageSize: pageSize,
        };

        // Add status filter
        if (filters.status && filters.status !== '') {
            params.status = filters.status;
        }

        // Add tag filters
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

    const addTagFilter = () => {
        setFilters(prev => ({
            ...prev,
            tagKeys: [...prev.tagKeys, ''],
            tagValues: [...prev.tagValues, ''],
        }));
    };

    const removeTagFilter = (index: number) => {
        setFilters(prev => ({
            ...prev,
            tagKeys: prev.tagKeys.filter((_, i) => i !== index),
            tagValues: prev.tagValues.filter((_, i) => i !== index),
        }));
    };

    const updateTagKey = (index: number, value: string) => {
        setFilters(prev => ({
            ...prev,
            tagKeys: prev.tagKeys.map((key, i) => i === index ? value : key),
        }));
    };

    const updateTagValue = (index: number, value: string) => {
        setFilters(prev => ({
            ...prev,
            tagValues: prev.tagValues.map((val, i) => i === index ? value : val),
        }));
    };

    // Pagination functions
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

    const DeleteConfirmationModal = ({ flagKey, flagName }: { flagKey: string; flagName: string }) => {
        return (
            <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
                <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
                    <div className="flex items-center gap-3 mb-4">
                        <div className="flex-shrink-0 w-10 h-10 bg-red-100 rounded-full flex items-center justify-center">
                            <AlertCircle className="w-5 h-5 text-red-600" />
                        </div>
                        <div>
                            <h3 className="text-lg font-semibold text-gray-900">Delete Feature Flag</h3>
                            <p className="text-sm text-gray-600">This action cannot be undone</p>
                        </div>
                    </div>

                    <div className="mb-6">
                        <p className="text-gray-700">
                            Are you sure you want to delete the feature flag <strong>"{flagName}"</strong>?
                        </p>
                        <p className="text-sm text-gray-500 mt-2">
                            Key: <code className="bg-gray-100 px-1 py-0.5 rounded text-xs">{flagKey}</code>
                        </p>
                    </div>

                    <div className="flex gap-3">
                        <button
                            onClick={() => setShowDeleteConfirm(null)}
                            className="flex-1 px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50"
                            disabled={deletingFlag}
                        >
                            Cancel
                        </button>
                        <button
                            onClick={() => handleDeleteFlag(flagKey)}
                            disabled={deletingFlag}
                            className="flex-1 px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                        >
                            {deletingFlag ? (
                                <>
                                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                                    Deleting...
                                </>
                            ) : (
                                <>
                                    <Trash2 className="w-4 h-4" />
                                    Delete Flag
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </div>
        );
    };

    const CreateFlagForm = () => {
        const [formData, setFormData] = useState<CreateFeatureFlagRequest>({
            key: '',
            name: '',
            description: '',
            status: 'Disabled',
            percentageEnabled: 0,
            variations: { on: true, off: false },
            defaultVariation: 'off',
            tags: {},
            isPermanent: false,
        });
        const [submitting, setSubmitting] = useState(false);

        const handleSubmit = async () => {
            try {
                setSubmitting(true);
                await createFlag(formData);
                setShowCreateForm(false);
                setFormData({
                    key: '',
                    name: '',
                    description: '',
                    status: 'Disabled',
                    percentageEnabled: 0,
                    variations: { on: true, off: false },
                    defaultVariation: 'off',
                    tags: {},
                    isPermanent: false,
                });
            } catch (error) {
                console.error('Failed to create flag:', error);
            } finally {
                setSubmitting(false);
            }
        };

        return (
            <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
                <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
                    <div className="flex justify-between items-center mb-4">
                        <h3 className="text-lg font-semibold">Create Feature Flag</h3>
                        <button 
                            onClick={() => setShowCreateForm(false)} 
                            className="text-gray-400 hover:text-gray-600"
                            disabled={submitting}
                        >
                            <X className="w-5 h-5" />
                        </button>
                    </div>

                    <div className="space-y-4">
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">Key *</label>
                            <input
                                type="text"
                                value={formData.key}
                                onChange={(e) => setFormData({ ...formData, key: e.target.value })}
                                className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                                placeholder="feature-key-name"
                                pattern="[a-zA-Z0-9_-]+"
                                required
                                disabled={submitting}
                            />
                            <p className="text-xs text-gray-500 mt-1">Only letters, numbers, hyphens, and underscores allowed</p>
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">Name *</label>
                            <input
                                type="text"
                                value={formData.name}
                                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                                placeholder="Feature Display Name"
                                required
                                disabled={submitting}
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                            <textarea
                                value={formData.description || ''}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                                rows={3}
                                placeholder="Brief description of the feature..."
                                disabled={submitting}
                            />
                        </div>

                        <div className="flex items-center gap-2">
                            <input
                                type="checkbox"
                                id="isPermanent"
                                checked={formData.isPermanent}
                                onChange={(e) => setFormData({ ...formData, isPermanent: e.target.checked })}
                                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                disabled={submitting}
                            />
                            <label htmlFor="isPermanent" className="text-sm text-gray-700 flex items-center gap-1">
                                <Lock className="w-4 h-4 text-gray-500" />
                                Permanent flag (cannot be deleted)
                            </label>
                        </div>
                    </div>

                    <div className="flex gap-3 mt-6">
                        <button
                            onClick={() => setShowCreateForm(false)}
                            className="flex-1 px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50"
                            disabled={submitting}
                        >
                            Cancel
                        </button>
                        <button
                            onClick={handleSubmit}
                            disabled={submitting || !formData.key.trim() || !formData.name.trim()}
                            className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {submitting ? 'Creating...' : 'Create Flag'}
                        </button>
                    </div>
                </div>
            </div>
        );
    };

    const FilterPanel = () => {
        return (
            <div className="bg-white border border-gray-200 rounded-lg p-4 mb-4">
                <div className="flex justify-between items-center mb-4">
                    <h3 className="text-lg font-semibold text-gray-900">Filters</h3>
                    <button
                        onClick={() => setShowFilters(false)}
                        className="text-gray-400 hover:text-gray-600"
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                <div className="space-y-4">
                    {/* Status Filter */}
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-2">Status</label>
                        <select
                            value={filters.status}
                            onChange={(e) => setFilters(prev => ({ ...prev, status: e.target.value }))}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                        >
                            <option value="">All Statuses</option>
                            <option value="Enabled">Enabled</option>
                            <option value="Disabled">Disabled</option>
                            <option value="Scheduled">Scheduled</option>
                            <option value="Percentage">Percentage</option>
                            <option value="UserTargeted">User Targeted</option>
                            <option value="TimeWindow">Time Window</option>
                        </select>
                    </div>

                    {/* Tag Filters */}
                    <div>
                        <div className="flex justify-between items-center mb-2">
                            <label className="block text-sm font-medium text-gray-700">Tags</label>
                            <button
                                onClick={addTagFilter}
                                className="text-blue-600 hover:text-blue-800 text-sm"
                            >
                                + Add Tag Filter
                            </button>
                        </div>

                        <div className="space-y-2">
                            {filters.tagKeys.map((key, index) => (
                                <div key={index} className="flex gap-2 items-center">
                                    <input
                                        type="text"
                                        placeholder="Tag key"
                                        value={key}
                                        onChange={(e) => updateTagKey(index, e.target.value)}
                                        className="flex-1 border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                    <span className="text-gray-500">:</span>
                                    <input
                                        type="text"
                                        placeholder="Tag value (optional)"
                                        value={filters.tagValues[index] || ''}
                                        onChange={(e) => updateTagValue(index, e.target.value)}
                                        className="flex-1 border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                    {filters.tagKeys.length > 1 && (
                                        <button
                                            onClick={() => removeTagFilter(index)}
                                            className="text-red-500 hover:text-red-700"
                                        >
                                            <X className="w-4 h-4" />
                                        </button>
                                    )}
                                </div>
                            ))}
                        </div>
                        <p className="text-xs text-gray-500 mt-1">
                            Leave value empty to search by tag key only
                        </p>
                    </div>
                </div>

                <div className="flex gap-3 mt-6">
                    <button
                        onClick={clearFilters}
                        className="flex-1 px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50"
                    >
                        Clear Filters
                    </button>
                    <button
                        onClick={applyFilters}
                        className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
                    >
                        Apply Filters
                    </button>
                </div>
            </div>
        );
    };

    const PaginationControls = () => {
        const startItem = (currentPage - 1) * pageSize + 1;
        const endItem = Math.min(currentPage * pageSize, totalCount);

        const generatePageNumbers = () => {
            const pages = [];
            const maxVisiblePages = 5;
            let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
            let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

            // Adjust start page if we're near the end
            if (endPage - startPage + 1 < maxVisiblePages) {
                startPage = Math.max(1, endPage - maxVisiblePages + 1);
            }

            for (let i = startPage; i <= endPage; i++) {
                pages.push(i);
            }

            return pages;
        };

        if (totalPages <= 1) return null;

        return (
            <div className="flex items-center justify-between bg-white border border-gray-200 rounded-lg px-4 py-3">
                <div className="text-sm text-gray-700">
                    Showing <span className="font-medium">{startItem}</span> to{' '}
                    <span className="font-medium">{endItem}</span> of{' '}
                    <span className="font-medium">{totalCount}</span> results
                </div>

                <div className="flex items-center gap-2">
                    <button
                        onClick={goToPreviousPage}
                        disabled={!hasPreviousPage || loading}
                        className="flex items-center gap-1 px-3 py-1 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        <ChevronLeft className="w-4 h-4" />
                        Previous
                    </button>

                    <div className="flex gap-1">
                        {generatePageNumbers().map((page) => (
                            <button
                                key={page}
                                onClick={() => goToPage(page)}
                                disabled={loading}
                                className={`px-3 py-1 text-sm font-medium rounded-md ${
                                    page === currentPage
                                        ? 'bg-blue-600 text-white'
                                        : 'text-gray-700 hover:bg-gray-50 border border-gray-300'
                                } disabled:opacity-50 disabled:cursor-not-allowed`}
                            >
                                {page}
                            </button>
                        ))}
                    </div>

                    <button
                        onClick={goToNextPage}
                        disabled={!hasNextPage || loading}
                        className="flex items-center gap-1 px-3 py-1 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        Next
                        <ChevronRight className="w-4 h-4" />
                    </button>
                </div>
            </div>
        );
    };

    const FlagDetails = ({ flag }: { flag: FeatureFlagDto }) => {
        const [editingPercentage, setEditingPercentage] = useState(false);
        const [newPercentage, setNewPercentage] = useState(flag.percentageEnabled || 0);
        const [editingSchedule, setEditingSchedule] = useState(false);
        const [scheduleData, setScheduleData] = useState({
            enableDate: flag.scheduledEnableDate ? flag.scheduledEnableDate.slice(0, 16) : '',
            disableDate: flag.scheduledDisableDate ? flag.scheduledDisableDate.slice(0, 16) : ''
        });
        const [operationLoading, setOperationLoading] = useState(false);

        const handlePercentageSubmit = async () => {
            try {
                setOperationLoading(true);
                await handleSetPercentage(flag, newPercentage);
                setEditingPercentage(false);
            } catch (error) {
                console.error('Failed to set percentage:', error);
            } finally {
                setOperationLoading(false);
            }
        };

        const handleScheduleSubmit = async () => {
            try {
                setOperationLoading(true);
                await handleScheduleFlag(
                    flag,
                    scheduleData.enableDate ? new Date(scheduleData.enableDate).toISOString() : '',
                    scheduleData.disableDate ? new Date(scheduleData.disableDate).toISOString() : undefined
                );
                setEditingSchedule(false);
            } catch (error) {
                console.error('Failed to schedule flag:', error);
            } finally {
                setOperationLoading(false);
            }
        };

        return (
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <div className="flex justify-between items-start mb-4">
                    <div className="flex-1">
                        <div className="flex items-center gap-2 mb-2">
                            <h3 className="text-lg font-semibold text-gray-900">{flag.name}</h3>
                            {flag.isPermanent && (
                                <div className="flex items-center gap-1 px-1.5 py-0.5 bg-amber-100 text-amber-700 rounded text-xs">
                                    <Lock className="w-3 h-3" />
                                    <span className="font-medium">PERM</span>
                                </div>
                            )}
                        </div>
                        <p className="text-sm text-gray-500 font-mono">{flag.key}</p>
                    </div>
                    <div className="flex items-center gap-2">
                        <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(flag.status)}`}>
                            {getStatusIcon(flag.status)}
                            {flag.status}
                        </span>
                        {!flag.isPermanent && (
                            <button
                                onClick={() => setShowDeleteConfirm(flag.key)}
                                className="p-1.5 text-red-600 hover:bg-red-50 rounded-md transition-colors"
                                title="Delete Flag"
                            >
                                <Trash2 className="w-4 h-4" />
                            </button>
                        )}
                    </div>
                </div>

                <p className="text-gray-600 mb-6">{flag.description || 'No description provided'}</p>

                {/* Quick Actions */}
                <div className="grid grid-cols-2 gap-3 mb-6">
                    <button
                        onClick={() => quickToggle(flag)}
                        disabled={operationLoading}
                        className={`flex items-center justify-center gap-2 px-4 py-2 rounded-md font-medium disabled:opacity-50 ${
                            flag.status === 'Enabled'
                                ? 'bg-red-100 text-red-700 hover:bg-red-200'
                                : 'bg-green-100 text-green-700 hover:bg-green-200'
                        }`}
                    >
                        {flag.status === 'Enabled' ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                        {flag.status === 'Enabled' ? 'Disable' : 'Enable'}
                    </button>

                    <button
                        onClick={() => setEditingPercentage(true)}
                        disabled={operationLoading}
                        className="flex items-center justify-center gap-2 px-4 py-2 bg-yellow-100 text-yellow-700 rounded-md hover:bg-yellow-200 font-medium disabled:opacity-50"
                    >
                        <Percent className="w-4 h-4" />
                        Percentage Rollout
                    </button>
                </div>

                {/* Permanent Flag Warning */}
                {flag.isPermanent && (
                    <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded-lg">
                        <div className="flex items-center gap-2 text-amber-800 text-sm">
                            <Lock className="w-4 h-4" />
                            <span className="font-medium">This is a permanent feature flag</span>
                        </div>
                        <p className="text-amber-700 text-xs mt-1">
                            Permanent flags cannot be deleted and are intended for long-term use in production systems.
                        </p>
                    </div>
                )}

                {/* Percentage Editing */}
                {editingPercentage && (
                    <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 mb-4">
                        <h4 className="font-medium text-yellow-800 mb-2">Set Percentage Rollout</h4>
                        <div className="flex items-center gap-3">
                            <input
                                type="range"
                                min="0"
                                max="100"
                                value={newPercentage}
                                onChange={(e) => setNewPercentage(parseInt(e.target.value))}
                                className="flex-1"
                                disabled={operationLoading}
                            />
                            <span className="text-sm font-medium text-yellow-800 min-w-[3rem]">{newPercentage}%</span>
                        </div>
                        <div className="flex gap-2 mt-3">
                            <button
                                onClick={handlePercentageSubmit}
                                disabled={operationLoading}
                                className="px-3 py-1 bg-yellow-600 text-white rounded text-sm hover:bg-yellow-700 disabled:opacity-50"
                            >
                                {operationLoading ? 'Applying...' : 'Apply'}
                            </button>
                            <button
                                onClick={() => {
                                    setEditingPercentage(false);
                                    setNewPercentage(flag.percentageEnabled || 0);
                                }}
                                disabled={operationLoading}
                                className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                )}

                {/* Schedule Section */}
                <div className="space-y-4">
                    <div className="flex justify-between items-center">
                        <h4 className="font-medium text-gray-900">Scheduling</h4>
                        <button
                            onClick={() => setEditingSchedule(true)}
                            disabled={operationLoading}
                            className="text-blue-600 hover:text-blue-800 text-sm flex items-center gap-1 disabled:opacity-50"
                        >
                            <Calendar className="w-4 h-4" />
                            Schedule
                        </button>
                    </div>

                    {editingSchedule ? (
                        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                            <div className="space-y-3">
                                <div>
                                    <label className="block text-sm font-medium text-blue-800 mb-1">Enable Date</label>
                                    <input
                                        type="datetime-local"
                                        value={scheduleData.enableDate}
                                        onChange={(e) => setScheduleData({ ...scheduleData, enableDate: e.target.value })}
                                        className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
                                        disabled={operationLoading}
                                    />
                                </div>
                                <div>
                                    <label className="block text-sm font-medium text-blue-800 mb-1">Disable Date (Optional)</label>
                                    <input
                                        type="datetime-local"
                                        value={scheduleData.disableDate}
                                        onChange={(e) => setScheduleData({ ...scheduleData, disableDate: e.target.value })}
                                        className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
                                        disabled={operationLoading}
                                    />
                                </div>
                            </div>
                            <div className="flex gap-2 mt-3">
                                <button
                                    onClick={handleScheduleSubmit}
                                    disabled={operationLoading || !scheduleData.enableDate}
                                    className="px-3 py-1 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
                                >
                                    {operationLoading ? 'Scheduling...' : 'Schedule'}
                                </button>
                                <button
                                    onClick={() => setEditingSchedule(false)}
                                    disabled={operationLoading}
                                    className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                                >
                                    Cancel
                                </button>
                            </div>
                        </div>
                    ) : (
                        <div className="text-sm text-gray-600 space-y-1">
                            <div>Enable: {formatDate(flag.scheduledEnableDate)}</div>
                            <div>Disable: {formatDate(flag.scheduledDisableDate)}</div>
                        </div>
                    )}
                </div>

                {/* Current Status */}
                {flag.status === 'Percentage' && (
                    <div className="mt-4 p-3 bg-yellow-50 rounded-lg">
                        <div className="text-sm text-yellow-800">
                            Currently enabled for <strong>{flag.percentageEnabled || 0}%</strong> of users
                        </div>
                    </div>
                )}

                {/* User Lists */}
                {((flag.enabledUsers && flag.enabledUsers.length > 0) || (flag.disabledUsers && flag.disabledUsers.length > 0)) && (
                    <div className="mt-4 space-y-2">
                        {flag.enabledUsers && flag.enabledUsers.length > 0 && (
                            <div className="text-sm">
                                <span className="font-medium text-green-700">Enabled for: </span>
                                <span className="text-gray-600">{flag.enabledUsers.join(', ')}</span>
                            </div>
                        )}
                        {flag.disabledUsers && flag.disabledUsers.length > 0 && (
                            <div className="text-sm">
                                <span className="font-medium text-red-700">Disabled for: </span>
                                <span className="text-gray-600">{flag.disabledUsers.join(', ')}</span>
                            </div>
                        )}
                    </div>
                )}

                {/* Metadata */}
                <div className="mt-6 pt-4 border-t border-gray-200 text-xs text-gray-500 space-y-1">
                    <div>Created by {flag.createdBy} on {formatDate(flag.createdAt)}</div>
                    <div>Last updated by {flag.updatedBy} on {formatDate(flag.updatedAt)}</div>
                    {hasValidTags(flag.tags) && (
                        <div className="flex flex-wrap gap-1 mt-2">
                            {getTagEntries(flag.tags).map(([key, value]) => (
                                <span key={key} className="bg-gray-100 text-gray-700 px-2 py-1 rounded text-xs">
                                    {key}: {value}
                                </span>
                            ))}
                        </div>
                    )}
                </div>
            </div>
        );
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
            {showFilters && <FilterPanel />}

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
                            <div
                                key={flag.key}
                                onClick={() => selectFlag(flag)}
                                className={`bg-white border rounded-lg p-4 cursor-pointer transition-all ${
                                    selectedFlag?.key === flag.key
                                        ? 'border-blue-500 ring-2 ring-blue-200'
                                        : 'border-gray-200 hover:border-gray-300'
                                }`}
                            >
                                <div className="flex justify-between items-start">
                                    <div className="flex-1">
                                        <div className="flex items-center gap-2 mb-1">
                                            <h3 className="font-medium text-gray-900">{flag.name}</h3>
                                            {flag.isPermanent && (
                                                <div className="flex items-center gap-1 px-1.5 py-0.5 bg-amber-100 text-amber-700 rounded text-xs">
                                                    <Lock className="w-3 h-3" />
                                                    <span className="font-medium">PERM</span>
                                                </div>
                                            )}
                                        </div>
                                        <p className="text-sm text-gray-500 font-mono">{flag.key}</p>
                                        <p className="text-sm text-gray-600 mt-1 line-clamp-2">{flag.description || 'No description'}</p>
                                    </div>

                                    <div className="flex flex-col items-end gap-2">
                                        <div className="flex items-center gap-2">
                                            <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${getStatusColor(flag.status)}`}>
                                                {getStatusIcon(flag.status)}
                                                {flag.status}
                                            </span>
                                            {!flag.isPermanent && (
                                                <button
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        setShowDeleteConfirm(flag.key);
                                                    }}
                                                    className="p-1 text-red-600 hover:bg-red-50 rounded transition-colors"
                                                    title="Delete Flag"
                                                >
                                                    <Trash2 className="w-3 h-3" />
                                                </button>
                                            )}
                                        </div>

                                        {flag.status === 'Percentage' && (
                                            <span className="text-xs text-gray-500">{flag.percentageEnabled || 0}%</span>
                                        )}
                                    </div>
                                </div>

                                {hasValidTags(flag.tags) && (
                                    <div className="flex flex-wrap gap-1 mt-2">
                                        {getTagEntries(flag.tags).slice(0, 3).map(([key, value]) => (
                                            <span key={key} className="bg-gray-100 text-gray-600 px-2 py-1 rounded text-xs">
                                                {key}: {value}
                                            </span>
                                        ))}
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>

                    {/* Pagination Controls */}
                    <PaginationControls />
                </div>

                {/* Flag Details */}
                <div>
                    {selectedFlag ? (
                        <>
                            <h2 className="text-lg font-semibold text-gray-900 mb-4">Flag Details</h2>
                            <FlagDetails flag={selectedFlag} />
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

            {showCreateForm && <CreateFlagForm />}
            {showDeleteConfirm && (
                <DeleteConfirmationModal 
                    flagKey={showDeleteConfirm} 
                    flagName={flags.find(f => f.key === showDeleteConfirm)?.name || showDeleteConfirm}
                />
            )}
        </div>
    );
};

export default FeatureFlagManager;