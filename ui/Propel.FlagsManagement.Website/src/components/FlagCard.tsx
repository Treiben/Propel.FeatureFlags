import { Lock, PlayCircle, Trash2 } from 'lucide-react';
import type { FeatureFlagDto } from '../services/apiService';
import { StatusBadge } from './StatusBadge';
import { getScheduleStatus, formatRelativeTime, hasValidTags, getTagEntries, parseStatusComponents } from '../utils/flagHelpers';

interface FlagCardProps {
    flag: FeatureFlagDto;
    isSelected: boolean;
    onClick: () => void;
    onDelete: (key: string) => void;
}

export const FlagCard: React.FC<FlagCardProps> = ({
    flag,
    isSelected,
    onClick,
    onDelete
}) => {
    const scheduleStatus = getScheduleStatus(flag);
    const components = parseStatusComponents(flag);

    return (
        <div
            onClick={onClick}
            className={`bg-white border rounded-lg p-4 cursor-pointer transition-all ${
                isSelected
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
                        {scheduleStatus.isActive && (
                            <div className="flex items-center gap-1 px-1.5 py-0.5 bg-green-100 text-green-700 rounded text-xs">
                                <PlayCircle className="w-3 h-3" />
                                <span className="font-medium">LIVE</span>
                            </div>
                        )}
                    </div>
                    <p className="text-sm text-gray-500 font-mono">{flag.key}</p>
                    <p className="text-sm text-gray-600 mt-1 line-clamp-2">{flag.description || 'No description'}</p>
                </div>

                <div className="flex flex-col items-end gap-2">
                    <div className="flex items-center gap-2">
                        <StatusBadge flag={flag} />
                        {!flag.isPermanent && (
                            <button
                                onClick={(e) => {
                                    e.stopPropagation();
                                    onDelete(flag.key);
                                }}
                                className="p-1 text-red-600 hover:bg-red-50 rounded transition-colors"
                                title="Delete Flag"
                            >
                                <Trash2 className="w-3 h-3" />
                            </button>
                        )}
                    </div>

                    {components.hasPercentage && (
                        <span className="text-xs text-gray-500">{flag.userRolloutPercentage || 0}%</span>
                    )}

                    {components.isScheduled && scheduleStatus.nextAction && scheduleStatus.nextActionTime && (
                        <span className="text-xs text-gray-500">
                            {scheduleStatus.nextAction} {formatRelativeTime(scheduleStatus.nextActionTime)}
                        </span>
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
    );
};