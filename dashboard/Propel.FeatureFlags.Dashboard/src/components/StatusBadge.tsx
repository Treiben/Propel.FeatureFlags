import { Eye, EyeOff, Calendar, Percent, Users, Building, Clock, Settings, Plus, Target } from 'lucide-react';
import { getStatusColor, parseStatusComponents, getStatusDescription } from '../utils/flagHelpers';
import type { FeatureFlagDto } from '../services/apiService';
import type { JSX } from 'react';

interface StatusBadgeProps {
    flag: FeatureFlagDto;
    className?: string;
    showIcons?: boolean;
    showDescription?: boolean;
}

const getStatusIcons = (flag: FeatureFlagDto): JSX.Element[] => {
    const components = parseStatusComponents(flag);
    const icons: JSX.Element[] = [];

    // Base status
    if (components.baseStatus === 'Enabled') {
        icons.push(<Eye key="enabled" className="w-3 h-3" />);
    } else if (components.baseStatus === 'Disabled'
        && !components.isScheduled
        && !components.hasTimeWindow
        && !components.hasPercentage
        && !components.hasUserTargeting
        && !components.hasTenantTargeting
        && !components.hasTargetingRules) {
        icons.push(<EyeOff key="disabled" className="w-3 h-3" />);
    }

    // Additional features
    if (components.isScheduled) {
        icons.push(<Calendar key="scheduled" className="w-3 h-3" />);
    }
    if (components.hasTimeWindow) {
        icons.push(<Clock key="timewindow" className="w-3 h-3" />);
    }
    if (components.hasPercentage) {
        icons.push(<Percent key="percentage" className="w-3 h-3" />);
    }
    if (components.hasUserTargeting) {
        icons.push(<Users key="users" className="w-3 h-3" />);
    }
    if (components.hasTenantTargeting) {
        icons.push(<Building key="tenants" className="w-3 h-3" />);
    }
    if (components.hasTargetingRules) {
        icons.push(<Target key="targeting-rules" className="w-3 h-3" />);
    }

    // If no specific icons, show settings
    if (icons.length === 0) {
        icons.push(<Settings key="default" className="w-3 h-3" />);
    }

    return icons;
};

const renderIconsWithSeparator = (icons: JSX.Element[]): JSX.Element => {
    if (icons.length <= 1) {
        return <>{icons}</>;
    }

    return (
        <>
            {icons.map((icon, index) => (
                <span key={index} className="inline-flex items-center">
                    {icon}
                    {index < icons.length - 1 && <Plus className="w-2 h-2 mx-0.5 opacity-60" />}
                </span>
            ))}
        </>
    );
};

export const StatusBadge: React.FC<StatusBadgeProps> = ({ 
    flag, 
    className = '', 
    showIcons = true, 
    showDescription = false 
}) => {
    const icons = getStatusIcons(flag);
    const description = showDescription ? getStatusDescription(flag) : getStatusDescription(flag);

    return (
        <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(flag)} ${className}`}>
            {showIcons && renderIconsWithSeparator(icons)}
            <span className="whitespace-nowrap">{description}</span>
        </span>
    );
};

// Compact version for space-constrained areas
export const StatusBadgeCompact: React.FC<StatusBadgeProps> = ({ flag, className = '' }) => {
    const icons = getStatusIcons(flag);
    
    return (
        <span className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-xs font-medium ${getStatusColor(flag)} ${className}`} title={getStatusDescription(flag)}>
            {renderIconsWithSeparator(icons)}
        </span>
    );
};

// Icon-only version for very compact displays
export const StatusIconOnly: React.FC<StatusBadgeProps> = ({ flag, className = '' }) => {
    const icons = getStatusIcons(flag);
    
    return (
        <span className={`inline-flex items-center gap-0.5 ${className}`} title={getStatusDescription(flag)}>
            {renderIconsWithSeparator(icons)}
        </span>
    );
};