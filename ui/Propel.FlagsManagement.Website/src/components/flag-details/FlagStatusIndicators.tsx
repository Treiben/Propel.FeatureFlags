import type { FeatureFlagDto } from '../../services/apiService';

interface FlagStatusIndicatorsProps {
    flag: FeatureFlagDto;
}

export const FlagStatusIndicators: React.FC<FlagStatusIndicatorsProps> = ({ flag }) => {
    const modes = flag.evaluationModes || [];
    
    // Map evaluation modes to readable names
    const getModeNames = (modes: number[]): string[] => {
        const modeMap: Record<number, string> = {
            0: 'Disabled',
            1: 'Enabled',
            2: 'Scheduled',
            3: 'TimeWindow',
            4: 'UserTargeted',
            5: 'UserRollout',
            6: 'TenantRollout',
			7: 'TenantTargeted',
        };
        
        return modes.map(mode => modeMap[mode]).filter(Boolean);
    };
    
    const modeNames = getModeNames(modes);
    
    if (modeNames.length === 0) return null;
    
    return (
        <>
            {modeNames.map((mode, index) => (
                <span
                    key={index}
                    className="px-2 py-1 bg-gray-100 text-gray-700 rounded-full text-xs font-medium"
                >
                    {mode}
                </span>
            ))}
        </>
    );
};