import { useState } from 'react';
import { Calendar, Clock, Percent, Users, Info, Play, Loader2, CheckCircle, XCircle } from 'lucide-react';
import type { FeatureFlagDto, EvaluationResult } from '../../services/apiService';
import { 
    parseStatusComponents, 
    getStatusDescription,
    getScheduleStatus,
    getTimeWindowStatus,
    formatDate,
    formatTime,
    formatRelativeTime,
    getDayName
} from '../../utils/flagHelpers';

interface CompoundStatusOverviewProps {
    flag: FeatureFlagDto;
    onEvaluateFlag?: (key: string, userId?: string, attributes?: Record<string, any>) => Promise<EvaluationResult>;
    evaluationResult?: EvaluationResult;
    evaluationLoading?: boolean;
}

export const CompoundStatusOverview: React.FC<CompoundStatusOverviewProps> = ({ 
    flag, 
    onEvaluateFlag,
    evaluationResult,
    evaluationLoading = false
}) => {
    const [testUserId, setTestUserId] = useState('');
    const [testAttributes, setTestAttributes] = useState('{}');
    const [showEvaluation, setShowEvaluation] = useState(false);
    const components = parseStatusComponents(flag);
    const scheduleStatus = getScheduleStatus(flag);
    const timeWindowStatus = getTimeWindowStatus(flag);

    // Don't show for simple enabled/disabled states
    if (components.baseStatus === 'Enabled' || 
        (components.baseStatus === 'Disabled' && !components.isScheduled && !components.hasTimeWindow && !components.hasPercentage && !components.hasUserTargeting)) {
        return null;
    }

    const handleEvaluate = async () => {
        if (!onEvaluateFlag) return;

        try {
            let attributes: Record<string, any> | undefined;
            if (testAttributes.trim()) {
                attributes = JSON.parse(testAttributes);
            }
            
            await onEvaluateFlag(flag.key, testUserId || undefined, attributes);
        } catch (error) {
            console.error('Failed to evaluate flag:', error);
            // You might want to show a toast notification here
        }
    };

    return (
        <div className="mb-4 p-4 bg-gray-50 border border-gray-200 rounded-lg">
            <div className="flex items-center justify-between gap-2 mb-3">
                <div className="flex items-center gap-2">
                    <Info className="w-4 h-4 text-gray-600" />
                    <h4 className="font-medium text-gray-900">Status Overview</h4>
                    <span className="text-sm text-gray-600">({getStatusDescription(flag)})</span>
                </div>
                <button
                    onClick={() => setShowEvaluation(!showEvaluation)}
                    className="flex items-center gap-1 px-2 py-1 text-xs bg-blue-100 text-blue-700 rounded hover:bg-blue-200 transition-colors"
                >
                    <Play className="w-3 h-3" />
                    Test Evaluation
                </button>
            </div>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
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
                        <span className="text-yellow-700">{flag.userRolloutPercentage || 0}% of users</span>
                    </div>
                )}

                {/* User Targeting */}
                {components.hasUserTargeting && (
                    <div className="flex items-center gap-2">
                        <Users className="w-4 h-4 text-purple-600" />
                        <span className="font-medium">User Targeting:</span>
                        <span className="text-purple-700">
                            {(flag.allowedUsers?.length || 0)} enabled, {(flag.blockedUsers?.length || 0)} disabled
                        </span>
                    </div>
                )}
            </div>

            {/* Evaluation Section */}
            {showEvaluation && onEvaluateFlag && (
                <div className="mt-4 pt-4 border-t border-gray-200">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
                        <div>
                            <label className="block text-xs font-medium text-gray-700 mb-1">Test User ID (optional)</label>
                            <input
                                type="text"
                                value={testUserId}
                                onChange={(e) => setTestUserId(e.target.value)}
                                placeholder="user123"
                                className="w-full px-2 py-1 text-xs border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-500"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-gray-700 mb-1">Attributes (JSON)</label>
                            <input
                                type="text"
                                value={testAttributes}
                                onChange={(e) => setTestAttributes(e.target.value)}
                                placeholder='{"country": "US", "plan": "premium"}'
                                className="w-full px-2 py-1 text-xs border border-gray-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-500"
                            />
                        </div>
                    </div>
                    
                    <div className="flex items-center gap-3">
                        <button
                            onClick={handleEvaluate}
                            disabled={evaluationLoading}
                            className="flex items-center gap-1 px-3 py-1 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {evaluationLoading ? (
                                <Loader2 className="w-3 h-3 animate-spin" />
                            ) : (
                                <Play className="w-3 h-3" />
                            )}
                            Evaluate
                        </button>
                        
                        {evaluationResult && (
                            <div className="flex items-center gap-1 text-xs">
                                {evaluationResult.isEnabled ? (
                                    <CheckCircle className="w-3 h-3 text-green-600" />
                                ) : (
                                    <XCircle className="w-3 h-3 text-red-600" />
                                )}
                                <span className={evaluationResult.isEnabled ? 'text-green-700' : 'text-red-700'}>
                                    {evaluationResult.isEnabled ? 'Enabled' : 'Disabled'}
                                </span>
                                {evaluationResult.reason && (
                                    <span className="text-gray-600">({evaluationResult.reason})</span>
                                )}
                                {evaluationResult.variation && evaluationResult.variation !== 'default' && (
                                    <span className="text-gray-600">- Variation: {evaluationResult.variation}</span>
                                )}
                            </div>
                        )}
                    </div>
                </div>
            )}

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
                            {flag.windowDays && flag.windowDays.length > 0 && `, ${flag.windowDays.map(day => getDayName(day)).join(', ')}`}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};