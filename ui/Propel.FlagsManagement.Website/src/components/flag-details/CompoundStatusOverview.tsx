import { Calendar, Clock, Percent, Users, Eye, EyeOff, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { 
    parseStatusComponents, 
    getStatusDescription,
    getScheduleStatus,
    getTimeWindowStatus,
    formatDate,
    formatTime,
    formatRelativeTime
} from '../../utils/flagHelpers';

interface CompoundStatusOverviewProps {
    flag: FeatureFlagDto;
}

export const CompoundStatusOverview: React.FC<CompoundStatusOverviewProps> = ({ flag }) => {
    const components = parseStatusComponents(flag.status);
    const scheduleStatus = getScheduleStatus(flag);
    const timeWindowStatus = getTimeWindowStatus(flag);

    // Don't show for simple enabled/disabled states
    if (components.baseStatus === 'Enabled' || 
        (components.baseStatus === 'Disabled' && !components.isScheduled && !components.hasTimeWindow && !components.hasPercentage && !components.hasUserTargeting)) {
        return null;
    }

    return (
        <div className="mb-4 p-4 bg-gray-50 border border-gray-200 rounded-lg">
            <div className="flex items-center gap-2 mb-3">
                <Info className="w-4 h-4 text-gray-600" />
                <h4 className="font-medium text-gray-900">Status Overview</h4>
                <span className="text-sm text-gray-600">({getStatusDescription(flag.status)})</span>
            </div>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                {/* Base Status */}
                <div className="flex items-center gap-2">
                    {components.baseStatus === 'Enabled' ? (
                        <Eye className="w-4 h-4 text-green-600" />
                    ) : (
                        <EyeOff className="w-4 h-4 text-red-600" />
                    )}
                    <span className="font-medium">Base Status:</span>
                    <span className={components.baseStatus === 'Enabled' ? 'text-green-700' : 'text-red-700'}>
                        {components.baseStatus}
                    </span>
                </div>

                {/* Scheduling */}
                {components.isScheduled && (
                    <div className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-blue-600" />
                        <span className="font-medium">Scheduling:</span>
                        <span className={
                            scheduleStatus.isActive ? 'text-green-700' : 
                            scheduleStatus.phase === 'upcoming' ? 'text-blue-700' : 'text-gray-700'
                        }>
                            {scheduleStatus.isActive ? 'Active' : 
                             scheduleStatus.phase === 'upcoming' ? 'Upcoming' : 
                             scheduleStatus.phase === 'expired' ? 'Expired' : 'Configured'}
                        </span>
                        {scheduleStatus.nextActionTime && (
                            <span className="text-gray-600">
                                ({formatRelativeTime(scheduleStatus.nextActionTime)})
                            </span>
                        )}
                    </div>
                )}

                {/* Time Window */}
                {components.hasTimeWindow && (
                    <div className="flex items-center gap-2">
                        <Clock className="w-4 h-4 text-indigo-600" />
                        <span className="font-medium">Time Window:</span>
                        <span className={timeWindowStatus.isActive ? 'text-green-700' : 'text-gray-700'}>
                            {timeWindowStatus.isActive ? 'Active' : 'Inactive'}
                        </span>
                        <span className="text-gray-600">
                            ({formatTime(flag.windowStartTime)} - {formatTime(flag.windowEndTime)})
                        </span>
                    </div>
                )}

                {/* Percentage */}
                {components.hasPercentage && (
                    <div className="flex items-center gap-2">
                        <Percent className="w-4 h-4 text-yellow-600" />
                        <span className="font-medium">Percentage:</span>
                        <span className="text-yellow-700">{flag.percentageEnabled || 0}% of users</span>
                    </div>
                )}

                {/* User Targeting */}
                {components.hasUserTargeting && (
                    <div className="flex items-center gap-2">
                        <Users className="w-4 h-4 text-purple-600" />
                        <span className="font-medium">User Targeting:</span>
                        <span className="text-purple-700">
                            {(flag.enabledUsers?.length || 0)} enabled, {(flag.disabledUsers?.length || 0)} disabled
                        </span>
                    </div>
                )}
            </div>

            {/* Additional Details */}
            {(components.isScheduled || components.hasTimeWindow) && (
                <div className="mt-3 pt-3 border-t border-gray-200 text-xs text-gray-600">
                    {components.isScheduled && (
                        <div>Schedule: {formatDate(flag.scheduledEnableDate)} - {formatDate(flag.scheduledDisableDate)}</div>
                    )}
                    {components.hasTimeWindow && (
                        <div>
                            Window: {formatTime(flag.windowStartTime)} - {formatTime(flag.windowEndTime)} 
                            {flag.timeZone && ` (${flag.timeZone})`}
                            {flag.windowDays && flag.windowDays.length > 0 && `, ${flag.windowDays.join(', ')}`}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};