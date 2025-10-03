import { Lock, PlayCircle, Trash2, Timer } from 'lucide-react';
import type { FeatureFlagDto } from '../services/apiService';
import { parseTargetingRules, Scope } from '../services/apiService';
import { StatusBadge } from './StatusBadge';
import { getScheduleStatus, getTimeWindowStatus, formatRelativeTime, hasValidTags, getTagEntries, parseStatusComponents } from '../utils/flagHelpers';
import { theme } from '../styles/theme';

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
    const timeWindowStatus = getTimeWindowStatus(flag);
    const components = parseStatusComponents(flag);

    const targetingRules = parseTargetingRules(flag.targetingRules);
    const targetingRulesCount = targetingRules.length;

    // Get percentage from userAccess or tenantAccess
    const percentage = flag.userAccess?.rolloutPercentage || flag.tenantAccess?.rolloutPercentage || 0;

    return (
        <div
            onClick={onClick}
            className={`bg-white border rounded-lg p-4 cursor-pointer transition-all ${isSelected
                ? `border-blue-500 ring-2 ring-blue-200`
                : `${theme.neutral.border[200]} hover:border-gray-300`
                }`}
        >
            {/* BUG FIX #12: Better layout for long content */}
            <div className="space-y-3">
                {/* Header section */}
                <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <h3 className={`font-medium ${theme.neutral.text[900]} break-words`}>{flag.name}</h3>
                            {flag.isPermanent && (
                                <div className={`flex items-center gap-1 px-1.5 py-0.5 ${theme.warning[100]} ${theme.warning.text[700]} rounded text-xs flex-shrink-0`}>
                                    <Lock className="w-3 h-3" />
                                    <span className="font-medium">PERM</span>
                                </div>
                            )}
                            {scheduleStatus.isActive && (
                                <div className={`flex items-center gap-1 px-1.5 py-0.5 ${theme.success[100]} ${theme.success.text[700]} rounded text-xs flex-shrink-0`}>
                                    <PlayCircle className="w-3 h-3" />
                                    <span className="font-medium">LIVE</span>
                                </div>
                            )}
                            {timeWindowStatus.isActive && (
                                <div className={`flex items-center gap-1 px-1.5 py-0.5 ${theme.success[100]} ${theme.success.text[700]} rounded text-xs flex-shrink-0`}>
                                    <Timer className="w-3 h-3" />
                                    <span className="font-medium">ACTIVE</span>
                                </div>
                            )}
                        </div>
                        <p className={`text-sm ${theme.neutral.text[500]} font-mono break-all`}>{flag.key}</p>
                    </div>

                    {/* Status badge and delete button */}
                    <div className="flex items-start gap-2 flex-shrink-0">
                        <StatusBadge flag={flag} />
                        {!flag.isPermanent && (
                            <button
                                onClick={(e) => {
                                    e.stopPropagation();
                                    onDelete(flag.key);
                                }}
                                className={`p-1 ${theme.danger.text[600]} hover:bg-red-50 rounded transition-colors`}
                                title="Delete Flag"
                            >
                                <Trash2 className="w-3 h-3" />
                            </button>
                        )}
                    </div>
                </div>

                {/* BUG FIX #11 & #23: Show application info only for Application scope */}
                {flag.scope === Scope.Application && flag.applicationName && (
                    <div className="flex items-center gap-1.5 text-xs">
                        <span className={`px-1.5 py-0.5 ${theme.primary[50]} ${theme.primary.text[600]} rounded ${theme.primary.border[200]} border font-medium truncate max-w-[200px]`} title={flag.applicationName}>
                            {flag.applicationName}
                        </span>
                        {flag.applicationVersion && (
                            <span className={theme.neutral.text[500]}>v{flag.applicationVersion}</span>
                        )}
                    </div>
                )}

                {/* Description */}
                <p className={`text-sm ${theme.neutral.text[600]} line-clamp-2`}>{flag.description || 'No description'}</p>

                {/* Tags */}
                {hasValidTags(flag.tags) && (
                    <div className="flex flex-wrap gap-1">
                        {getTagEntries(flag.tags).slice(0, 3).map(([key, value]) => (
                            <span key={key} className={`${theme.neutral[100]} ${theme.neutral.text[600]} px-2 py-1 rounded text-xs truncate max-w-[150px]`} title={`${key}: ${value}`}>
                                {key}: {value}
                            </span>
                        ))}
                        {getTagEntries(flag.tags).length > 3 && (
                            <span className={`text-xs ${theme.neutral.text[500]} italic px-2 py-1`}>
                                +{getTagEntries(flag.tags).length - 3} more
                            </span>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
};