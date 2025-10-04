import { useState, useEffect } from 'react';
import { Trash2, Eye, EyeOff, Play, Loader2, CheckCircle, XCircle, AlertCircle } from 'lucide-react';
import type { FeatureFlagDto, EvaluationResult, TargetingRule } from '../services/apiService';
import { parseTargetingRules, EvaluationMode, Scope } from '../services/apiService';
import { StatusBadge } from './StatusBadge';
import { parseStatusComponents } from '../utils/flagHelpers';
import { theme } from '../styles/theme';

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
    VariationSection,
    checkForCustomVariations
} from './flag-details/VariationComponents';
import {
    ExpirationWarning,
    PermanentFlagWarning,
    FlagMetadata,
    FlagEditSection
} from './flag-details/UtilityComponents';
import { ApiError } from '../services/apiService';

interface FlagDetailsProps {
    flag: FeatureFlagDto;
    onToggle: (flag: FeatureFlagDto) => Promise<void>;
    onUpdateUserAccess: (allowedUsers?: string[], blockedUsers?: string[], percentage?: number) => Promise<void>;
    onUpdateTenantAccess: (allowedTenants?: string[], blockedTenants?: string[], percentage?: number) => Promise<void>;
    onUpdateTargetingRules: (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => Promise<void>;
    onUpdateVariations?: (variations: Record<string, any>, defaultVariation: string) => Promise<void>;
    onClearVariations?: () => Promise<void>;
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

// Error display component
const ErrorAlert: React.FC<{ message: string; onDismiss: () => void }> = ({ message, onDismiss }) => (
    <div className={`mb-4 p-3 ${theme.danger[50]} ${theme.danger.border[200]} border rounded-lg flex items-start gap-2`}>
        <AlertCircle className={`w-5 h-5 ${theme.danger.text[600]} flex-shrink-0 mt-0.5`} />
        <div className="flex-1">
            <p className={`text-sm ${theme.danger.text[800]}`}>{message}</p>
        </div>
        <button
            onClick={onDismiss}
            className={`${theme.danger.text[500]} hover:${theme.danger.text[600]} transition-colors`}
        >
            <XCircle className="w-4 h-4" />
        </button>
    </div>
);

export const FlagDetails: React.FC<FlagDetailsProps> = ({
    flag,
    onToggle,
    onUpdateUserAccess,
    onUpdateTenantAccess,
    onUpdateTargetingRules,
    onUpdateVariations,
    onClearVariations,
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
    const [evaluationError, setEvaluationError] = useState<string | null>(null);
    
    // Component-specific error states
    const [userAccessError, setUserAccessError] = useState<string | null>(null);
    const [tenantAccessError, setTenantAccessError] = useState<string | null>(null);
    const [targetingRulesError, setTargetingRulesError] = useState<string | null>(null);
    const [variationError, setVariationError] = useState<string | null>(null);
    const [scheduleError, setScheduleError] = useState<string | null>(null);
    const [timeWindowError, setTimeWindowError] = useState<string | null>(null);
    const [flagEditError, setFlagEditError] = useState<string | null>(null);

    useEffect(() => {
        setShowEvaluation(false);
        setTestUserId('');
        setTestTenantId('');
        setTestAttributes('{}');
        setEvaluationError(null);
        // Clear all component errors when flag changes
        setUserAccessError(null);
        setTenantAccessError(null);
        setTargetingRulesError(null);
        setVariationError(null);
        setScheduleError(null);
        setTimeWindowError(null);
        setFlagEditError(null);
    }, [flag.key]);

    const extractErrorMessage = (error: any): string => {
        if (error instanceof ApiError) {
            return error.detail || error.message;
        }
        return error?.message || 'An unexpected error occurred';
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

    const handleUpdateUserAccessWrapper = async (allowedUsers?: string[], blockedUsers?: string[], percentage?: number) => {
        setOperationLoading(true);
        setUserAccessError(null);
        try {
            await onUpdateUserAccess(allowedUsers, blockedUsers, percentage);
        } catch (error: any) {
            setUserAccessError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearUserAccessWrapper = async () => {
        setOperationLoading(true);
        setUserAccessError(null);
        try {
            await onUpdateUserAccess([], [], 100);
        } catch (error: any) {
            setUserAccessError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateTenantAccessWrapper = async (allowedTenants?: string[], blockedTenants?: string[], percentage?: number) => {
        setOperationLoading(true);
        setTenantAccessError(null);
        try {
            await onUpdateTenantAccess(allowedTenants, blockedTenants, percentage);
        } catch (error: any) {
            setTenantAccessError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearTenantAccessWrapper = async () => {
        setOperationLoading(true);
        setTenantAccessError(null);
        try {
            await onUpdateTenantAccess([], [], 100);
        } catch (error: any) {
            setTenantAccessError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateTargetingRulesWrapper = async (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => {
        setOperationLoading(true);
        setTargetingRulesError(null);
        try {
            await onUpdateTargetingRules(targetingRules, removeTargetingRules);
        } catch (error: any) {
            setTargetingRulesError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearTargetingRulesWrapper = async () => {
        setOperationLoading(true);
        setTargetingRulesError(null);
        try {
            await onUpdateTargetingRules(undefined, true);
        } catch (error: any) {
            setTargetingRulesError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleScheduleWrapper = async (flag: FeatureFlagDto, enableOn: string, disableOn?: string) => {
        setOperationLoading(true);
        setScheduleError(null);
        try {
            await onSchedule(flag, enableOn, disableOn);
        } catch (error: any) {
            setScheduleError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearScheduleWrapper = async () => {
        setOperationLoading(true);
        setScheduleError(null);
        try {
            await onClearSchedule(flag);
        } catch (error: any) {
            setScheduleError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateTimeWindowWrapper = async (flag: FeatureFlagDto, timeWindowData: any) => {
        setOperationLoading(true);
        setTimeWindowError(null);
        try {
            await onUpdateTimeWindow(flag, timeWindowData);
        } catch (error: any) {
            setTimeWindowError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleClearTimeWindowWrapper = async () => {
        setOperationLoading(true);
        setTimeWindowError(null);
        try {
            await onClearTimeWindow(flag);
        } catch (error: any) {
            setTimeWindowError(extractErrorMessage(error));
            throw error;
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
        setFlagEditError(null);
        try {
            await onUpdateFlag(flag, updates);
        } catch (error: any) {
            setFlagEditError(extractErrorMessage(error));
            throw error;
        } finally {
            setOperationLoading(false);
        }
    };

    const handleUpdateVariationsWrapper = async (variations: Record<string, any>, defaultVariation: string) => {
        setVariationError(null);
        try {
            await onUpdateVariations?.(variations, defaultVariation);
        } catch (error: any) {
            setVariationError(extractErrorMessage(error));
            throw error;
        }
    };

    const handleClearVariationsWrapper = async () => {
        setVariationError(null);
        try {
            await onClearVariations?.();
        } catch (error: any) {
            setVariationError(extractErrorMessage(error));
            throw error;
        }
    };

    const handleEvaluate = async () => {
        if (!onEvaluateFlag) return;

        try {
            setEvaluationError(null);
            let attributes: Record<string, any> | undefined;
            if (testAttributes.trim()) {
                try {
                    attributes = JSON.parse(testAttributes);
                } catch (e) {
                    setEvaluationError('Invalid JSON in attributes field');
                    return;
                }
            }

            await onEvaluateFlag(
                flag.key,
                testUserId || undefined,
                testTenantId || undefined,
                attributes
            );
        } catch (error: any) {
            console.error('Failed to evaluate flag:', error);
            setEvaluationError(extractErrorMessage(error));
        }
    };

    const components = parseStatusComponents(flag);
    const isEnabled = components.baseStatus === 'Enabled';

    const targetingRules = parseTargetingRules(flag.targetingRules);

    const shouldShowUserAccessIndicator = flag.modes?.includes(EvaluationMode.UserRolloutPercentage) || flag.modes?.includes(EvaluationMode.UserTargeted);
    const shouldShowTenantAccessIndicator = flag.modes?.includes(EvaluationMode.TenantRolloutPercentage) || flag.modes?.includes(EvaluationMode.TenantTargeted);
    const shouldShowTargetingRulesIndicator = flag.modes?.includes(EvaluationMode.TargetingRules) || targetingRules.length > 0;
    const shouldShowVariationIndicator = checkForCustomVariations(flag);

    return (
        <div className={`bg-white rounded-lg shadow-sm ${theme.neutral.border[200]} border p-6`}>
            <div className="flex justify-between items-start mb-4">
                <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                        <h3 className={`text-lg font-semibold ${theme.neutral.text[900]}`}>{flag.name}</h3>
                        <div className="flex items-center gap-1">
                            <button
                                onClick={handleToggle}
                                disabled={operationLoading}
                                className={`p-2 rounded-md transition-colors font-medium shadow-sm ${isEnabled
                                    ? `${theme.warning[100]} ${theme.warning.text[700]} hover:bg-amber-200 ${theme.warning.border[300]} border`
                                    : `${theme.success[100]} ${theme.success.text[700]} ${theme.success.hover.bg700} hover:bg-green-200 ${theme.success.border[300]} border`
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
                                    className={`p-2 rounded-md transition-colors font-medium shadow-sm ${theme.primary[100]} ${theme.primary.text[700]} hover:bg-blue-200 ${theme.primary.border[300]} border`}
                                    title="Test Flag Evaluation"
                                >
                                    <Play className="w-4 h-4" />
                                </button>
                            )}

                            {!flag.isPermanent && (
                                <button
                                    onClick={() => onDelete(flag.key)}
                                    className={`p-2 ${theme.danger.text[600]} hover:bg-red-50 rounded-md transition-colors border border-transparent hover:border-red-200`}
                                    title="Delete Flag"
                                >
                                    <Trash2 className="w-4 h-4" />
                                </button>
                            )}
                        </div>
                    </div>
                    <p className={`text-sm ${theme.neutral.text[500]} font-mono`}>{flag.key}</p>
                    {flag.scope === Scope.Application && (flag.applicationName || flag.applicationVersion) && (
                        <div className={`mt-1 flex items-center gap-2 text-xs ${theme.neutral.text[600]}`}>
                            <span className="font-medium">Scope:</span>
                            <span className={`px-2 py-0.5 ${theme.primary[50]} ${theme.primary.text[700]} rounded ${theme.primary.border[200]} border`}>
                                Application
                            </span>
                            {flag.applicationName && (
                                <>
                                    <span className={theme.neutral.text[400]}>|</span>
                                    <span className="font-medium">App:</span>
                                    <span>{flag.applicationName}</span>
                                </>
                            )}
                            {flag.applicationVersion && (
                                <>
                                    <span className={theme.neutral.text[400]}>|</span>
                                    <span className="font-medium">Version:</span>
                                    <span>{flag.applicationVersion}</span>
                                </>
                            )}
                        </div>
                    )}
                    {flag.scope === Scope.Global && (
                        <div className={`mt-1 flex items-center gap-2 text-xs ${theme.neutral.text[600]}`}>
                            <span className="font-medium">Scope:</span>
                            <span className={`px-2 py-0.5 ${theme.success[50]} ${theme.success.text[700]} rounded ${theme.success.border[200]} border`}>
                                Global
                            </span>
                        </div>
                    )}
                </div>
                <div className="flex items-center gap-2">
                    <StatusBadge flag={flag} showDescription={true} />
                </div>
            </div>

            <p className={`${theme.neutral.text[600]} mb-6`}>{flag.description || 'No description provided'}</p>

            {onEvaluateFlag && showEvaluation && (
                <div className={`mb-6 p-4 ${theme.primary[50]} ${theme.primary.border[200]} border rounded-lg`}>
                    <h4 className={`font-medium ${theme.primary.text[900]} mb-3`}>Test Flag Evaluation</h4>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mb-3">
                        <div>
                            <label className={`block text-xs font-medium ${theme.primary.text[700]} mb-1`}>User ID (optional)</label>
                            <input
                                type="text"
                                value={testUserId}
                                onChange={(e) => setTestUserId(e.target.value)}
                                placeholder="user123"
                                className={`w-full px-2 py-1 text-xs ${theme.primary.border[300]} border rounded focus:outline-none focus:ring-1 focus:ring-blue-500`}
                            />
                        </div>
                        <div>
                            <label className={`block text-xs font-medium ${theme.primary.text[700]} mb-1`}>Tenant ID (optional)</label>
                            <input
                                type="text"
                                value={testTenantId}
                                onChange={(e) => setTestTenantId(e.target.value)}
                                placeholder="tenant456"
                                className={`w-full px-2 py-1 text-xs ${theme.primary.border[300]} border rounded focus:outline-none focus:ring-1 focus:ring-blue-500`}
                            />
                        </div>
                        <div>
                            <label className={`block text-xs font-medium ${theme.primary.text[700]} mb-1`}>Attributes (JSON)</label>
                            <input
                                type="text"
                                value={testAttributes}
                                onChange={(e) => setTestAttributes(e.target.value)}
                                placeholder='{"country": "US"}'
                                className={`w-full px-2 py-1 text-xs ${theme.primary.border[300]} border rounded focus:outline-none focus:ring-1 focus:ring-blue-500`}
                            />
                        </div>
                    </div>

                    <div className="flex items-center gap-3">
                        <button
                            onClick={handleEvaluate}
                            disabled={evaluationLoading}
                            className={`flex items-center gap-1 px-3 py-1 text-xs ${theme.primary[600]} text-white rounded ${theme.primary.hover.bg700} disabled:opacity-50 disabled:cursor-not-allowed`}
                        >
                            {evaluationLoading ? (
                                <Loader2 className="w-3 h-3 animate-spin" />
                            ) : (
                                <Play className="w-3 h-3" />
                            )}
                            Evaluate
                        </button>
                        <button
                            onClick={() => setShowEvaluation(false)}
                            disabled={operationLoading}
                            className={`px-3 py-1 ${theme.neutral[300]} ${theme.neutral.text[700]} rounded text-sm ${theme.neutral.hover.bg400} disabled:opacity-50`}
                            data-testid="cancel-schedule-button"
                        >
                            Cancel
                        </button>

                        {evaluationResult && (
                            <div className="flex items-center gap-1 text-xs">
                                {evaluationResult.isEnabled ? (
                                    <CheckCircle className={`w-3 h-3 ${theme.success.text[600]}`} />
                                ) : (
                                    <XCircle className={`w-3 h-3 ${theme.danger.text[600]}`} />
                                )}
                                <span className={evaluationResult.isEnabled ? theme.success.text[700] : theme.danger.text[700]}>
                                    {evaluationResult.isEnabled ? 'Enabled' : 'Disabled'}
                                </span>
                                {evaluationResult.reason && (
                                    <span className={theme.primary.text[600]}>({evaluationResult.reason})</span>
                                )}
                                {evaluationResult.variation && evaluationResult.variation !== 'default' && (
                                    <span className={theme.primary.text[600]}>- Variation: {evaluationResult.variation}</span>
                                )}
                            </div>
                        )}
                    </div>

                    {evaluationError && (
                        <div className={`mt-3 p-2 ${theme.danger[50]} ${theme.danger.border[200]} border rounded text-xs ${theme.danger.text[700]}`}>
                            {evaluationError}
                        </div>
                    )}
                </div>
            )}

            <ExpirationWarning flag={flag} />
            <SchedulingStatusIndicator flag={flag} />
            <TimeWindowStatusIndicator flag={flag} />
            {shouldShowUserAccessIndicator && <UserAccessControlStatusIndicator flag={flag} />}
            {shouldShowTenantAccessIndicator && <TenantAccessControlStatusIndicator flag={flag} />}
            {shouldShowTargetingRulesIndicator && <TargetingRulesStatusIndicator flag={flag} />}

            <PermanentFlagWarning flag={flag} />

            {flagEditError && <ErrorAlert message={flagEditError} onDismiss={() => setFlagEditError(null)} />}
            <FlagEditSection
                flag={flag}
                onUpdateFlag={handleUpdateFlagWrapper}
                operationLoading={operationLoading}
            />

            {scheduleError && <ErrorAlert message={scheduleError} onDismiss={() => setScheduleError(null)} />}
            <SchedulingSection
                flag={flag}
                onSchedule={handleScheduleWrapper}
                onClearSchedule={handleClearScheduleWrapper}
                operationLoading={operationLoading}
            />

            {timeWindowError && <ErrorAlert message={timeWindowError} onDismiss={() => setTimeWindowError(null)} />}
            <TimeWindowSection
                flag={flag}
                onUpdateTimeWindow={handleUpdateTimeWindowWrapper}
                onClearTimeWindow={handleClearTimeWindowWrapper}
                operationLoading={operationLoading}
            />

            {userAccessError && <ErrorAlert message={userAccessError} onDismiss={() => setUserAccessError(null)} />}
            <UserAccessSection
                flag={flag}
                onUpdateUserAccess={handleUpdateUserAccessWrapper}
                onClearUserAccess={handleClearUserAccessWrapper}
                operationLoading={operationLoading}
            />

            {tenantAccessError && <ErrorAlert message={tenantAccessError} onDismiss={() => setTenantAccessError(null)} />}
            <TenantAccessSection
                flag={flag}
                onUpdateTenantAccess={handleUpdateTenantAccessWrapper}
                onClearTenantAccess={handleClearTenantAccessWrapper}
                operationLoading={operationLoading}
            />

            {targetingRulesError && <ErrorAlert message={targetingRulesError} onDismiss={() => setTargetingRulesError(null)} />}
            <TargetingRulesSection
                flag={flag}
                onUpdateTargetingRules={handleUpdateTargetingRulesWrapper}
                onClearTargetingRules={handleClearTargetingRulesWrapper}
                operationLoading={operationLoading}
            />

            {variationError && <ErrorAlert message={variationError} onDismiss={() => setVariationError(null)} />}
            <VariationSection
                flag={flag}
                onUpdateVariations={handleUpdateVariationsWrapper}
                onClearVariations={handleClearVariationsWrapper}
                operationLoading={operationLoading}
            />

            <FlagMetadata flag={flag} />
        </div>
    );
};