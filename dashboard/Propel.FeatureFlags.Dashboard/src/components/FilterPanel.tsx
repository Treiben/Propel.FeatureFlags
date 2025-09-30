import { X } from 'lucide-react';
import { getEvaluationModes } from '../services/apiService';

interface FilterState {
    modes: number[];
    tagKeys: string[];
    tagValues: string[];
    expiringInDays?: number;
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
    const evaluationModes = getEvaluationModes();

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

    const toggleMode = (mode: number) => {
        const newModes = filters.modes.includes(mode)
            ? filters.modes.filter(m => m !== mode)
            : [...filters.modes, mode];
        
        onFiltersChange({
            ...filters,
            modes: newModes,
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
                {/* Evaluation Modes Filter */}
                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">Evaluation Modes</label>
                    <div className="grid grid-cols-2 gap-2">
                        {evaluationModes.map((mode) => (
                            <label key={mode.value} className="flex items-center gap-2 text-sm">
                                <input
                                    type="checkbox"
                                    checked={filters.modes.includes(mode.value)}
                                    onChange={() => toggleMode(mode.value)}
                                    className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                />
                                <span className="text-gray-700">{mode.label}</span>
                            </label>
                        ))}
                    </div>
                </div>

                {/* Expiring In Days Filter */}
                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">Expiring In Days</label>
                    <input
                        type="number"
                        min="1"
                        max="365"
                        placeholder="Days (1-365)"
                        value={filters.expiringInDays || ''}
                        onChange={(e) => onFiltersChange({ 
                            ...filters, 
                            expiringInDays: e.target.value ? parseInt(e.target.value) : undefined 
                        })}
                        className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                    <p className="text-xs text-gray-500 mt-1">
                        Filter flags expiring within specified days
                    </p>
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