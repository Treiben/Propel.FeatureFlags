import { Eye, EyeOff, Calendar, Percent, Users, Clock, Settings } from 'lucide-react';
import { getStatusColor } from '../utils/flagHelpers';

interface StatusBadgeProps {
    status: string;
    className?: string;
}

const getStatusIcon = (status: string) => {
    switch (status) {
        case 'Enabled': return <Eye className="w-4 h-4" />;
        case 'Disabled': return <EyeOff className="w-4 h-4" />;
        case 'Scheduled': return <Calendar className="w-4 h-4" />;
        case 'Percentage': return <Percent className="w-4 h-4" />;
        case 'UserTargeted': return <Users className="w-4 h-4" />;
        case 'TimeWindow': return <Clock className="w-4 h-4" />;
        default: return <Settings className="w-4 h-4" />;
    }
};

export const StatusBadge: React.FC<StatusBadgeProps> = ({ status, className = '' }) => {
    return (
        <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(status)} ${className}`}>
            {getStatusIcon(status)}
            {status}
        </span>
    );
};