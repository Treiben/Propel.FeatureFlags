import type { FeatureFlagDto } from '../services/apiService';
import { hasValidTargetingRulesJson, EvaluationMode } from '../services/apiService';
import { getStatusColor as getThemeStatusColor } from '../styles/theme';

export interface ScheduleStatus {
    isActive: boolean;
    phase: 'upcoming' | 'active' | 'expired' | 'none';
    nextAction?: string;
    nextActionTime?: Date;
}

export interface TimeWindowStatus {
    isActive: boolean;
    phase: 'active' | 'inactive' | 'none';
    reason?: string;
}

export interface StatusComponents {
    isScheduled: boolean;
    hasTimeWindow: boolean;
    hasPercentage: boolean;
    hasUserTargeting: boolean;
    hasTenantTargeting: boolean;
    hasTargetingRules: boolean;
    baseStatus: 'Enabled' | 'Disabled' | 'Other';
}

export const getStatusFromEvaluationModes = (modes: EvaluationMode[]): string => {
    if (!modes || modes.length === 0) return 'Disabled';

    const hasDisabled = modes.includes(EvaluationMode.Off);
    const hasEnabled = modes.includes(EvaluationMode.On);
    const hasScheduled = modes.includes(EvaluationMode.Scheduled);
    const hasTimeWindow = modes.includes(EvaluationMode.TimeWindow);
    const hasUserTargeted = modes.includes(EvaluationMode.UserTargeted);
    const hasUserRollout = modes.includes(EvaluationMode.UserRolloutPercentage);
    const hasTenantRollout = modes.includes(EvaluationMode.TenantRolloutPercentage);
    const hasTenantTargeted = modes.includes(EvaluationMode.TenantTargeted);
    const hasTargetingRules = modes.includes(EvaluationMode.TargetingRules);

    if (hasEnabled && modes.length === 1) return 'Enabled';
    if (hasDisabled && modes.length === 1) return 'Disabled';

    const features: string[] = [];
    if (hasScheduled) features.push('Scheduled');
    if (hasTimeWindow) features.push('TimeWindow');
    if (hasUserRollout || hasTenantRollout) features.push('Percentage');
    if (hasUserTargeted) features.push('UserTargeted');
    if (hasTenantTargeted) features.push('TenantTargeted');
    if (hasTargetingRules) features.push('TargetingRules');

    return features.length > 0 ? features.join('With') : 'Disabled';
};

export const hasValidTargetingRules = (targetingRules: string | any): boolean => {
    if (typeof targetingRules === 'string') {
        return hasValidTargetingRulesJson(targetingRules);
    }
    return Array.isArray(targetingRules) && targetingRules.length > 0;
};

export const parseStatusComponents = (flag: FeatureFlagDto): StatusComponents => {
    const modes = flag.modes || [];

    return {
        isScheduled: modes.includes(EvaluationMode.Scheduled),
        hasTimeWindow: modes.includes(EvaluationMode.TimeWindow),
        hasPercentage: modes.includes(EvaluationMode.UserRolloutPercentage) || modes.includes(EvaluationMode.TenantRolloutPercentage),
        hasUserTargeting: modes.includes(EvaluationMode.UserTargeted),
        hasTenantTargeting: modes.includes(EvaluationMode.TenantTargeted),
        hasTargetingRules: modes.includes(EvaluationMode.TargetingRules) || hasValidTargetingRules(flag.targetingRules),
        baseStatus: modes.includes(EvaluationMode.On) ? 'Enabled' : (modes.includes(EvaluationMode.Off) ? 'Disabled' : 'Other')
    };
};

export const getStatusDescription = (flag: FeatureFlagDto): string => {
    const components = parseStatusComponents(flag);
    const features: string[] = [];

    if (components.baseStatus === 'Enabled') return 'Enabled';
    if (components.baseStatus === 'Disabled' && !components.isScheduled
        && !components.hasTimeWindow && !components.hasPercentage
        && !components.hasUserTargeting && !components.hasTenantTargeting
        && !components.hasTargetingRules) {
        return 'Disabled';
    }

    if (components.isScheduled) features.push('Scheduled');
    if (components.hasTimeWindow) features.push('Time Window');
    if (components.hasPercentage) features.push('Percentage');
    if (components.hasUserTargeting) features.push('User Targeted');
    if (components.hasTenantTargeting) features.push('Tenant Targeted');
    if (components.hasTargetingRules) features.push('Targeting Rules');

    return features.join(' + ');
};

export const isScheduledActive = (flag: FeatureFlagDto): boolean => {
    const components = parseStatusComponents(flag);
    if (!components.isScheduled || !flag.schedule) return false;

    const now = new Date();
    const enableDate = flag.schedule.enableOnUtc ? new Date(flag.schedule.enableOnUtc) : null;
    const disableDate = flag.schedule.disableOnUtc ? new Date(flag.schedule.disableOnUtc) : null;

    if (enableDate && enableDate <= now) {
        if (!disableDate) return true;
        if (disableDate > now) return true;
    }

    return false;
};

export const isTimeWindowActive = (flag: FeatureFlagDto): boolean => {
    const components = parseStatusComponents(flag);
    if (!components.hasTimeWindow || !flag.timeWindow) return false;

    const now = new Date();
    const timeZone = flag.timeWindow.timeZone || 'UTC';

    try {
        const nowInTimeZone = new Date(now.toLocaleString("en-US", { timeZone }));
        const currentDayOfWeek = nowInTimeZone.getDay();
        const currentTime = nowInTimeZone.toTimeString().slice(0, 8);

        if (flag.timeWindow.daysActive && flag.timeWindow.daysActive.length > 0 && !flag.timeWindow.daysActive.includes(currentDayOfWeek)) {
            return false;
        }

        if (flag.timeWindow.startOn && flag.timeWindow.stopOn) {
            const startTime = flag.timeWindow.startOn;
            const endTime = flag.timeWindow.stopOn;

            if (startTime <= endTime) {
                return currentTime >= startTime && currentTime <= endTime;
            } else {
                return currentTime >= startTime || currentTime <= endTime;
            }
        }

        return true;
    } catch (error) {
        console.error('Error checking time window:', error);
        return false;
    }
};

export const getTimeWindowStatus = (flag: FeatureFlagDto): TimeWindowStatus => {
    const components = parseStatusComponents(flag);
    if (!components.hasTimeWindow) {
        return { isActive: false, phase: 'none' };
    }

    const isActive = isTimeWindowActive(flag);

    if (!flag.timeWindow?.startOn || !flag.timeWindow?.stopOn) {
        return {
            isActive: false,
            phase: 'inactive',
            reason: 'Time window not properly configured'
        };
    }

    if (flag.timeWindow.daysActive && flag.timeWindow.daysActive.length > 0) {
        const now = new Date();
        const timeZone = flag.timeWindow.timeZone || 'UTC';
        const currentDayOfWeek = new Date(now.toLocaleString("en-US", { timeZone })).getDay();
        const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

        if (!flag.timeWindow.daysActive.includes(currentDayOfWeek)) {
            return {
                isActive: false,
                phase: 'inactive',
                reason: `Not active on ${dayNames[currentDayOfWeek]}`
            };
        }
    }

    return {
        isActive,
        phase: isActive ? 'active' : 'inactive',
        reason: isActive ? 'Within time window' : 'Outside time window'
    };
};

export const isExpired = (flag: FeatureFlagDto): boolean => {
    if (!flag.expirationDate) return false;

    try {
        const expirationDate = new Date(flag.expirationDate);
        const now = new Date();

        if (isNaN(expirationDate.getTime()) || isNaN(now.getTime())) {
            console.warn('Invalid date in expiration check:', {
                expirationDate: flag.expirationDate,
                parsedDate: expirationDate.toISOString(),
                now: now.toISOString()
            });
            return false;
        }

        return expirationDate <= now;
    } catch (error) {
        console.error('Error checking expiration:', error, { expirationDate: flag.expirationDate });
        return false;
    }
};

export const getScheduleStatus = (flag: FeatureFlagDto): ScheduleStatus => {
    const components = parseStatusComponents(flag);
    if (!components.isScheduled || !flag.schedule) {
        return { isActive: false, phase: 'none' };
    }

    const now = new Date();
    const enableDate = flag.schedule.enableOnUtc ? new Date(flag.schedule.enableOnUtc) : null;
    const disableDate = flag.schedule.disableOnUtc ? new Date(flag.schedule.disableOnUtc) : null;

    if (enableDate && enableDate > now) {
        return {
            isActive: false,
            phase: 'upcoming',
            nextAction: 'Enable',
            nextActionTime: enableDate
        };
    }

    if (enableDate && enableDate <= now) {
        if (!disableDate) {
            return {
                isActive: true,
                phase: 'active',
            };
        }

        if (disableDate > now) {
            return {
                isActive: true,
                phase: 'active',
                nextAction: 'Disable',
                nextActionTime: disableDate
            };
        }

        if (disableDate <= now) {
            return {
                isActive: false,
                phase: 'expired'
            };
        }
    }

    return { isActive: false, phase: 'none' };
};

export const getStatusColor = (flag: FeatureFlagDto): string => {
    const components = parseStatusComponents(flag);

    // Determine status type
    if (components.baseStatus === 'Enabled') {
        return getThemeStatusColor('enabled');
    }
    if (components.baseStatus === 'Disabled' && !components.isScheduled && !components.hasTimeWindow
        && !components.hasPercentage && !components.hasUserTargeting && !components.hasTenantTargeting
        && !components.hasTargetingRules) {
        return getThemeStatusColor('disabled');
    }

    // Complex status - has scheduling or targeting
    if (components.isScheduled || components.hasTimeWindow || components.hasPercentage 
        || components.hasUserTargeting || components.hasTenantTargeting || components.hasTargetingRules) {
        return getThemeStatusColor('scheduled');
    }

    // Default to partial/other
    return getThemeStatusColor('partial');
};

// BUG FIX #17: Fix date formatting to display correctly in local timezone
export const formatDate = (dateString?: string): string => {
    if (!dateString) return 'Not set';
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return dateString;

        // Format in local timezone with proper formatting
        return date.toLocaleString(undefined, {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            hour12: true
        });
    } catch (error) {
        console.error('Error formatting date:', error);
        return dateString;
    }
};

export const formatTime = (timeString?: string): string => {
    if (!timeString) return 'Not set';
    return timeString;
};

export const formatRelativeTime = (date: Date): string => {
    const now = new Date();
    const diff = date.getTime() - now.getTime();
    const absDiff = Math.abs(diff);

    const minutes = Math.floor(absDiff / (1000 * 60));
    const hours = Math.floor(absDiff / (1000 * 60 * 60));
    const days = Math.floor(absDiff / (1000 * 60 * 60 * 24));

    if (days > 0) {
        return diff > 0 ? `in ${days} day${days > 1 ? 's' : ''}` : `${days} day${days > 1 ? 's' : ''} ago`;
    } else if (hours > 0) {
        return diff > 0 ? `in ${hours} hour${hours > 1 ? 's' : ''}` : `${hours} hour${hours > 1 ? 's' : ''} ago`;
    } else if (minutes > 0) {
        return diff > 0 ? `in ${minutes} minute${minutes > 1 ? 's' : ''}` : `${minutes} minute${minutes > 1 ? 's' : ''} ago`;
    } else {
        return 'now';
    }
};

export const hasValidTags = (tags: Record<string, string> | undefined | null): boolean => {
    return tags != null && typeof tags === 'object' && Object.keys(tags).length > 0;
};

export const getTagEntries = (tags: Record<string, string> | undefined | null): [string, string][] => {
    if (!hasValidTags(tags)) return [];
    return Object.entries(tags!);
};

export const getDayName = (dayOfWeek: number): string => {
    const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    return dayNames[dayOfWeek] || 'Unknown';
};

export const getDayOfWeekNumber = (dayName: string): number => {
    const dayMap: Record<string, number> = {
        'Sunday': 0, 'Monday': 1, 'Tuesday': 2, 'Wednesday': 3,
        'Thursday': 4, 'Friday': 5, 'Saturday': 6
    };
    return dayMap[dayName] ?? -1;
};