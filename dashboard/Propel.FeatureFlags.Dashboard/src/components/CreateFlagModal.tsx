import { useState } from 'react';
import { X, AlertCircle } from 'lucide-react';
import type { CreateFeatureFlagRequest } from '../services/apiService';
import { ApiError } from '../services/apiService';

interface CreateFlagModalProps {
    isOpen: boolean;
    onClose: () => void;
    onSubmit: (flag: CreateFeatureFlagRequest) => Promise<void>;
}

export const CreateFlagModal: React.FC<CreateFlagModalProps> = ({
    isOpen,
    onClose,
    onSubmit
}) => {
    const [formData, setFormData] = useState<CreateFeatureFlagRequest>({
        key: '',
        name: '',
        description: '',
        tags: {},
    });
    const [tagsInput, setTagsInput] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleSubmit = async () => {
        try {
            setSubmitting(true);
            setError(null);

            const cleanedData: CreateFeatureFlagRequest = {
                key: formData.key,
                name: formData.name,
                ...(formData.description && { description: formData.description }),
                ...(formData.tags && Object.keys(formData.tags).length > 0 && { tags: formData.tags }),
            };

            await onSubmit(cleanedData);
            
            // Reset form on success
            setFormData({
                key: '',
                name: '',
                description: '',
                tags: {},
            });
            setTagsInput('');
            onClose();
        } catch (err: any) {
            console.error('Failed to create flag:', err);
            
            // Extract detailed error message
            let errorMessage = 'Failed to create flag. Please try again.';
            
            if (err instanceof ApiError) {
                errorMessage = err.detail || err.message;
            } else if (err?.message) {
                errorMessage = err.message;
            }
            
            setError(errorMessage);
        } finally {
            setSubmitting(false);
        }
    };

    const handleClose = () => {
        if (!submitting) {
            setError(null);
            setFormData({
                key: '',
                name: '',
                description: '',
                tags: {},
            });
            setTagsInput('');
            onClose();
        }
    };

    const handleTagsChange = (value: string) => {
        setTagsInput(value);

        try {
            const tags: Record<string, string> = {};
            if (value.trim()) {
                const tagPairs = value.split(',').map(tag => tag.trim()).filter(tag => tag);

                tagPairs.forEach(tagPair => {
                    const colonIndex = tagPair.indexOf(':');

                    if (colonIndex > 0) {
                        const key = tagPair.substring(0, colonIndex).trim();
                        const tagValue = tagPair.substring(colonIndex + 1).trim();
                        if (key) {
                            tags[key] = tagValue || '';
                        }
                    } else {
                        const key = tagPair.trim();
                        if (key) {
                            tags[key] = '';
                        }
                    }
                });
            }
            setFormData({ ...formData, tags });
        } catch (error) {
            console.error('Error parsing tags:', error);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
                <div className="flex justify-between items-center mb-4">
                    <h3 className="text-lg font-semibold">Create Feature Flag</h3>
                    <button
                        onClick={handleClose}
                        className="text-gray-400 hover:text-gray-600"
                        disabled={submitting}
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                {error && (
                    <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg flex items-start gap-2">
                        <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
                        <div className="flex-1">
                            <p className="text-sm text-red-800">{error}</p>
                        </div>
                        <button
                            onClick={() => setError(null)}
                            className="text-red-400 hover:text-red-600"
                        >
                            <X className="w-4 h-4" />
                        </button>
                    </div>
                )}

                <div className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Key *</label>
                        <input
                            type="text"
                            value={formData.key}
                            onChange={(e) => setFormData({ ...formData, key: e.target.value })}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                            placeholder="feature-key-name"
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

                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Tags</label>
                        <input
                            type="text"
                            value={tagsInput}
                            onChange={(e) => handleTagsChange(e.target.value)}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                            placeholder="environment:prod, team:backend, priority:high"
                            disabled={submitting}
                        />
                        <p className="text-xs text-gray-500 mt-1">Comma-separated key:value pairs (e.g., env:prod, team:backend)</p>
                    </div>

                    <div className="bg-blue-50 border border-blue-200 rounded-md p-3">
                        <p className="text-sm text-blue-800">
                            <strong>Note:</strong> Only global flags can be created from this dashboard. Application-specific flags must be created directly from your application code. New flags are created as disabled and permanent by default. You can modify these settings and add configurations after creation.
                        </p>
                    </div>
                </div>

                <div className="flex gap-3 mt-6">
                    <button
                        onClick={handleClose}
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