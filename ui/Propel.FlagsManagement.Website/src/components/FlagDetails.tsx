import { useState } from 'react';
import { 
    Calendar, 
    Clock, 
    Percent, 
    Eye, 
    EyeOff, 
    Lock, 
    Shield, 
    PlayCircle, 
    Trash2 
} from 'lucide-react';
import type { FeatureFlagDto } from '../services/apiService';
import { StatusBadge } from './StatusBadge';
import { 
    getScheduleStatus, 
    formatDate, 
    formatRelativeTime, 
    hasValidTags, 
    getTagEntries 
} from '../utils/flagHelpers';

interface FlagDetailsProps {
    flag: FeatureFlagDto;
    onToggle: (flag: FeatureFlagDto) => Promise<void>;
    onSetPercentage: (flag: FeatureFlagDto, percentage: number) => Promise<void>;
    onSchedule: (flag: FeatureFlagDto, enableDate: string, disableDate?: string) => Promise<void>;
    onDelete: (key: string) => void;
}

export const FlagDetails: React.FC<FlagDetailsProps> = ({
    flag,
    onToggle,
    onSetPercentage,
    onSchedule,
    onDelete
}) => {
    const [editingPercentage, setEditingPercentage] = useState(false);
    const [newPercentage, setNewPercentage] = useState(flag.percentageEnabled || 0);
    const [editingSchedule, setEditingSchedule] = useState(false);
    const [scheduleData, setScheduleData] = useState({
        enableDate: flag.scheduledEnableDate ? flag.scheduledEnableDate.slice(0, 16) : '',
        disableDate: flag.scheduledDisableDate ? flag.scheduledDisableDate.slice(0, 16) : ''
    });
    const [operationLoading, setOperationLoading] = useState(false);

    const scheduleStatus = getScheduleStatus(flag);

    const handlePercentageSubmit = async () => {
        try {
            setOperationLoading(true);
            await onSetPercentage(flag, newPercentage);
            setEditingPercentage(false);
        } catch (error) {
            console.error('Failed to set percentage:', error);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleScheduleSubmit = async () => {
        try {
            setOperationLoading(true);
            await onSchedule(
                flag,
                scheduleData.enableDate ? new Date(scheduleData.enableDate).toISOString() : '',
                scheduleData.disableDate ? new Date(scheduleData.disableDate).toISOString() : undefined
            );
            setEditingSchedule(false);
        } catch (error) {
            console.error('Failed to schedule flag:', error);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleToggle = async () => {
        try {
            setOperationLoading(true);
            await onToggle(flag);
        } catch (error) {
            console.error('Failed to toggle flag:', error);
        } finally {
            setOperationLoading(false);
        }
    };

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div className="flex justify-between items-start mb-4">
                <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{flag.name}</h3>
                        {flag.isPermanent && (
                            <div className="flex items-center gap-1 px-2 py-1 bg-amber-100 text-amber-800 rounded-full text-xs font-medium">
                                <Shield className="w-3 h-3" />
                                Permanent
                            </div>
                        )}
                        {scheduleStatus.isActive && (
                            <div className="flex items-center gap-1 px-2 py-1 bg-green-100 text-green-700 rounded-full text-xs font-medium">
                                <PlayCircle className="w-3 h-3" />
                                Live
                            </div>
                        )}
                    </div>
                    <p className="text-sm text-gray-500 font-mono">{flag.key}</p>
                </div>
                <div className="flex items-center gap-2">
                    <StatusBadge status={flag.status} />
                    {!flag.isPermanent && (
                        <button
                            onClick={() => onDelete(flag.key)}
                            className="p-1.5 text-red-600 hover:bg-red-50 rounded-md transition-colors"
                            title="Delete Flag"
                        >
                            <Trash2 className="w-4 h-4" />
                        </button>
                    )}
                </div>
            </div>

            <p className="text-gray-600 mb-6">{flag.description || 'No description provided'}</p>

            {/* Scheduled Status Indicator */}
            {flag.status === 'Scheduled' && (
                <div className={`mb-4 p-3 rounded-lg border ${
                    scheduleStatus.isActive 
                        ? 'bg-green-50 border-green-200' 
                        : scheduleStatus.phase === 'upcoming'
                        ? 'bg-blue-50 border-blue-200'
                        : 'bg-gray-50 border-gray-200'
                }`}>
                    <div className="flex items-center gap-2 mb-1">
                        {scheduleStatus.isActive ? (
                            <>
                                <PlayCircle className="w-4 h-4 text-green-600" />
                                <span className="font-medium text-green-800">Schedule Currently Active</span>
                            </>
                        ) : scheduleStatus.phase === 'upcoming' ? (
                            <>
                                <Clock className="w-4 h-4 text-blue-600" />
                                <span className="font-medium text-blue-800">Schedule Upcoming</span>
                            </>
                        ) : scheduleStatus.phase === 'expired' ? (
                            <>
                                <Clock className="w-4 h-4 text-gray-600" />
                                <span className="font-medium text-gray-800">Schedule Expired</span>
                            </>
                        ) : null}
                    </div>
                    
                    {scheduleStatus.nextAction && scheduleStatus.nextActionTime && (
                        <p className={`text-sm ${
                            scheduleStatus.isActive ? 'text-green-700' : 
                            scheduleStatus.phase === 'upcoming' ? 'text-blue-700' : 'text-gray-700'
                        }`}>
                            {scheduleStatus.nextAction} {formatRelativeTime(scheduleStatus.nextActionTime)}
                        </p>
                    )}
                    
                    {scheduleStatus.isActive && !scheduleStatus.nextAction && (
                        <p className="text-sm text-green-700">
                            Flag is currently enabled via schedule (no end date)
                        </p>
                    )}
                    
                    {scheduleStatus.phase === 'expired' && (
                        <p className="text-sm text-gray-700">
                            Scheduled period has ended
                        </p>
                    )}
                </div>
            )}

            {/* Quick Actions */}
            <div className="grid grid-cols-2 gap-3 mb-6">
                <button
                    onClick={handleToggle}
                    disabled={operationLoading}
                    className={`flex items-center justify-center gap-2 px-4 py-2 rounded-md font-medium disabled:opacity-50 ${
                        flag.status === 'Enabled'
                            ? 'bg-red-100 text-red-700 hover:bg-red-200'
                            : 'bg-green-100 text-green-700 hover:bg-green-200'
                    }`}
                >
                    {flag.status === 'Enabled' ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    {flag.status === 'Enabled' ? 'Disable' : 'Enable'}
                </button>

                <button
                    onClick={() => setEditingPercentage(true)}
                    disabled={operationLoading}
                    className="flex items-center justify-center gap-2 px-4 py-2 bg-yellow-100 text-yellow-700 rounded-md hover:bg-yellow-200 font-medium disabled:opacity-50"
                >
                    <Percent className="w-4 h-4" />
                    Percentage Rollout
                </button>
            </div>

            {/* Permanent Flag Warning */}
            {flag.isPermanent && (
                <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded-lg">
                    <div className="flex items-center gap-2 text-amber-800 text-sm">
                        <Lock className="w-4 h-4" />
                        <span className="font-medium">This is a permanent feature flag</span>
                    </div>
                    <p className="text-amber-700 text-xs mt-1">
                        Permanent flags cannot be deleted and are intended for long-term use in production systems.
                    </p>
                </div>
            )}

            {/* Percentage Editing */}
            {editingPercentage && (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 mb-4">
                    <h4 className="font-medium text-yellow-800 mb-2">Set Percentage Rollout</h4>
                    <div className="flex items-center gap-3">
                        <input
                            type="range"
                            min="0"
                            max="100"
                            value={newPercentage}
                            onChange={(e) => setNewPercentage(parseInt(e.target.value))}
                            className="flex-1"
                            disabled={operationLoading}
                        />
                        <span className="text-sm font-medium text-yellow-800 min-w-[3rem]">{newPercentage}%</span>
                    </div>
                    <div className="flex gap-2 mt-3">
                        <button
                            onClick={handlePercentageSubmit}
                            disabled={operationLoading}
                            className="px-3 py-1 bg-yellow-600 text-white rounded text-sm hover:bg-yellow-700 disabled:opacity-50"
                        >
                            {operationLoading ? 'Applying...' : 'Apply'}
                        </button>
                        <button
                            onClick={() => {
                                setEditingPercentage(false);
                                setNewPercentage(flag.percentageEnabled || 0);
                            }}
                            disabled={operationLoading}
                            className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                        >
                            Cancel
                        </button>
                    </div>
                </div>
            )}

            {/* Schedule Section */}
            <div className="space-y-4">
                <div className="flex justify-between items-center">
                    <h4 className="font-medium text-gray-900">Scheduling</h4>
                    <button
                        onClick={() => setEditingSchedule(true)}
                        disabled={operationLoading}
                        className="text-blue-600 hover:text-blue-800 text-sm flex items-center gap-1 disabled:opacity-50"
                    >
                        <Calendar className="w-4 h-4" />
                        Schedule
                    </button>
                </div>

                {editingSchedule ? (
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                        <div className="space-y-3">
                            <div>
                                <label className="block text-sm font-medium text-blue-800 mb-1">Enable Date</label>
                                <input
                                    type="datetime-local"
                                    value={scheduleData.enableDate}
                                    onChange={(e) => setScheduleData({ ...scheduleData, enableDate: e.target.value })}
                                    className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
                                    disabled={operationLoading}
                                />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-blue-800 mb-1">Disable Date (Optional)</label>
                                <input
                                    type="datetime-local"
                                    value={scheduleData.disableDate}
                                    onChange={(e) => setScheduleData({ ...scheduleData, disableDate: e.target.value })}
                                    className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
                                    disabled={operationLoading}
                                />
                            </div>
                        </div>
                        <div className="flex gap-2 mt-3">
                            <button
                                onClick={handleScheduleSubmit}
                                disabled={operationLoading || !scheduleData.enableDate}
                                className="px-3 py-1 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
                            >
                                {operationLoading ? 'Scheduling...' : 'Schedule'}
                            </button>
                            <button
                                onClick={() => setEditingSchedule(false)}
                                disabled={operationLoading}
                                className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                ) : (
                    <div className="text-sm text-gray-600 space-y-1">
                        <div>Enable: {formatDate(flag.scheduledEnableDate)}</div>
                        <div>Disable: {formatDate(flag.scheduledDisableDate)}</div>
                    </div>
                )}
            </div>

            {/* Current Status */}
            {flag.status === 'Percentage' && (
                <div className="mt-4 p-3 bg-yellow-50 rounded-lg">
                    <div className="text-sm text-yellow-800">
                        Currently enabled for <strong>{flag.percentageEnabled || 0}%</strong> of users
                    </div>
                </div>
            )}

            {/* User Lists */}
            {((flag.enabledUsers && flag.enabledUsers.length > 0) || (flag.disabledUsers && flag.disabledUsers.length > 0)) && (
                <div className="mt-4 space-y-2">
                    {flag.enabledUsers && flag.enabledUsers.length > 0 && (
                        <div className="text-sm">
                            <span className="font-medium text-green-700">Enabled for: </span>
                            <span className="text-gray-600">{flag.enabledUsers.join(', ')}</span>
                        </div>
                    )}
                    {flag.disabledUsers && flag.disabledUsers.length > 0 && (
                        <div className="text-sm">
                            <span className="font-medium text-red-700">Disabled for: </span>
                            <span className="text-gray-600">{flag.disabledUsers.join(', ')}</span>
                        </div>
                    )}
                </div>
            )}

            {/* Metadata */}
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
        </div>
    );
};