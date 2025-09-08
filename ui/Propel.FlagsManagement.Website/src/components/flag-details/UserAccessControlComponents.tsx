import { useState, useEffect } from 'react';
import { Users, Percent, X, UserCheck, UserX } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';

interface UserAccessControlStatusIndicatorProps {
    flag: FeatureFlagDto;
}

export const UserAccessControlStatusIndicator: React.FC<UserAccessControlStatusIndicatorProps> = ({ flag }) => {
    const components = parseStatusComponents(flag);
    
    if (!components.hasPercentage && !components.hasUserTargeting) return null;

    return (
        <div className="mt-4 p-3 bg-purple-50 rounded-lg space-y-2">
            {components.hasPercentage && (
                <div className="text-sm text-purple-800">
                    <strong>{flag.userRolloutPercentage || 0}%</strong> rollout percentage enabled
                </div>
            )}
            {components.hasUserTargeting && (
                <div className="text-sm text-purple-800">
                    <strong>{flag.allowedUsers?.length || 0}</strong> explicitly allowed users, 
                    <strong> {flag.blockedUsers?.length || 0}</strong> explicitly blocked users
                </div>
            )}
        </div>
    );
};

interface UserAccessControlEditorProps {
    flag: FeatureFlagDto;
    isEditing: boolean;
    onStartEditing: () => void;
    onCancelEditing: () => void;
    onUpdateUserAccess: (allowedUsers?: string[], blockedUsers?: string[], percentage?: number) => Promise<void>;
    operationLoading: boolean;
}

export const UserAccessControlEditor: React.FC<UserAccessControlEditorProps> = ({
    flag,
    isEditing,
    onStartEditing,
    onCancelEditing,
    onUpdateUserAccess,
    operationLoading
}) => {
    const [newPercentage, setNewPercentage] = useState(flag.userRolloutPercentage || 0);
    const [allowedUsersInput, setAllowedUsersInput] = useState('');
    const [blockedUsersInput, setBlockedUsersInput] = useState('');
    const [localLoading, setLocalLoading] = useState(false);

    // Update local state when flag changes (when a different flag is selected)
    useEffect(() => {
        setNewPercentage(flag.userRolloutPercentage || 0);
        setAllowedUsersInput('');
        setBlockedUsersInput('');
    }, [flag.key, flag.userRolloutPercentage]);

    const handlePercentageSubmit = async () => {
        try {
            setLocalLoading(true);
            await onUpdateUserAccess(undefined, undefined, newPercentage);
        } catch (error) {
            console.error('Failed to set percentage:', error);
        } finally {
            setLocalLoading(false);
        }
    };

    const handleAllowUsers = async () => {
        if (!allowedUsersInput.trim()) return;
        
        try {
            setLocalLoading(true);
            const userIds = allowedUsersInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
            const currentAllowedUsers = flag.allowedUsers || [];
            const updatedAllowedUsers = [...new Set([...currentAllowedUsers, ...userIds])];
            await onUpdateUserAccess(updatedAllowedUsers, flag.blockedUsers);
            setAllowedUsersInput('');
        } catch (error) {
            console.error('Failed to enable users:', error);
        } finally {
            setLocalLoading(false);
        }
    };

    const handleBlockUsers = async () => {
        if (!blockedUsersInput.trim()) return;
        
        try {
            setLocalLoading(true);
            const userIds = blockedUsersInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
            const currentBlockedUsers = flag.blockedUsers || [];
            const updatedBlockedUsers = [...new Set([...currentBlockedUsers, ...userIds])];
            await onUpdateUserAccess(flag.allowedUsers, updatedBlockedUsers);
            setBlockedUsersInput('');
        } catch (error) {
            console.error('Failed to disable users:', error);
        } finally {
            setLocalLoading(false);
        }
    };

    const handleCancel = () => {
        setNewPercentage(flag.userRolloutPercentage || 0);
        setAllowedUsersInput('');
        setBlockedUsersInput('');
        onCancelEditing();
    };

    if (!isEditing) {
        return (
            <button
                onClick={onStartEditing}
                disabled={operationLoading}
                className="flex items-center justify-center gap-2 px-4 py-2 bg-purple-100 text-purple-700 rounded-md hover:bg-purple-200 font-medium disabled:opacity-50"
            >
                <Users className="w-4 h-4" />
                User Access Control
            </button>
        );
    }

    return (
        <div className="col-span-2 bg-purple-50 border border-purple-200 rounded-lg p-4 mb-4">
            <h4 className="font-medium text-purple-800 mb-4 flex items-center gap-2">
                <Users className="w-4 h-4" />
                User Access Control
            </h4>
            
            {/* Percentage Rollout Section */}
            <div className="mb-4 p-3 bg-white border border-purple-200 rounded">
                <div className="flex items-center gap-2 mb-2">
                    <Percent className="w-4 h-4 text-purple-600" />
                    <span className="font-medium text-purple-800">Percentage Rollout</span>
                </div>
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
                    <span className="text-sm font-medium text-purple-800 min-w-[3rem]">{newPercentage}%</span>
                    <button
                        onClick={handlePercentageSubmit}
                        disabled={localLoading || operationLoading}
                        className="px-3 py-1 bg-purple-600 text-white rounded text-sm hover:bg-purple-700 disabled:opacity-50"
                    >
                        {localLoading ? 'Applying...' : 'Apply'}
                    </button>
                </div>
            </div>

            {/* User Lists Section */}
            <div className="space-y-3">
                {/* Allow Users */}
                <div className="p-3 bg-white border border-purple-200 rounded">
                    <div className="flex items-center gap-2 mb-2">
                        <UserCheck className="w-4 h-4 text-green-600" />
                        <span className="font-medium text-purple-800">Allow Users</span>
                    </div>
                    <div className="flex gap-2">
                        <input
                            type="text"
                            value={allowedUsersInput}
                            onChange={(e) => setAllowedUsersInput(e.target.value)}
                            placeholder="user1, user2, user3..."
                            className="flex-1 border border-gray-300 rounded px-3 py-2 text-sm"
                            disabled={localLoading || operationLoading}
                        />
                        <button
                            onClick={handleAllowUsers}
                            disabled={!allowedUsersInput.trim() || localLoading || operationLoading}
                            className="px-3 py-2 bg-green-600 text-white rounded text-sm hover:bg-green-700 disabled:opacity-50"
                        >
                            Add
                        </button>
                    </div>
                    {flag.allowedUsers && flag.allowedUsers.length > 0 && (
                        <div className="mt-2">
                            <span className="text-xs text-gray-600">Currently allowed: </span>
                            <span className="text-xs text-green-700">{flag.allowedUsers.join(', ')}</span>
                        </div>
                    )}
                </div>

                {/* Block Users */}
                <div className="p-3 bg-white border border-purple-200 rounded">
                    <div className="flex items-center gap-2 mb-2">
                        <UserX className="w-4 h-4 text-red-600" />
                        <span className="font-medium text-purple-800">Block Users</span>
                    </div>
                    <div className="flex gap-2">
                        <input
                            type="text"
                            value={blockedUsersInput}
                            onChange={(e) => setBlockedUsersInput(e.target.value)}
                            placeholder="user4, user5, user6..."
                            className="flex-1 border border-gray-300 rounded px-3 py-2 text-sm"
                            disabled={localLoading || operationLoading}
                        />
                        <button
                            onClick={handleBlockUsers}
                            disabled={!blockedUsersInput.trim() || localLoading || operationLoading}
                            className="px-3 py-2 bg-red-600 text-white rounded text-sm hover:bg-red-700 disabled:opacity-50"
                        >
                            Add
                        </button>
                    </div>
                    {flag.blockedUsers && flag.blockedUsers.length > 0 && (
                        <div className="mt-2">
                            <span className="text-xs text-gray-600">Currently blocked: </span>
                            <span className="text-xs text-red-700">{flag.blockedUsers.join(', ')}</span>
                        </div>
                    )}
                </div>
            </div>

            <div className="flex gap-2 mt-4">
                <button
                    onClick={handleCancel}
                    disabled={localLoading || operationLoading}
                    className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
                >
                    Close
                </button>
            </div>
        </div>
    );
};