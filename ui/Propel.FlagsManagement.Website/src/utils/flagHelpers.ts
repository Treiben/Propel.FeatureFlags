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

// Helper function to check if a flag is currently enabled due to scheduling
export const isScheduledActive = (flag: FeatureFlagDto): boolean => {
    if (flag.status !== 'Scheduled') return false;
    
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
    if (flag.status !== 'TimeWindow') return false;
    
    const now = new Date();
    const timeZone = flag.timeZone || 'UTC';
    
    try {
        // Convert current time to the flag's timezone
        const nowInTimeZone = new Date(now.toLocaleString("en-US", { timeZone }));
        const currentDay = nowInTimeZone.toLocaleDateString('en-US', { weekday: 'long', timeZone });
        const currentTime = nowInTimeZone.toTimeString().slice(0, 8); // HH:MM:SS format
        
        // Check if current day is in the allowed window days
        if (flag.windowDays && flag.windowDays.length > 0 && !flag.windowDays.includes(currentDay)) {
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
    if (flag.status !== 'TimeWindow') {
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
        const currentDay = now.toLocaleDateString('en-US', { weekday: 'long', timeZone });
        
        if (!flag.windowDays.includes(currentDay)) {
            return { 
                isActive: false, 
                phase: 'inactive', 
                reason: `Not active on ${currentDay}` 
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
    return new Date(flag.expirationDate) <= new Date();
};

// Helper function to get schedule status information
export const getScheduleStatus = (flag: FeatureFlagDto): ScheduleStatus => {
    if (flag.status !== 'Scheduled') {
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

export const getStatusColor = (status: string): string => {
    switch (status) {
        case 'Enabled': return 'bg-green-100 text-green-800';
        case 'Disabled': return 'bg-red-100 text-red-800';
        case 'Scheduled': return 'bg-blue-100 text-blue-800';
        case 'Percentage': return 'bg-yellow-100 text-yellow-800';
        case 'UserTargeted': return 'bg-purple-100 text-purple-800';
        case 'TimeWindow': return 'bg-indigo-100 text-indigo-800';
        default: return 'bg-gray-100 text-gray-800';
    }
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