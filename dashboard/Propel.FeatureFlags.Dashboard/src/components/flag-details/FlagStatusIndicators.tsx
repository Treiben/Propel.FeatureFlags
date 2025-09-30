import type { FeatureFlagDto } from '../../services/apiService';
import { EvaluationMode } from '../../services/apiService';

interface FlagStatusIndicatorsProps {
    flag: FeatureFlagDto;
}

export const FlagStatusIndicators: React.FC<FlagStatusIndicatorsProps> = ({ flag }) => {
    const modes = flag.modes || [];

    const getModeNames = (modes: EvaluationMode[]): string[] => {
        const modeMap: Record<number, string> = {
            [EvaluationMode.Off]: 'Disabled',
            [EvaluationMode.On]: 'Enabled',
            [EvaluationMode.Scheduled]: 'Scheduled',
            [EvaluationMode.TimeWindow]: 'TimeWindow',
            [EvaluationMode.UserTargeted]: 'UserTargeted',
            [EvaluationMode.UserRolloutPercentage]: 'UserRollout',
            [EvaluationMode.TenantRolloutPercentage]: 'TenantRollout',
            [EvaluationMode.TenantTargeted]: 'TenantTargeted',
            [EvaluationMode.TargetingRules]: 'TargetingRules'
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