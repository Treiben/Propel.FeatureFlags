import { X } from 'lucide-react';

interface FilterState {
    status: string;
    tagKeys: string[];
    tagValues: string[];
}

interface FilterPanelProps {
    filters: FilterState;
    onFiltersChange: (filters: FilterState) => void;
    onApplyFilters: () => void;
    onClearFilters: () => void;
    onClose: () => void;
}

export const FilterPanel: React.FC<FilterPanelProps> = ({
    filters,
    onFiltersChange,
    onApplyFilters,
    onClearFilters,
    onClose
}) => {
    const addTagFilter = () => {
        onFiltersChange({
            ...filters,
            tagKeys: [...filters.tagKeys, ''],
            tagValues: [...filters.tagValues, ''],
        });
    };

    const removeTagFilter = (index: number) => {
        onFiltersChange({
            ...filters,
            tagKeys: filters.tagKeys.filter((_, i) => i !== index),
            tagValues: filters.tagValues.filter((_, i) => i !== index),
        });
    };

    const updateTagKey = (index: number, value: string) => {
        onFiltersChange({
            ...filters,
            tagKeys: filters.tagKeys.map((key, i) => i === index ? value : key),
        });
    };

    const updateTagValue = (index: number, value: string) => {
        onFiltersChange({
            ...filters,
            tagValues: filters.tagValues.map((val, i) => i === index ? value : val),
        });
    };

    return (
        <div className="bg-white border border-gray-200 rounded-lg p-4 mb-4">
            <div className="flex justify-between items-center mb-4">
                <h3 className="text-lg font-semibold text-gray-900">Filters</h3>
                <button
                    onClick={onClose}
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
                        onChange={(e) => onFiltersChange({ ...filters, status: e.target.value })}
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
                    onClick={onClearFilters}
                    className="flex-1 px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50"
                >
                    Clear Filters
                </button>
                <button
                    onClick={onApplyFilters}
                    className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
                >
                    Apply Filters
                </button>
            </div>
        </div>
    );
};