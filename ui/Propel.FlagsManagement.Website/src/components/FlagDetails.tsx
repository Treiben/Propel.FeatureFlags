import { useState, useEffect } from 'react';
import { Trash2, Eye, EyeOff, Play, Loader2, CheckCircle, XCircle } from 'lucide-react';
import type { FeatureFlagDto, EvaluationResult } from '../services/apiService';
import { StatusBadge } from './StatusBadge';
import { parseStatusComponents } from '../utils/flagHelpers';

// Import business logic components
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
    UserAccessSection
} from './flag-details/UserAccessControlComponents';
import { 
    ExpirationWarning, 
    PermanentFlagWarning, 
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
    const [operationLoading, setOperationLoading] = useState(false);
    const [showEvaluation, setShowEvaluation] = useState(false);
    const [testUserId, setTestUserId] = useState('');
    const [testAttributes, setTestAttributes] = useState('{}');

    // Reset editing states when flag changes (when a different flag is selected)
    useEffect(() => {
        setShowEvaluation(false);
        setTestUserId('');
        setTestAttributes('{}');
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

    const handleClearUserAccessWrapper = async () => {
        setOperationLoading(true);
        try {
            // Clear user access by setting empty arrays and 0 percentage
            await onUpdateUserAccess([], [], 0);
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
        }
    };

    const components = parseStatusComponents(flag);
    const isEnabled = components.baseStatus === 'Enabled';

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            {/* Flag Header - Enable/Disable, Test Evaluation, and Delete buttons */}
            <div className="flex justify-between items-start mb-4">
                <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{flag.name}</h3>
                        <div className="flex items-center gap-1">
                            <button
                                onClick={handleToggle}
                                disabled={operationLoading}
                                className={`p-2 rounded-md transition-colors font-medium shadow-sm ${
                                    isEnabled
                                        ? 'bg-orange-100 text-orange-700 hover:bg-orange-200 border border-orange-300'
                                        : 'bg-green-100 text-green-700 hover:bg-green-200 border border-green-300'
                                }`}
                                title={isEnabled ? 'Disable Flag' : 'Enable Flag'}
                            >
                                {operationLoading ? (
                                    <Loader2 className="w-4 h-4 animate-spin" />
                                ) : isEnabled ? (
                                    <EyeOff className="w-4 h-4" />
                                ) : (
                                    <Eye className="w-4 h-4" />
                                )}
                            </button>
                            
                            {/* Test Flag Evaluation Button */}
                            {onEvaluateFlag && (
                                <button
                                    onClick={() => setShowEvaluation(!showEvaluation)}
                                    className="p-2 rounded-md transition-colors font-medium shadow-sm bg-blue-100 text-blue-700 hover:bg-blue-200 border border-blue-300"
                                    title="Test Flag Evaluation"
                                >
                                    <Play className="w-4 h-4" />
                                </button>
                            )}
                            
                            {!flag.isPermanent && (
                                <button
                                    onClick={() => onDelete(flag.key)}
                                    className="p-2 text-red-600 hover:bg-red-50 rounded-md transition-colors border border-transparent hover:border-red-200"
                                    title="Delete Flag"
                                >
                                    <Trash2 className="w-4 h-4" />
                                </button>
                            )}
                        </div>
                    </div>
                    <p className="text-sm text-gray-500 font-mono">{flag.key}</p>
                </div>
                <div className="flex items-center gap-2">
                    <StatusBadge flag={flag} showDescription={true} />
                </div>
            </div>

            <p className="text-gray-600 mb-6">{flag.description || 'No description provided'}</p>

            {/* Test Evaluation Panel - shown when evaluation button is clicked */}
            {onEvaluateFlag && showEvaluation && (
                <div className="mb-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
                    <h4 className="font-medium text-blue-900 mb-3">Test Flag Evaluation</h4>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
                        <div>
                            <label className="block text-xs font-medium text-blue-700 mb-1">Test User ID (optional)</label>
                            <input
                                type="text"
                                value={testUserId}
                                onChange={(e) => setTestUserId(e.target.value)}
                                placeholder="user123"
                                className="w-full px-2 py-1 text-xs border border-blue-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-500"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-blue-700 mb-1">Attributes (JSON)</label>
                            <input
                                type="text"
                                value={testAttributes}
                                onChange={(e) => setTestAttributes(e.target.value)}
                                placeholder='{"country": "US", "plan": "premium"}'
                                className="w-full px-2 py-1 text-xs border border-blue-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-500"
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
                                    <span className="text-blue-600">({evaluationResult.reason})</span>
                                )}
                                {evaluationResult.variation && evaluationResult.variation !== 'default' && (
                                    <span className="text-blue-600">- Variation: {evaluationResult.variation}</span>
                                )}
                            </div>
                        )}
                    </div>
                </div>
            )}

            {/* Status Indicators */}
            <ExpirationWarning flag={flag} />
            <SchedulingStatusIndicator flag={flag} />
            <TimeWindowStatusIndicator flag={flag} />
            <UserAccessControlStatusIndicator flag={flag} />

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

            <UserAccessSection
                flag={flag}
                onUpdateUserAccess={handleUpdateUserAccessWrapper}
                onClearUserAccess={handleClearUserAccessWrapper}
                operationLoading={operationLoading}
            />

            {/* Additional Information */}
            <FlagMetadata flag={flag} />
        </div>
    );
};