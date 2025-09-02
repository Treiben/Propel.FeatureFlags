import type { FeatureFlagDto } from '../services/apiService';

export interface ScheduleStatus {
    isActive: boolean;
    phase: 'upcoming' | 'active' | 'expired' | 'none';
    nextAction?: string;
    nextActionTime?: Date;
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