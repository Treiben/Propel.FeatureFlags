import { useState, useEffect } from 'react';
import { Users, Percent, UserCheck, UserX, X, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';

interface UserAccessControlStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const UserAccessControlStatusIndicator: React.FC<UserAccessControlStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);

	if (!components.hasPercentage && !components.hasUserTargeting) return null;

	const allowedCount = flag.userAccess?.allowedIds?.length || 0;
	const blockedCount = flag.userAccess?.blockedIds?.length || 0;
	const rolloutPercentage = flag.userAccess?.rolloutPercentage || 0;

	return (
		<div className="mb-4 p-4 bg-purple-50 border border-purple-200 rounded-lg">
			<div className="flex items-center gap-2 mb-3">
				<Users className="w-4 h-4 text-purple-600" />
				<h4 className="font-medium text-purple-900">User Access Control</h4>
			</div>

			<div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
				{components.hasPercentage && (
					<div className="flex items-center gap-2">
						<Percent className="w-4 h-4 text-yellow-600" />
						<span className="font-medium">Percentage:</span>
						<span className="text-yellow-700">{rolloutPercentage}% rollout</span>
					</div>
				)}

				{components.hasUserTargeting && (
					<div className="flex items-center gap-2">
						<UserCheck className="w-4 h-4 text-green-600" />
						<span className="font-medium">Allowed:</span>
						<span className="text-green-700 font-semibold">{allowedCount} user{allowedCount !== 1 ? 's' : ''}</span>
					</div>
				)}

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
	onUpdateUserAccess: (allowedUsers?: string[], blockedUsers?: string[], rolloutPercentage?: number) => Promise<void>;
	onClearUserAccess: () => Promise<void>;
	operationLoading: boolean;
}

const InfoTooltip: React.FC<{ content: string; className?: string }> = ({ content, className = "" }) => {
	const [showTooltip, setShowTooltip] = useState(false);

	return (
		<div className={`relative inline-block ${className}`}>
			<button
				onMouseEnter={() => setShowTooltip(true)}
				onMouseLeave={() => setShowTooltip(false)}
				onClick={(e) => {
					e.preventDefault();
					setShowTooltip(!showTooltip);
				}}
				className="text-gray-400 hover:text-gray-600 transition-colors"
				type="button"
			>
				<Info className="w-4 h-4" />
			</button>

			{showTooltip && (
				<div className="absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-xs text-white bg-gray-900 rounded-lg shadow-lg max-w-xs whitespace-normal">
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-gray-900"></div>
				</div>
			)}
		</div>
	);
};

const renderAllowedUsers = (users: string[], expanded: boolean, onToggleExpand: () => void) => {
	const displayUsers = expanded ? users : users.slice(0, 3);
	const hasMore = users.length > 3;

	return (
		<div className="mt-2">
			<span className="text-xs font-medium text-green-700">Allowed: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayUsers.map((user) => (
					<span
						key={user}
						className="inline-flex items-center px-2 py-1 text-xs bg-green-100 text-green-800 rounded-full border border-green-200"
					>
						<UserCheck className="w-3 h-3 mr-1" />
						{user}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className="inline-flex items-center px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded-full border border-gray-200 hover:bg-gray-200 transition-colors cursor-pointer"
						title={`Show ${users.length - 3} more users`}
					>
						...
					</button>
				)}
				{hasMore && expanded && (
					<button
						onClick={onToggleExpand}
						className="inline-flex items-center px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded-full border border-gray-200 hover:bg-gray-200 transition-colors cursor-pointer"
						title="Show less"
					>
						Show less
					</button>
				)}
			</div>
		</div>
	);
};

const renderBlockedUsers = (users: string[], expanded: boolean, onToggleExpand: () => void) => {
	const displayUsers = expanded ? users : users.slice(0, 3);
	const hasMore = users.length > 3;

	return (
		<div className="mt-2">
			<span className="text-xs font-medium text-red-700">Blocked: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayUsers.map((user) => (
					<span
						key={user}
						className="inline-flex items-center px-2 py-1 text-xs bg-red-100 text-red-800 rounded-full border border-red-200"
					>
						<UserX className="w-3 h-3 mr-1" />
						{user}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className="inline-flex items-center px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded-full border border-gray-200 hover:bg-gray-200 transition-colors cursor-pointer"
						title={`Show ${users.length - 3} more users`}
					>
						...
					</button>
				)}
				{hasMore && expanded && (
					<button
						onClick={onToggleExpand}
						className="inline-flex items-center px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded-full border border-gray-200 hover:bg-gray-200 transition-colors cursor-pointer"
						title="Show less"
					>
						Show less
					</button>
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
		rolloutPercentage: flag.userAccess?.rolloutPercentage || 0,
		allowedUsersInput: '',
		blockedUsersInput: ''
	});
	const [expandedAllowedUsers, setExpandedAllowedUsers] = useState(false);
	const [expandedBlockedUsers, setExpandedBlockedUsers] = useState(false);

	const components = parseStatusComponents(flag);

	useEffect(() => {
		setUserAccessData({
			rolloutPercentage: flag.userAccess?.rolloutPercentage || 0,
			allowedUsersInput: '',
			blockedUsersInput: ''
		});
		setExpandedAllowedUsers(false);
		setExpandedBlockedUsers(false);
	}, [flag.key, flag.userAccess?.rolloutPercentage]);

	const handleUserAccessSubmit = async () => {
		try {
			let finalAllowedUsers = flag.userAccess?.allowedIds || [];
			let finalBlockedUsers = flag.userAccess?.blockedIds || [];
			let finalPercentage = flag.userAccess?.rolloutPercentage || 0;

			if (userAccessData.rolloutPercentage !== (flag.userAccess?.rolloutPercentage || 0)) {
				finalPercentage = userAccessData.rolloutPercentage;
			}

			if (userAccessData.allowedUsersInput.trim()) {
				const userIds = userAccessData.allowedUsersInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
				finalAllowedUsers = [...new Set([...finalAllowedUsers, ...userIds])];
			}

			if (userAccessData.blockedUsersInput.trim()) {
				const userIds = userAccessData.blockedUsersInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
				finalBlockedUsers = [...new Set([...finalBlockedUsers, ...userIds])];
			}

			await onUpdateUserAccess(finalAllowedUsers, finalBlockedUsers, finalPercentage);

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

	const hasUserAccessControl = components.hasUserTargeting ||
		(flag.userAccess?.rolloutPercentage && flag.userAccess.rolloutPercentage > 0) ||
		(flag.userAccess?.allowedIds && flag.userAccess.allowedIds.length > 0) ||
		(flag.userAccess?.blockedIds && flag.userAccess.blockedIds.length > 0);

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">User Access Control</h4>
					<InfoTooltip content="Control user access with percentage rollouts for A/B testing, canary releases, and gradual feature deployment." />
				</div>
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
						<div>
							<label className="block text-sm font-medium text-purple-800 mb-2">Percentage Rollout</label>
							<div className="flex items-center gap-3">
								<input
									type="range"
									min="0"
									max="100"
									value={userAccessData.rolloutPercentage}
									onChange={(e) => setUserAccessData({
										...userAccessData,
										rolloutPercentage: parseInt(e.target.value)
									})}
									className="flex-1"
									disabled={operationLoading}
									data-testid="percentage-slider"
								/>
								<span className="text-sm font-medium text-purple-800 min-w-[3rem]">
									{userAccessData.rolloutPercentage}%
								</span>
							</div>
						</div>

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
									rolloutPercentage: flag.userAccess?.rolloutPercentage || 0,
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
						if (components.baseStatus === 'Enabled') {
							return <div className="text-green-600 font-medium">Open access - available to all users</div>;
						}

						const rolloutPercentage = flag.userAccess?.rolloutPercentage || 0;
						const allowedUsers = flag.userAccess?.allowedIds || [];
						const blockedUsers = flag.userAccess?.blockedIds || [];

						if (rolloutPercentage > 0 || components.hasUserTargeting) {
							return (
								<>
									{rolloutPercentage > 0 && (
										<div>Percentage Rollout: {rolloutPercentage}%</div>
									)}
									{components.hasUserTargeting && (
										<>
											{allowedUsers.length > 0 &&
												renderAllowedUsers(
													allowedUsers,
													expandedAllowedUsers,
													() => setExpandedAllowedUsers(!expandedAllowedUsers)
												)
											}
											{blockedUsers.length > 0 &&
												renderBlockedUsers(
													blockedUsers,
													expandedBlockedUsers,
													() => setExpandedBlockedUsers(!expandedBlockedUsers)
												)
											}
										</>
									)}
								</>
							);
						}

						if ((!components.hasUserTargeting && rolloutPercentage <= 0) && components.baseStatus === 'Other') {
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