import { Shield, PlayCircle, Timer, AlertCircle } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { 
    getScheduleStatus, 
    getTimeWindowStatus, 
    isExpired 
} from '../../utils/flagHelpers';

interface FlagStatusIndicatorsProps {
    flag: FeatureFlagDto;
}

export const FlagStatusIndicators: React.FC<FlagStatusIndicatorsProps> = ({ flag }) => {
    const scheduleStatus = getScheduleStatus(flag);
    const timeWindowStatus = getTimeWindowStatus(flag);
    const flagExpired = isExpired(flag);

    return (
        <>
            {flag.isPermanent && (
                <div className="flex items-center gap-1 px-2 py-1 bg-amber-100 text-amber-800 rounded-full text-xs font-medium">
                    <Shield className="w-3 h-3" />
                    Permanent
                </div>
            )}
            {scheduleStatus.isActive && (
                <div className="flex items-center gap-1 px-2 py-1 bg-green-100 text-green-700 rounded-full text-xs font-medium">
                    <PlayCircle className="w-3 h-3" />
                    Live
                </div>
            )}
            {timeWindowStatus.isActive && (
                <div className="flex items-center gap-1 px-2 py-1 bg-indigo-100 text-indigo-700 rounded-full text-xs font-medium">
                    <Timer className="w-3 h-3" />
                    Active Window
                </div>
            )}
            {flagExpired && (
                <div className="flex items-center gap-1 px-2 py-1 bg-red-100 text-red-800 rounded-full text-xs font-medium">
                    <AlertCircle className="w-3 h-3" />
                    Expired
                </div>
            )}
        </>
    );
};