import { useState, useEffect } from 'react';
import { Trash2, Eye, EyeOff, Play, Loader2, CheckCircle, XCircle } from 'lucide-react';
import type { FeatureFlagDto, EvaluationResult, TargetingRule } from '../services/apiService';
import { parseTargetingRules, EvaluationMode } from '../services/apiService';
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
    TenantAccessControlStatusIndicator,
    TenantAccessSection
} from './flag-details/TenantAccessControlComponents';
import {
    TargetingRulesStatusIndicator,
    TargetingRulesSection
} from './flag-details/TargetingRuleComponents';
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
    onUpdateTenantAccess: (allowedTenants?: string[], blockedTenants?: string[], percentage?: number) => Promise<void>;
    onUpdateTargetingRules: (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => Promise<void>;
    onSchedule: (flag: FeatureFlagDto, enableOn: string, disableOn?: string) => Promise<void>;
    onClearSchedule: (flag: FeatureFlagDto) => Promise<void>;
    onUpdateTimeWindow: (flag: FeatureFlagDto, timeWindowData: {
        startOn: string;
        endOn: string;
        timeZone: string;
        daysActive: string[];
    }) => Promise<void>;
    onClearTimeWindow: (flag: FeatureFlagDto) => Promise<void>;
    onUpdateFlag: (flag: FeatureFlagDto, updates: {
        name?: string;
        description?: string;
        tags?: Record<string, string>;
        expirationDate?: string;
        notes?: string;
    }) => Promise<void>;
    onDelete: (key: string) => void;
    onEvaluateFlag?: (key: string, userId?: string, tenantId?: string, attributes?: Record<string, any>) => Promise<EvaluationResult>;
    evaluationResult?: EvaluationResult;
    evaluationLoading?: boolean;
}

export const FlagDetails: React.FC<FlagDetailsProps> = ({
    flag,
    onToggle,
    onUpdateUserAccess,
    onUpdateTenantAccess,
    onUpdateTargetingRules,
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
    const [testTenantId, setTestTenantId] = useState('');
    const [testAttributes, setTestAttributes] = useState('{}');

    useEffect(() => {
        setShowEvaluation(false);
        setTestUserId('');
        setTestTenantId('');
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
            await onUpdateUserAccess([], [], 0);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateTenantAccessWrapper = async (allowedTenants?: string[], blockedTenants?: string[], percentage?: number) => {
        setOperationLoading(true);
        try {
            await onUpdateTenantAccess(allowedTenants, blockedTenants, percentage);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearTenantAccessWrapper = async () => {
        setOperationLoading(true);
        try {
            await onUpdateTenantAccess([], [], 0);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateTargetingRulesWrapper = async (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => {
        setOperationLoading(true);
        try {
            await onUpdateTargetingRules(targetingRules, removeTargetingRules);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearTargetingRulesWrapper = async () => {
        setOperationLoading(true);
        try {
            await onUpdateTargetingRules(undefined, true);
        } finally {
            setOperationLoading(false);
        }
    };

    const handleScheduleWrapper = async (flag: FeatureFlagDto, enableOn: string, disableOn?: string) => {
        setOperationLoading(true);
        try {
            await onSchedule(flag, enableOn, disableOn);
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
        expirationDate?: string;
        notes?: string;
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

            await onEvaluateFlag(
                flag.key,
                testUserId || undefined,
                testTenantId || undefined,
                attributes
            );
        } catch (error) {
            console.error('Failed to evaluate flag:', error);
        }
    };

    const components = parseStatusComponents(flag);
    const isEnabled = components.baseStatus === 'Enabled';

    const targetingRules = parseTargetingRules(flag.targetingRules);

    const shouldShowUserAccessIndicator = flag.modes?.includes(EvaluationMode.UserRolloutPercentage) || flag.modes?.includes(EvaluationMode.UserTargeted);
    const shouldShowTenantAccessIndicator = flag.modes?.includes(EvaluationMode.TenantRolloutPercentage) || flag.modes?.includes(EvaluationMode.TenantTargeted);
    const shouldShowTargetingRulesIndicator = flag.modes?.includes(EvaluationMode.TargetingRules) || targetingRules.length > 0;

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div className="flex justify-between items-start mb-4">
                <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{flag.name}</h3>
                        <div className="flex items-center gap-1">
                            <button
                                onClick={handleToggle}
                                disabled={operationLoading}
                                className={`p-2 rounded-md transition-colors font-medium shadow-sm ${isEnabled
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

            {onEvaluateFlag && showEvaluation && (
                <div className="mb-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
                    <h4 className="font-medium text-blue-900 mb-3">Test Flag Evaluation</h4>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mb-3">
                        <div>
                            <label className="block text-xs font-medium text-blue-700 mb-1">User ID (optional)</label>
                            <input
                                type="text"
                                value={testUserId}
                                onChange={(e) => setTestUserId(e.target.value)}
                                placeholder="user123"
                                className="w-full px-2 py-1 text-xs border border-blue-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-500"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-blue-700 mb-1">Tenant ID (optional)</label>
                            <input
                                type="text"
                                value={testTenantId}
                                onChange={(e) => setTestTenantId(e.target.value)}
                                placeholder="tenant456"
                                className="w-full px-2 py-1 text-xs border border-blue-300 rounded focus:outline-none focus:ring-1 focus:ring-blue-500"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-blue-700 mb-1">Attributes (JSON)</label>
                            <input
                                type="text"
                                value={testAttributes}
                                onChange={(e) => setTestAttributes(e.target.value)}
                                placeholder='{"country": "US"}'
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

            <ExpirationWarning flag={flag} />
            <SchedulingStatusIndicator flag={flag} />
            <TimeWindowStatusIndicator flag={flag} />
            {shouldShowUserAccessIndicator && <UserAccessControlStatusIndicator flag={flag} />}
            {shouldShowTenantAccessIndicator && <TenantAccessControlStatusIndicator flag={flag} />}
            {shouldShowTargetingRulesIndicator && <TargetingRulesStatusIndicator flag={flag} />}

            <PermanentFlagWarning flag={flag} />

            <FlagEditSection
                flag={flag}
                onUpdateFlag={handleUpdateFlagWrapper}
                operationLoading={operationLoading}
            />

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

            <TenantAccessSection
                flag={flag}
                onUpdateTenantAccess={handleUpdateTenantAccessWrapper}
                onClearTenantAccess={handleClearTenantAccessWrapper}
                operationLoading={operationLoading}
            />

            <TargetingRulesSection
                flag={flag}
                onUpdateTargetingRules={handleUpdateTargetingRulesWrapper}
                onClearTargetingRules={handleClearTargetingRulesWrapper}
                operationLoading={operationLoading}
            />

            <FlagMetadata flag={flag} />
        </div>
    );
};