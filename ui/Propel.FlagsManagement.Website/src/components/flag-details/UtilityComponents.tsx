import { Lock, AlertCircle } from 'lucide-react';
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