import { useState } from 'react';
import { X, Lock, Calendar } from 'lucide-react';
import type { CreateFeatureFlagRequest } from '../services/apiService';

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
        expirationDate: undefined,
        tags: {},
        isPermanent: false,
    });
    const [submitting, setSubmitting] = useState(false);

    const handleSubmit = async () => {
        try {
            setSubmitting(true);
            
            // Clean up the request - remove undefined values and empty strings
            const cleanedData: CreateFeatureFlagRequest = {
                key: formData.key,
                name: formData.name,
                ...(formData.description && { description: formData.description }),
                ...(formData.expirationDate && { expirationDate: formData.expirationDate }),
                ...(formData.tags && Object.keys(formData.tags).length > 0 && { tags: formData.tags }),
                ...(formData.isPermanent && { isPermanent: formData.isPermanent }),
            };

            await onSubmit(cleanedData);
            onClose();
            
            // Reset form
            setFormData({
                key: '',
                name: '',
                description: '',
                expirationDate: undefined,
                tags: {},
                isPermanent: false,
            });
        } catch (error) {
            console.error('Failed to create flag:', error);
        } finally {
            setSubmitting(false);
        }
    };

    const handleTagsChange = (tagsString: string) => {
        try {
            const tags: Record<string, string> = {};
            if (tagsString.trim()) {
                const tagPairs = tagsString.split(',').map(tag => tag.trim()).filter(tag => tag);
                tagPairs.forEach(tagPair => {
                    const [key, value] = tagPair.split(':').map(part => part.trim());
                    if (key) {
                        tags[key] = value || '';
                    }
                });
            }
            setFormData({ ...formData, tags });
        } catch (error) {
            console.error('Error parsing tags:', error);
        }
    };

    const getTagsString = (): string => {
        if (!formData.tags || Object.keys(formData.tags).length === 0) return '';
        return Object.entries(formData.tags)
            .map(([key, value]) => value ? `${key}:${value}` : key)
            .join(', ');
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
                <div className="flex justify-between items-center mb-4">
                    <h3 className="text-lg font-semibold">Create Feature Flag</h3>
                    <button 
                        onClick={onClose} 
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
                        <label className="block text-sm font-medium text-gray-700 mb-1 flex items-center gap-1">
                            <Calendar className="w-4 h-4" />
                            Expiration Date
                        </label>
                        <input
                            type="datetime-local"
                            value={formData.expirationDate || ''}
                            onChange={(e) => setFormData({ ...formData, expirationDate: e.target.value || undefined })}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                            disabled={submitting}
                            min={new Date().toISOString().slice(0, 16)}
                        />
                        <p className="text-xs text-gray-500 mt-1">Optional: When this flag should expire</p>
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Tags</label>
                        <input
                            type="text"
                            value={getTagsString()}
                            onChange={(e) => handleTagsChange(e.target.value)}
                            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                            placeholder="environment:prod, team:backend, priority:high"
                            disabled={submitting}
                        />
                        <p className="text-xs text-gray-500 mt-1">Optional: Comma-separated key:value pairs or just keys</p>
                    </div>

                    <div className="flex items-center gap-2">
                        <input
                            type="checkbox"
                            id="isPermanent"
                            checked={formData.isPermanent || false}
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
                        onClick={onClose}
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