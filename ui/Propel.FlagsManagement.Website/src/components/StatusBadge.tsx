import { Eye, EyeOff, Calendar, Percent, Users, Clock, Settings, Plus } from 'lucide-react';
import { getStatusColor, parseStatusComponents, getStatusDescription } from '../utils/flagHelpers';
import type { JSX } from 'react';

interface StatusBadgeProps {
    status: string;
    className?: string;
    showIcons?: boolean;
    showDescription?: boolean;
}

const getStatusIcons = (status: string): JSX.Element[] => {
    const components = parseStatusComponents(status);
    const icons: JSX.Element[] = [];

    // Base status
    if (components.baseStatus === 'Enabled') {
        icons.push(<Eye key="enabled" className="w-3 h-3" />);
    } else if (components.baseStatus === 'Disabled' && !components.isScheduled && !components.hasTimeWindow && !components.hasPercentage && !components.hasUserTargeting) {
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
    status, 
    className = '', 
    showIcons = true, 
    showDescription = false 
}) => {
    const icons = getStatusIcons(status);
    const description = showDescription ? getStatusDescription(status) : status;

    return (
        <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(status)} ${className}`}>
            {showIcons && renderIconsWithSeparator(icons)}
            <span className="whitespace-nowrap">{description}</span>
        </span>
    );
};

// Compact version for space-constrained areas
export const StatusBadgeCompact: React.FC<StatusBadgeProps> = ({ status, className = '' }) => {
    const icons = getStatusIcons(status);
    
    return (
        <span className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-xs font-medium ${getStatusColor(status)} ${className}`} title={getStatusDescription(status)}>
            {renderIconsWithSeparator(icons)}
        </span>
    );
};

// Icon-only version for very compact displays
export const StatusIconOnly: React.FC<StatusBadgeProps> = ({ status, className = '' }) => {
    const icons = getStatusIcons(status);
    
    return (
        <span className={`inline-flex items-center gap-0.5 ${className}`} title={getStatusDescription(status)}>
            {renderIconsWithSeparator(icons)}
        </span>
    );
};