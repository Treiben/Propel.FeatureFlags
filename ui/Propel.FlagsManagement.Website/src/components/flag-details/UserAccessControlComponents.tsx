import { useState, useEffect } from 'react';
import { Users, Percent, UserCheck, UserX, X } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';

interface UserAccessControlStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const UserAccessControlStatusIndicator: React.FC<UserAccessControlStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);

	if (!components.hasPercentage && !components.hasUserTargeting) return null;

	const allowedCount = flag.allowedUsers?.length || 0;
	const blockedCount = flag.blockedUsers?.length || 0;
	const percentage = flag.userRolloutPercentage || 0;

	return (
		<div className="mb-4 p-4 bg-purple-50 border border-purple-200 rounded-lg">
			<div className="flex items-center gap-2 mb-3">
				<Users className="w-4 h-4 text-purple-600" />
				<h4 className="font-medium text-purple-900">User Access Control</h4>
			</div>

			<div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
				{/* Percentage Rollout */}
				{components.hasPercentage && (
					<div className="flex items-center gap-2">
						<Percent className="w-4 h-4 text-yellow-600" />
						<span className="font-medium">Percentage:</span>
						<span className="text-yellow-700">{percentage}% rollout</span>
					</div>
				)}

				{/* Allowed Users */}
				{components.hasUserTargeting && (
					<div className="flex items-center gap-2">
						<UserCheck className="w-4 h-4 text-green-600" />
						<span className="font-medium">Allowed:</span>
						<span className="text-green-700 font-semibold">{allowedCount} user{allowedCount !== 1 ? 's' : ''}</span>
					</div>
				)}

				{/* Blocked Users */}
				{components.hasUserTargeting && (
					<div className="flex items-center gap-2">
						<UserX className="w-4 h-4 text-red-600" />
						<span className="font-medium">Blocked:</span>
						<span className="text-red-700 font-semibold">{blockedCount} user{blockedCount !== 1 ? 's' : ''}</span>
					</div>
				)}
			</div>
		</div>
	);
};

interface UserAccessSectionProps {
	flag: FeatureFlagDto;
	onUpdateUserAccess: (allowedUsers?: string[], blockedUsers?: string[], percentage?: number) => Promise<void>;
	onClearUserAccess: () => Promise<void>;
	operationLoading: boolean;
}

// Helper function to display allowed users
const renderAllowedUsers = (users: string[]) => {
	return (
		<div className="mt-2">
			<span className="text-xs font-medium text-green-700">Allowed: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{users.slice(0, 5).map((user) => (
					<span
						key={user}
						className="inline-flex items-center px-2 py-1 text-xs bg-green-100 text-green-800 rounded-full border border-green-200"
					>
						<UserCheck className="w-3 h-3 mr-1" />
						{user}
					</span>
				))}
				{users.length > 5 && (
					<span className="text-xs text-gray-500">+{users.length - 5} more</span>
				)}
			</div>
		</div>
	);
};

// Helper function to display blocked users
const renderBlockedUsers = (users: string[]) => {
	return (
		<div className="mt-2">
			<span className="text-xs font-medium text-red-700">Blocked: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{users.slice(0, 5).map((user) => (
					<span
						key={user}
						className="inline-flex items-center px-2 py-1 text-xs bg-red-100 text-red-800 rounded-full border border-red-200"
					>
						<UserX className="w-3 h-3 mr-1" />
						{user}
					</span>
				))}
				{users.length > 5 && (
					<span className="text-xs text-gray-500">+{users.length - 5} more</span>
				)}
			</div>
		</div>
	);
};

export const UserAccessSection: React.FC<UserAccessSectionProps> = ({
	flag,
	onUpdateUserAccess,
	onClearUserAccess,
	operationLoading
}) => {
	const [editingUserAccess, setEditingUserAccess] = useState(false);
	const [userAccessData, setUserAccessData] = useState({
		percentage: flag.userRolloutPercentage || 0,
		allowedUsersInput: '',
		blockedUsersInput: ''
	});

	const components = parseStatusComponents(flag);

	// Update local state when flag changes (when a different flag is selected)
	useEffect(() => {
		setUserAccessData({
			percentage: flag.userRolloutPercentage || 0,
			allowedUsersInput: '',
			blockedUsersInput: ''
		});
	}, [flag.key, flag.userRolloutPercentage]);

	const handleUserAccessSubmit = async () => {
		try {
			// Apply percentage if changed
			if (userAccessData.percentage !== (flag.userRolloutPercentage || 0)) {
				await onUpdateUserAccess(undefined, undefined, userAccessData.percentage);
			}

			// Add allowed users if any
			if (userAccessData.allowedUsersInput.trim()) {
				const userIds = userAccessData.allowedUsersInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
				const currentAllowedUsers = flag.allowedUsers || [];
				const updatedAllowedUsers = [...new Set([...currentAllowedUsers, ...userIds])];
				await onUpdateUserAccess(updatedAllowedUsers, flag.blockedUsers);
			}

			// Add blocked users if any
			if (userAccessData.blockedUsersInput.trim()) {
				const userIds = userAccessData.blockedUsersInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
				const currentBlockedUsers = flag.blockedUsers || [];
				const updatedBlockedUsers = [...new Set([...currentBlockedUsers, ...userIds])];
				await onUpdateUserAccess(flag.allowedUsers, updatedBlockedUsers);
			}

			setEditingUserAccess(false);
		} catch (error) {
			console.error('Failed to update user access:', error);
		}
	};

	const handleClearUserAccess = async () => {
		try {
			await onClearUserAccess();
		} catch (error) {
			console.error('Failed to clear user access:', error);
		}
	};

	const hasUserAccessControl = components.hasPercentage || components.hasUserTargeting ||
		(flag.allowedUsers && flag.allowedUsers.length > 0) ||
		(flag.blockedUsers && flag.blockedUsers.length > 0);

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<h4 className="font-medium text-gray-900">User Access Control</h4>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingUserAccess(true)}
						disabled={operationLoading}
						className="text-purple-600 hover:text-purple-800 text-sm flex items-center gap-1 disabled:opacity-50"
						data-testid="manage-users-button"
					>
						<Users className="w-4 h-4" />
						Manage Users
					</button>
					{hasUserAccessControl && (
						<button
							onClick={handleClearUserAccess}
							disabled={operationLoading}
							className="text-red-600 hover:text-red-800 text-sm flex items-center gap-1 disabled:opacity-50"
							title="Clear User Access Control"
							data-testid="clear-user-access-button"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingUserAccess ? (
				<div className="bg-purple-50 border border-purple-200 rounded-lg p-4">
					<div className="space-y-4">
						{/* Percentage Rollout */}
						<div>
							<label className="block text-sm font-medium text-purple-800 mb-2">Percentage Rollout</label>
							<div className="flex items-center gap-3">
								<input
									type="range"
									min="0"
									max="100"
									value={userAccessData.percentage}
									onChange={(e) => setUserAccessData({
										...userAccessData,
										percentage: parseInt(e.target.value)
									})}
									className="flex-1"
									disabled={operationLoading}
									data-testid="percentage-slider"
								/>
								<span className="text-sm font-medium text-purple-800 min-w-[3rem]">
									{userAccessData.percentage}%
								</span>
							</div>
						</div>

						{/* Allowed Users */}
						<div>
							<label className="block text-sm font-medium text-purple-800 mb-1">Add Allowed Users</label>
							<input
								type="text"
								value={userAccessData.allowedUsersInput}
								onChange={(e) => setUserAccessData({
									...userAccessData,
									allowedUsersInput: e.target.value
								})}
								placeholder="user1, user2, user3..."
								className="w-full border border-purple-300 rounded px-3 py-2 text-sm"
								disabled={operationLoading}
								data-testid="allowed-users-input"
							/>
						</div>

						{/* Blocked Users */}
						<div>
							<label className="block text-sm font-medium text-purple-800 mb-1">Add Blocked Users</label>
							<input
								type="text"
								value={userAccessData.blockedUsersInput}
								onChange={(e) => setUserAccessData({
									...userAccessData,
									blockedUsersInput: e.target.value
								})}
								placeholder="user4, user5, user6..."
								className="w-full border border-purple-300 rounded px-3 py-2 text-sm"
								disabled={operationLoading}
								data-testid="blocked-users-input"
							/>
						</div>
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleUserAccessSubmit}
							disabled={operationLoading}
							className="px-3 py-1 bg-purple-600 text-white rounded text-sm hover:bg-purple-700 disabled:opacity-50"
							data-testid="save-user-access-button"
						>
							{operationLoading ? 'Saving...' : 'Save User Access'}
						</button>
						<button
							onClick={() => {
								setEditingUserAccess(false);
								setUserAccessData({
									percentage: flag.userRolloutPercentage || 0,
									allowedUsersInput: '',
									blockedUsersInput: ''
								});
							}}
							disabled={operationLoading}
							className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
							data-testid="cancel-user-access-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className="text-sm text-gray-600 space-y-1">
					{(() => {
						// Check flag mode and show appropriate text
						if (components.baseStatus === 'Enabled') {
							return <div className="text-green-600 font-medium">Open access - available to all users</div>;
						};
						// Check if user access control is set
						if (components.hasPercentage || components.hasUserTargeting) {
							return (
								<>
									{/* Display rollout percentage */}
									{components.hasPercentage && (
										<div>Percentage Rollout: {flag.userRolloutPercentage || 0}%</div>
									)}
									{components.hasUserTargeting && (
										<>
											{/* Display allowed users using the extracted function */}
											{flag.allowedUsers && flag.allowedUsers.length > 0 && 
												renderAllowedUsers(flag.allowedUsers)
											}
											{/* Display blocked users using the extracted function */}
											{flag.blockedUsers && flag.blockedUsers.length > 0 && 
												renderBlockedUsers(flag.blockedUsers)
											}
										</>
									)}
								</>
							);
						}
							// Check if user access control is set
							if ((!components.hasUserTargeting && flag.userRolloutPercentage <= 0) && components.baseStatus === 'Other') {
								return <div className="text-gray-500 italic">No user restrictions</div>;
						} else if (components.baseStatus === 'Disabled') {
								return <div className="text-orange-600 font-medium">Access denied to all users - flag is disabled</div>;
						}

						return <div className="text-gray-500 italic">User access control configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};