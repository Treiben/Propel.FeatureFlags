import { useState } from 'react';
import { Percent } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';

interface PercentageStatusIndicatorProps {
    flag: FeatureFlagDto;
}

export const PercentageStatusIndicator: React.FC<PercentageStatusIndicatorProps> = ({ flag }) => {
    if (flag.status !== 'Percentage') return null;

    return (
        <div className="mt-4 p-3 bg-yellow-50 rounded-lg">
            <div className="text-sm text-yellow-800">
                Currently enabled for <strong>{flag.percentageEnabled || 0}%</strong> of users
            </div>
        </div>
    );
};

interface PercentageEditorProps {
    flag: FeatureFlagDto;
    isEditing: boolean;
    onStartEditing: () => void;
    onCancelEditing: () => void;
    onSetPercentage: (flag: FeatureFlagDto, percentage: number) => Promise<void>;
    operationLoading: boolean;
}

export const PercentageEditor: React.FC<PercentageEditorProps> = ({
    flag,
    isEditing,
    onStartEditing,
    onCancelEditing,
    onSetPercentage,
    operationLoading
}) => {
    const [newPercentage, setNewPercentage] = useState(flag.percentageEnabled || 0);
    const [localLoading, setLocalLoading] = useState(false);

    const handlePercentageSubmit = async () => {
        try {
            setLocalLoading(true);
            await onSetPercentage(flag, newPercentage);
        } catch (error) {
            console.error('Failed to set percentage:', error);
        } finally {
            setLocalLoading(false);
        }
    };

    const handleCancel = () => {
        setNewPercentage(flag.percentageEnabled || 0);
        onCancelEditing();
    };

    if (!isEditing) {
        return (
            <button
                onClick={onStartEditing}
                disabled={operationLoading}
                className="flex items-center justify-center gap-2 px-4 py-2 bg-yellow-100 text-yellow-700 rounded-md hover:bg-yellow-200 font-medium disabled:opacity-50"
            >
                <Percent className="w-4 h-4" />
                Percentage Rollout
            </button>
        );
    }

    return (
        <div className="col-span-2 bg-yellow-50 border border-yellow-200 rounded-lg p-4 mb-4">
            <h4 className="font-medium text-yellow-800 mb-2">Set Percentage Rollout</h4>
            <div className="flex items-center gap-3">
                <input
                    type="range"
                    min="0"
                    max="100"
                    value={newPercentage}
                    onChange={(e) => setNewPercentage(parseInt(e.target.value))}
                    className="flex-1"
                    disabled={localLoading || operationLoading}
                />
                <span className="text-sm font-medium text-yellow-800 min-w-[3rem]">{newPercentage}%</span>
            </div>
            <div className="flex gap-2 mt-3">
                <button
                    onClick={handlePercentageSubmit}
                    disabled={localLoading || operationLoading}
                    className="px-3 py-1 bg-yellow-600 text-white rounded text-sm hover:bg-yellow-700 disabled:opacity-50"
                >
                    {localLoading ? 'Applying...' : 'Apply'}
                </button>
                <button
                    onClick={handleCancel}
                    disabled={localLoading || operationLoading}
                    className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                >
                    Cancel
                </button>
            </div>
        </div>
    );
};