import { useState, useEffect } from 'react';
import { Trash2, Eye, EyeOff } from 'lucide-react';
import type { FeatureFlagDto, EvaluationResult } from '../services/apiService';
import { StatusBadge } from './StatusBadge';
import { parseStatusComponents } from '../utils/flagHelpers';

// Import business logic components
import { FlagStatusIndicators } from './flag-details/FlagStatusIndicators';
import { CompoundStatusOverview } from './flag-details/CompoundStatusOverview';
import { 
    SchedulingStatusIndicator, 
    SchedulingSection 
} from './flag-details/SchedulingComponents';
import { 
    TimeWindowStatusIndicator, 
    TimeWindowSection 
} from './flag-details/TimeWindowComponents';
import { 
    UserAccessControlStatusIndicator, 
    UserAccessControlEditor 
} from './flag-details/UserAccessControlComponents';
import { 
    ExpirationWarning, 
    PermanentFlagWarning, 
    UserLists, 
    FlagMetadata,
    FlagEditSection
} from './flag-details/UtilityComponents';

interface FlagDetailsProps {
    flag: FeatureFlagDto;
    onToggle: (flag: FeatureFlagDto) => Promise<void>;
    onUpdateUserAccess: (allowedUsers?: string[], blockedUsers?: string[], percentage?: number) => Promise<void>;
    onSchedule: (flag: FeatureFlagDto, enableDate: string, disableDate?: string) => Promise<void>;
    onClearSchedule: (flag: FeatureFlagDto) => Promise<void>;
    onUpdateTimeWindow: (flag: FeatureFlagDto, timeWindowData: {
        windowStartTime: string;
        windowEndTime: string;
        timeZone: string;
        windowDays: string[];
    }) => Promise<void>;
    onClearTimeWindow: (flag: FeatureFlagDto) => Promise<void>;
    onUpdateFlag: (flag: FeatureFlagDto, updates: {
        name?: string;
        description?: string;
        tags?: Record<string, string>;
        isPermanent?: boolean;
        expirationDate?: string;
    }) => Promise<void>;
    onDelete: (key: string) => void;
    onEvaluateFlag?: (key: string, userId?: string, attributes?: Record<string, any>) => Promise<EvaluationResult>;
    evaluationResult?: EvaluationResult;
    evaluationLoading?: boolean;
}

export const FlagDetails: React.FC<FlagDetailsProps> = ({
    flag,
    onToggle,
    onUpdateUserAccess,
    onSchedule,
    onClearSchedule,
    onUpdateTimeWindow,
    onClearTimeWindow,
    onUpdateFlag,
    onDelete,
    onEvaluateFlag,
    evaluationResult,
    evaluationLoading = false
}) => {
    const [editingUserAccess, setEditingUserAccess] = useState(false);
    const [operationLoading, setOperationLoading] = useState(false);

    // Reset editing states when flag changes (when a different flag is selected)
    useEffect(() => {
        setEditingUserAccess(false);
    }, [flag.key]);

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

    const handleUpdateUserAccessWrapper = async (allowedUsers?: string[], blockedUsers?: string[], percentage?: number) => {
        setOperationLoading(true);
        try {
            await onUpdateUserAccess(allowedUsers, blockedUsers, percentage);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleScheduleWrapper = async (flag: FeatureFlagDto, enableDate: string, disableDate?: string) => {
        setOperationLoading(true);
        try {
            await onSchedule(flag, enableDate, disableDate);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearScheduleWrapper = async () => {
        setOperationLoading(true);
        try {
            await onClearSchedule(flag);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateTimeWindowWrapper = async (flag: FeatureFlagDto, timeWindowData: any) => {
        setOperationLoading(true);
        try {
            await onUpdateTimeWindow(flag, timeWindowData);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearTimeWindowWrapper = async () => {
        setOperationLoading(true);
        try {
            await onClearTimeWindow(flag);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateFlagWrapper = async (updates: {
        name?: string;
        description?: string;
        tags?: Record<string, string>;
        isPermanent?: boolean;
        expirationDate?: string;
    }) => {
        setOperationLoading(true);
        try {
            await onUpdateFlag(flag, updates);
        } finally {
            setOperationLoading(false);
        }
    };

    const components = parseStatusComponents(flag);
    const isEnabled = components.baseStatus === 'Enabled';

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            {/* Flag Header */}
            <div className="flex justify-between items-start mb-4">
                <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{flag.name}</h3>
                        <FlagStatusIndicators flag={flag} />
                    </div>
                    <p className="text-sm text-gray-500 font-mono">{flag.key}</p>
                </div>
                <div className="flex items-center gap-2">
                    <StatusBadge flag={flag} showDescription={true} />
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

            {/* Compound Status Overview */}
            <CompoundStatusOverview 
                flag={flag} 
                onEvaluateFlag={onEvaluateFlag}
                evaluationResult={evaluationResult}
                evaluationLoading={evaluationLoading}
            />

            {/* Status Indicators */}
            <ExpirationWarning flag={flag} />
            <SchedulingStatusIndicator flag={flag} />
            <TimeWindowStatusIndicator flag={flag} />

            {/* Quick Actions */}
            <div className="grid grid-cols-2 gap-3 mb-6">
                <button
                    onClick={handleToggle}
                    disabled={operationLoading}
                    className={`flex items-center justify-center gap-2 px-4 py-2 rounded-md font-medium disabled:opacity-50 ${
                        isEnabled
                            ? 'bg-red-100 text-red-700 hover:bg-red-200'
                            : 'bg-green-100 text-green-700 hover:bg-green-200'
                    }`}
                >
                    {isEnabled ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    {isEnabled ? 'Disable' : 'Enable'}
                </button>

                <UserAccessControlEditor
                    flag={flag}
                    isEditing={editingUserAccess}
                    onStartEditing={() => setEditingUserAccess(true)}
                    onCancelEditing={() => setEditingUserAccess(false)}
                    onUpdateUserAccess={handleUpdateUserAccessWrapper}
                    operationLoading={operationLoading}
                />
            </div>

            {/* Warnings */}
            <PermanentFlagWarning flag={flag} />

            {/* Flag Edit Section */}
            <FlagEditSection
                flag={flag}
                onUpdateFlag={handleUpdateFlagWrapper}
                operationLoading={operationLoading}
            />

            {/* Business Logic Sections */}
            <SchedulingSection
                flag={flag}
                onSchedule={handleScheduleWrapper}
                onClearSchedule={handleClearScheduleWrapper}
                operationLoading={operationLoading}
            />

            <TimeWindowSection
                flag={flag}
                onUpdateTimeWindow={handleUpdateTimeWindowWrapper}
                onClearTimeWindow={handleClearTimeWindowWrapper}
                operationLoading={operationLoading}
            />

            {/* Additional Information */}
            <UserAccessControlStatusIndicator flag={flag} />
            <UserLists flag={flag} />
            <FlagMetadata flag={flag} />
        </div>
    );
};