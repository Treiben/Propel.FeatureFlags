import { useState, useEffect } from 'react';
import { Trash2, Eye, EyeOff } from 'lucide-react';
import type { FeatureFlagDto } from '../services/apiService';
import { StatusBadge } from './StatusBadge';

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
    PercentageStatusIndicator, 
    PercentageEditor 
} from './flag-details/PercentageComponents';
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
    onSetPercentage: (flag: FeatureFlagDto, percentage: number) => Promise<void>;
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
        expirationDate?: string;
        isPermanent?: boolean;
        tags?: Record<string, string>;
    }) => Promise<void>;
    onDelete: (key: string) => void;
}

export const FlagDetails: React.FC<FlagDetailsProps> = ({
    flag,
    onToggle,
    onSetPercentage,
    onSchedule,
    onClearSchedule,
    onUpdateTimeWindow,
    onClearTimeWindow,
    onUpdateFlag,
    onDelete
}) => {
    const [editingPercentage, setEditingPercentage] = useState(false);
    const [operationLoading, setOperationLoading] = useState(false);

    // Reset editing states when flag changes (when a different flag is selected)
    useEffect(() => {
        setEditingPercentage(false);
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

    const handleSetPercentageWrapper = async (flag: FeatureFlagDto, percentage: number) => {
        setOperationLoading(true);
        try {
            await onSetPercentage(flag, percentage);
            setEditingPercentage(false);
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
        expirationDate?: string;
        isPermanent?: boolean;
        tags?: Record<string, string>;
    }) => {
        setOperationLoading(true);
        try {
            await onUpdateFlag(flag, updates);
        } finally {
            setOperationLoading(false);
        }
    };

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
                    <StatusBadge status={flag.status} showDescription={true} />
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
            <CompoundStatusOverview flag={flag} />

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
                        flag.status === 'Enabled'
                            ? 'bg-red-100 text-red-700 hover:bg-red-200'
                            : 'bg-green-100 text-green-700 hover:bg-green-200'
                    }`}
                >
                    {flag.status === 'Enabled' ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    {flag.status === 'Enabled' ? 'Disable' : 'Enable'}
                </button>

                <PercentageEditor
                    flag={flag}
                    isEditing={editingPercentage}
                    onStartEditing={() => setEditingPercentage(true)}
                    onCancelEditing={() => setEditingPercentage(false)}
                    onSetPercentage={handleSetPercentageWrapper}
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
            <PercentageStatusIndicator flag={flag} />
            <UserLists flag={flag} />
            <FlagMetadata flag={flag} />
        </div>
    );
};