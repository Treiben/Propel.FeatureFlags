import type { FeatureFlagDto } from '../services/apiService';

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

// New interface for compound status information
export interface StatusComponents {
    isScheduled: boolean;
    hasTimeWindow: boolean;
    hasPercentage: boolean;
    hasUserTargeting: boolean;
	hasTenantTargeting: boolean;
    baseStatus: 'Enabled' | 'Disabled';
}

// Helper function to determine status from evaluationModes array
export const getStatusFromEvaluationModes = (modes: number[]): string => {
    if (!modes || modes.length === 0) return 'Disabled';
    
    // Check for specific combinations
    const hasDisabled = modes.includes(0);
    const hasEnabled = modes.includes(1);
    const hasScheduled = modes.includes(2);
    const hasTimeWindow = modes.includes(3);
    const hasUserTargeted = modes.includes(4);
    const hasUserRollout = modes.includes(5);
    const hasTenantRollout = modes.includes(6);
	const hasTenantTargeted = modes.includes(7);
    
    // If only enabled
    if (hasEnabled && modes.length === 1) return 'Enabled';
    
    // If only disabled
    if (hasDisabled && modes.length === 1) return 'Disabled';
    
    // Build compound status
    const features: string[] = [];
    if (hasScheduled) features.push('Scheduled');
    if (hasTimeWindow) features.push('TimeWindow');
    if (hasUserRollout || hasTenantRollout) features.push('Percentage');
    if (hasUserTargeted) features.push('UserTargeted');
    if (hasTenantTargeted) features.push('TenantTargeted');
    
    return features.length > 0 ? features.join('With') : 'Disabled';
};

// Helper function to parse compound status into components - Updated to work with evaluationModes
export const parseStatusComponents = (flag: FeatureFlagDto): StatusComponents => {
    const modes = flag.evaluationModes || [];
    
    const components: StatusComponents = {
        isScheduled: modes.includes(2), // FlagEvaluationMode.Scheduled
        hasTimeWindow: modes.includes(3), // FlagEvaluationMode.TimeWindow
        hasPercentage: modes.includes(5) || modes.includes(6), // UserRolloutPercentage or TenantRolloutPercentage
        hasUserTargeting: modes.includes(4), // FlagEvaluationMode.UserTargeted
        hasTenantTargeting: modes.includes(7), // FlagEvaluationMode.TenantTargeted
        baseStatus: modes.includes(1) ? 'Enabled' : 'Disabled' // FlagEvaluationMode.Enabled
    };

    return components;
};

// Helper function to get a human-readable status description
export const getStatusDescription = (flag: FeatureFlagDto): string => {
    const components = parseStatusComponents(flag);
    const features: string[] = [];

    if (components.baseStatus === 'Enabled') return 'Enabled';
    if (components.baseStatus === 'Disabled' && !components.isScheduled
        && !components.hasTimeWindow && !components.hasPercentage && !components.hasUserTargeting && !components.hasTenantTargeting) {
        return 'Disabled';
    }

    if (components.isScheduled) features.push('Scheduled');
    if (components.hasTimeWindow) features.push('Time Window');
    if (components.hasPercentage) features.push('Percentage');
    if (components.hasUserTargeting) features.push('User Targeted');
    if (components.hasTenantTargeting) features.push('Tenant Targeted');

    return features.join(' + ');
};

// Helper function to check if a flag is currently enabled due to scheduling
export const isScheduledActive = (flag: FeatureFlagDto): boolean => {
    const components = parseStatusComponents(flag);
    if (!components.isScheduled) return false;
    
    const now = new Date();
    const enableDate = flag.scheduledEnableDate ? new Date(flag.scheduledEnableDate) : null;
    const disableDate = flag.scheduledDisableDate ? new Date(flag.scheduledDisableDate) : null;

    // If there's an enable date and it's in the past
    if (enableDate && enableDate <= now) {
        // If there's no disable date, it's active
        if (!disableDate) return true;
        // If there's a disable date and it's in the future, it's active
        if (disableDate > now) return true;
    }

    return false;
};

// Helper function to check if a TimeWindow flag is currently active
export const isTimeWindowActive = (flag: FeatureFlagDto): boolean => {
    const components = parseStatusComponents(flag);
    if (!components.hasTimeWindow) return false;
    
    const now = new Date();
    const timeZone = flag.timeZone || 'UTC';
    
    try {
        // Convert current time to the flag's timezone
        const nowInTimeZone = new Date(now.toLocaleString("en-US", { timeZone }));
        const currentDayOfWeek = nowInTimeZone.getDay(); // 0 = Sunday, 1 = Monday, etc.
        const currentTime = nowInTimeZone.toTimeString().slice(0, 8); // HH:MM:SS format
        
        // Check if current day is in the allowed window days (now using numbers)
        if (flag.windowDays && flag.windowDays.length > 0 && !flag.windowDays.includes(currentDayOfWeek)) {
            return false;
        }
        
        // Check if current time is within the window
        if (flag.windowStartTime && flag.windowEndTime) {
            const startTime = flag.windowStartTime;
            const endTime = flag.windowEndTime;
            
            if (startTime <= endTime) {
                // Same day window (e.g., 09:00 - 17:00)
                return currentTime >= startTime && currentTime <= endTime;
            } else {
                // Overnight window (e.g., 22:00 - 06:00)
                return currentTime >= startTime || currentTime <= endTime;
            }
        }
        
        return true;
    } catch (error) {
        console.error('Error checking time window:', error);
        return false;
    }
};

// Helper function to get time window status information
export const getTimeWindowStatus = (flag: FeatureFlagDto): TimeWindowStatus => {
    const components = parseStatusComponents(flag);
    if (!components.hasTimeWindow) {
        return { isActive: false, phase: 'none' };
    }

    const isActive = isTimeWindowActive(flag);
    
    if (!flag.windowStartTime || !flag.windowEndTime) {
        return { 
            isActive: false, 
            phase: 'inactive', 
            reason: 'Time window not properly configured' 
        };
    }
    
    if (flag.windowDays && flag.windowDays.length > 0) {
        const now = new Date();
        const timeZone = flag.timeZone || 'UTC';
        const currentDayOfWeek = new Date(now.toLocaleString("en-US", { timeZone })).getDay();
        const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
        
        if (!flag.windowDays.includes(currentDayOfWeek)) {
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

// Helper function to check if a flag has expired
export const isExpired = (flag: FeatureFlagDto): boolean => {
    if (!flag.expirationDate) return false;
    
    try {
        const expirationDate = new Date(flag.expirationDate);
        const now = new Date();
        
        // Ensure both dates are valid
        if (isNaN(expirationDate.getTime()) || isNaN(now.getTime())) {
            console.warn('Invalid date in expiration check:', {
                expirationDate: flag.expirationDate,
                parsedDate: expirationDate.toISOString(),
                now: now.toISOString()
            });
            return false;
        }
        
        const isExpiredFlag = expirationDate <= now;
        
        // Add debug logging to help troubleshoot
        console.debug('Expiration check:', {
            flagKey: flag.key,
            expirationDate: expirationDate.toISOString(),
            now: now.toISOString(),
            isExpired: isExpiredFlag,
            timeDifference: expirationDate.getTime() - now.getTime()
        });
        
        return isExpiredFlag;
    } catch (error) {
        console.error('Error checking expiration:', error, { expirationDate: flag.expirationDate });
        return false;
    }
};

// Helper function to get schedule status information
export const getScheduleStatus = (flag: FeatureFlagDto): ScheduleStatus => {
    const components = parseStatusComponents(flag);
    if (!components.isScheduled) {
        return { isActive: false, phase: 'none' };
    }

    const now = new Date();
    const enableDate = flag.scheduledEnableDate ? new Date(flag.scheduledEnableDate) : null;
    const disableDate = flag.scheduledDisableDate ? new Date(flag.scheduledDisableDate) : null;

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

// Updated function to get status color based on primary feature
export const getStatusColor = (flag: FeatureFlagDto): string => {
    const components = parseStatusComponents(flag);

    // Base statuses
    if (components.baseStatus === 'Enabled') {
        return 'bg-green-100 text-green-800';
    }
    if (components.baseStatus === 'Disabled' && !components.isScheduled && !components.hasTimeWindow && !components.hasPercentage && !components.hasUserTargeting) {
        return 'bg-red-100 text-red-800';
    }

    // For compound statuses, prioritize based on most prominent feature
    if (components.isScheduled) {
        return 'bg-blue-100 text-blue-800';
    }
    if (components.hasTimeWindow) {
        return 'bg-indigo-100 text-indigo-800';
    }
    if (components.hasPercentage) {
        return 'bg-yellow-100 text-yellow-800';
    }
    if (components.hasUserTargeting) {
        return 'bg-purple-100 text-purple-800';
    }

    return 'bg-gray-100 text-gray-800';
};

export const formatDate = (dateString?: string): string => {
    if (!dateString) return 'Not set';
    return new Date(dateString).toLocaleString();
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

// Helper function to safely check if tags exist and have content
export const hasValidTags = (tags: Record<string, string> | undefined | null): boolean => {
    return tags != null && typeof tags === 'object' && Object.keys(tags).length > 0;
};

// Helper function to safely get tag entries
export const getTagEntries = (tags: Record<string, string> | undefined | null): [string, string][] => {
    if (!hasValidTags(tags)) return [];
    return Object.entries(tags!);
};

// Helper function to convert DayOfWeek numbers to day names
export const getDayName = (dayOfWeek: number): string => {
    const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    return dayNames[dayOfWeek] || 'Unknown';
};

// Helper function to convert day names to DayOfWeek numbers
export const getDayOfWeekNumber = (dayName: string): number => {
    const dayMap: Record<string, number> = {
        'Sunday': 0, 'Monday': 1, 'Tuesday': 2, 'Wednesday': 3,
        'Thursday': 4, 'Friday': 5, 'Saturday': 6
    };
    return dayMap[dayName] ?? -1;
};