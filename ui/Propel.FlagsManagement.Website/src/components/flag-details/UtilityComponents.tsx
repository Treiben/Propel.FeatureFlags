import { useState, useEffect } from 'react';
import { Lock, AlertCircle, Edit3, Calendar, FileText } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { isExpired, formatDate, hasValidTags, getTagEntries } from '../../utils/flagHelpers';

interface ExpirationWarningProps {
    flag: FeatureFlagDto;
}

export const ExpirationWarning: React.FC<ExpirationWarningProps> = ({ flag }) => {
    const flagExpired = isExpired(flag);

    if (!flag.expirationDate) return null;

    return (
        <div className={`mb-4 p-3 rounded-lg border ${
            flagExpired 
                ? 'bg-red-50 border-red-200' 
                : 'bg-orange-50 border-orange-200'
        }`}>
            <div className="flex items-center gap-2 mb-1">
                <AlertCircle className={`w-4 h-4 ${flagExpired ? 'text-red-600' : 'text-orange-600'}`} />
                <span className={`font-medium ${flagExpired ? 'text-red-800' : 'text-orange-800'}`}>
                    {flagExpired ? 'Flag Expired' : 'Expiration Set'}
                </span>
            </div>
            <p className={`text-sm ${flagExpired ? 'text-red-700' : 'text-orange-700'}`}>
                {flagExpired 
                    ? `Expired on ${formatDate(flag.expirationDate)}`
                    : `Will expire on ${formatDate(flag.expirationDate)}`
                }
            </p>
        </div>
    );
};

interface PermanentFlagWarningProps {
    flag: FeatureFlagDto;
}

export const PermanentFlagWarning: React.FC<PermanentFlagWarningProps> = ({ flag }) => {
    if (!flag.isPermanent) return null;

    return (
        <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded-lg">
            <div className="flex items-center gap-2 text-amber-800 text-sm">
                <Lock className="w-4 h-4" />
                <span className="font-medium">This is a permanent feature flag</span>
            </div>
            <p className="text-amber-700 text-xs mt-1">
                Permanent flags cannot be deleted and are intended for long-term use in production systems.
            </p>
        </div>
    );
};

interface UserListsProps {
    flag: FeatureFlagDto;
}

export const UserLists: React.FC<UserListsProps> = ({ flag }) => {
    const hasEnabledUsers = flag.enabledUsers && flag.enabledUsers.length > 0;
    const hasDisabledUsers = flag.disabledUsers && flag.disabledUsers.length > 0;

    if (!hasEnabledUsers && !hasDisabledUsers) return null;

    return (
        <div className="mt-4 space-y-2">
            {hasEnabledUsers && (
                <div className="text-sm">
                    <span className="font-medium text-green-700">Enabled for: </span>
                    <span className="text-gray-600">{flag.enabledUsers!.join(', ')}</span>
                </div>
            )}
            {hasDisabledUsers && (
                <div className="text-sm">
                    <span className="font-medium text-red-700">Disabled for: </span>
                    <span className="text-gray-600">{flag.disabledUsers!.join(', ')}</span>
                </div>
            )}
        </div>
    );
};

interface FlagMetadataProps {
    flag: FeatureFlagDto;
}

export const FlagMetadata: React.FC<FlagMetadataProps> = ({ flag }) => {
    return (
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
    );
};

interface FlagEditSectionProps {
    flag: FeatureFlagDto;
    onUpdateFlag: (updates: {
        name?: string;
        description?: string;
        expirationDate?: string;
        isPermanent?: boolean;
        tags?: Record<string, string>;
    }) => Promise<void>;
    operationLoading: boolean;
}

export const FlagEditSection: React.FC<FlagEditSectionProps> = ({
    flag,
    onUpdateFlag,
    operationLoading
}) => {
    const [editing, setEditing] = useState(false);
    const [formData, setFormData] = useState({
        name: flag.name,
        description: flag.description || '',
        expirationDate: flag.expirationDate ? flag.expirationDate.slice(0, 16) : '',
        isPermanent: flag.isPermanent
    });

    // Update local state when flag changes (when a different flag is selected)
    useEffect(() => {
        setFormData({
            name: flag.name,
            description: flag.description || '',
            expirationDate: flag.expirationDate ? flag.expirationDate.slice(0, 16) : '',
            isPermanent: flag.isPermanent
        });
    }, [flag.key, flag.name, flag.description, flag.expirationDate, flag.isPermanent]);

    const handleSubmit = async () => {
        try {
            const updates: any = {};
            
            if (formData.name !== flag.name) {
                updates.name = formData.name;
            }
            
            if (formData.description !== (flag.description || '')) {
                updates.description = formData.description;
            }
            
            if (formData.expirationDate !== (flag.expirationDate ? flag.expirationDate.slice(0, 16) : '')) {
                updates.expirationDate = formData.expirationDate ? new Date(formData.expirationDate).toISOString() : null;
            }
            
            if (formData.isPermanent !== flag.isPermanent) {
                updates.isPermanent = formData.isPermanent;
            }

            await onUpdateFlag(updates);
            setEditing(false);
        } catch (error) {
            console.error('Failed to update flag:', error);
        }
    };

    const handleCancel = () => {
        setFormData({
            name: flag.name,
            description: flag.description || '',
            expirationDate: flag.expirationDate ? flag.expirationDate.slice(0, 16) : '',
            isPermanent: flag.isPermanent
        });
        setEditing(false);
    };

    return (
        <div className="space-y-4 mb-6">
            <div className="flex justify-between items-center">
                <h4 className="font-medium text-gray-900">Flag Details</h4>
                <button
                    onClick={() => setEditing(true)}
                    disabled={operationLoading}
                    className="text-gray-600 hover:text-gray-800 text-sm flex items-center gap-1 disabled:opacity-50"
                >
                    <Edit3 className="w-4 h-4" />
                    Edit
                </button>
            </div>

            {editing ? (
                <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
                    <div className="space-y-3">
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                <FileText className="w-4 h-4 inline mr-1" />
                                Name
                            </label>
                            <input
                                type="text"
                                value={formData.name}
                                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                className="w-full border border-gray-300 rounded px-3 py-2 text-sm"
                                disabled={operationLoading}
                                maxLength={200}
                            />
                        </div>
                        
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                <FileText className="w-4 h-4 inline mr-1" />
                                Description
                            </label>
                            <textarea
                                value={formData.description}
                                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                className="w-full border border-gray-300 rounded px-3 py-2 text-sm"
                                rows={3}
                                disabled={operationLoading}
                                maxLength={1000}
                                placeholder="Enter flag description..."
                            />
                        </div>
                        
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                <Calendar className="w-4 h-4 inline mr-1" />
                                Expiration Date (Optional)
                            </label>
                            <input
                                type="datetime-local"
                                value={formData.expirationDate}
                                onChange={(e) => setFormData({ ...formData, expirationDate: e.target.value })}
                                className="w-full border border-gray-300 rounded px-3 py-2 text-sm"
                                disabled={operationLoading}
                            />
                            <p className="text-xs text-gray-500 mt-1">Leave empty for no expiration</p>
                        </div>
                        
                        <div className="flex items-center">
                            <input
                                type="checkbox"
                                id="isPermanent"
                                checked={formData.isPermanent}
                                onChange={(e) => setFormData({ ...formData, isPermanent: e.target.checked })}
                                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                disabled={operationLoading}
                            />
                            <label htmlFor="isPermanent" className="ml-2 text-sm text-gray-700">
                                <Lock className="w-4 h-4 inline mr-1" />
                                Mark as permanent flag
                            </label>
                        </div>
                    </div>
                    
                    <div className="flex gap-2 mt-4">
                        <button
                            onClick={handleSubmit}
                            disabled={operationLoading || !formData.name.trim()}
                            className="px-3 py-1 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
                        >
                            {operationLoading ? 'Saving...' : 'Save Changes'}
                        </button>
                        <button
                            onClick={handleCancel}
                            disabled={operationLoading}
                            className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                        >
                            Cancel
                        </button>
                    </div>
                </div>
            ) : (
                <div className="text-sm text-gray-600 space-y-1">
                    <div><strong>Name:</strong> {flag.name}</div>
                    <div><strong>Description:</strong> {flag.description || 'No description'}</div>
                    <div><strong>Expiration:</strong> {formatDate(flag.expirationDate)}</div>
                    <div><strong>Permanent:</strong> {flag.isPermanent ? 'Yes' : 'No'}</div>
                </div>
            )}
        </div>
    );
};