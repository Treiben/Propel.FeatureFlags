import { useState } from 'react';
import { Search, X } from 'lucide-react';
import type { SearchFeatureFlagRequest } from '../services/apiService';

interface SearchPanelProps {
    onSearch: (request: SearchFeatureFlagRequest) => Promise<void>;
    onClearSearch: () => void;
    onClose: () => void;
    loading: boolean;
    hasResult: boolean;
}

export const SearchPanel: React.FC<SearchPanelProps> = ({
    onSearch,
    onClearSearch,
    onClose,
    loading,
    hasResult
}) => {
    const [searchParams, setSearchParams] = useState<SearchFeatureFlagRequest>({
        key: '',
        name: '',
        description: ''
    });

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        // Check if at least one field is filled
        const hasSearchCriteria = searchParams.key || searchParams.name || searchParams.description;
        if (!hasSearchCriteria) return;

        // Create request with only filled fields
        const request: SearchFeatureFlagRequest = {};
        if (searchParams.key?.trim()) request.key = searchParams.key.trim();
        if (searchParams.name?.trim()) request.name = searchParams.name.trim();
        if (searchParams.description?.trim()) request.description = searchParams.description.trim();

        await onSearch(request);
    };

    const handleClear = () => {
        setSearchParams({
            key: '',
            name: '',
            description: ''
        });
        onClearSearch();
    };

    const hasSearchCriteria = searchParams.key || searchParams.name || searchParams.description;

    return (
        <div className="bg-white border border-gray-200 rounded-lg p-4 mb-4">
            <div className="flex justify-between items-center mb-4">
                <h3 className="text-lg font-semibold text-gray-900 flex items-center gap-2">
                    <Search className="w-5 h-5" />
                    Search Flags
                </h3>
                <button
                    onClick={onClose}
                    className="text-gray-400 hover:text-gray-600 transition-colors"
                    title="Close search"
                >
                    <X className="w-5 h-5" />
                </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Flag Key
                        </label>
                        <input
                            type="text"
                            placeholder="e.g. new-checkout-flow"
                            value={searchParams.key || ''}
                            onChange={(e) => setSearchParams(prev => ({ ...prev, key: e.target.value }))}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Flag Name
                        </label>
                        <input
                            type="text"
                            placeholder="e.g. New Checkout Flow"
                            value={searchParams.name || ''}
                            onChange={(e) => setSearchParams(prev => ({ ...prev, name: e.target.value }))}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Description
                        </label>
                        <input
                            type="text"
                            placeholder="e.g. Enhanced checkout"
                            value={searchParams.description || ''}
                            onChange={(e) => setSearchParams(prev => ({ ...prev, description: e.target.value }))}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                        />
                    </div>
                </div>

                <div className="flex justify-between items-center">
                    <p className="text-sm text-gray-500">
                        Enter at least one search criteria to find a flag
                    </p>
                    <div className="flex gap-3">
                        {hasResult && (
                            <button
                                type="button"
                                onClick={handleClear}
                                className="px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50 transition-colors"
                            >
                                Clear Results
                            </button>
                        )}
                        <button
                            type="submit"
                            disabled={!hasSearchCriteria || loading}
                            className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 transition-colors"
                        >
                            {loading ? (
                                <>
                                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                                    Searching...
                                </>
                            ) : (
                                <>
                                    <Search className="w-4 h-4" />
                                    Search
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </form>
        </div>
    );
};