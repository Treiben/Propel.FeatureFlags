import { useState, useEffect } from 'react';
import { Users, Percent, UserCheck, UserX, X, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';
import { getSectionClasses, theme } from '../../styles/theme';

interface UserAccessControlStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const UserAccessControlStatusIndicator: React.FC<UserAccessControlStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);

	if (!components.hasPercentage && !components.hasUserTargeting) return null;

	const allowedCount = flag.userAccess?.allowed?.length || 0;
	const blockedCount = flag.userAccess?.blocked?.length || 0;
	const rolloutPercentage = flag.userAccess?.rolloutPercentage || 0;

	const userAccessStyles = getSectionClasses('userAccess');

	return (
		<div className={`mb-4 p-4 ${userAccessStyles.bg} ${userAccessStyles.border} border rounded-lg`}>
			<div className="flex items-center gap-2 mb-3">
				<Users className={`w-4 h-4 ${userAccessStyles.buttonText}`} />
				<h4 className={`font-medium ${userAccessStyles.textPrimary}`}>User Access Control</h4>
			</div>

			<div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
				{components.hasPercentage && (
					<div className="flex items-center gap-2">
						<Percent className={`w-4 h-4 ${theme.warning.text[600]}`} />
						<span className="font-medium">Percentage:</span>
						<span className={theme.warning.text[700]}>{rolloutPercentage}% rollout</span>
					</div>
				)}

				{components.hasUserTargeting && (
					<div className="flex items-center gap-2">
						<UserCheck className={`w-4 h-4 ${theme.success.text[600]}`} />
						<span className="font-medium">Allowed:</span>
						<span className={`${theme.success.text[700]} font-semibold`}>{allowedCount} user{allowedCount !== 1 ? 's' : ''}</span>
					</div>
				)}

				{components.hasUserTargeting && (
					<div className="flex items-center gap-2">
						<UserX className={`w-4 h-4 ${theme.danger.text[600]}`} />
						<span className="font-medium">Blocked:</span>
						<span className={`${theme.danger.text[700]} font-semibold`}>{blockedCount} user{blockedCount !== 1 ? 's' : ''}</span>
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
				className={`${theme.neutral.text[400]} ${theme.neutral.hover.text600} transition-colors`}
				type="button"
			>
				<Info className="w-4 h-4" />
			</button>

			{showTooltip && (
				<div className={`absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-sm leading-relaxed ${theme.neutral.text[800]} bg-white ${theme.neutral.border[300]} border rounded-lg shadow-lg min-w-[280px] max-w-[360px]`}>
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-white"></div>
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
			<span className={`text-xs font-medium ${theme.success.text[700]}`}>Allowed: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayUsers.map((user) => (
					<span
						key={user}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.success[100]} ${theme.success.text[800]} rounded-full ${theme.success.border[200]} border`}
					>
						<UserCheck className="w-3 h-3 mr-1" />
						{user}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.neutral[100]} ${theme.neutral.text[600]} rounded-full ${theme.neutral.border[200]} border ${theme.neutral.hover.bg100} transition-colors cursor-pointer`}
						title={`Show ${users.length - 3} more users`}
					>
						...
					</button>
				)}
				{hasMore && expanded && (
					<button
						onClick={onToggleExpand}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.neutral[100]} ${theme.neutral.text[600]} rounded-full ${theme.neutral.border[200]} border ${theme.neutral.hover.bg100} transition-colors cursor-pointer`}
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
			<span className={`text-xs font-medium ${theme.danger.text[700]}`}>Blocked: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayUsers.map((user) => (
					<span
						key={user}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.danger[100]} ${theme.danger.text[800]} rounded-full ${theme.danger.border[200]} border`}
					>
						<UserX className="w-3 h-3 mr-1" />
						{user}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.neutral[100]} ${theme.neutral.text[600]} rounded-full ${theme.neutral.border[200]} border ${theme.neutral.hover.bg100} transition-colors cursor-pointer`}
						title={`Show ${users.length - 3} more users`}
					>
						...
					</button>
				)}
				{hasMore && expanded && (
					<button
						onClick={onToggleExpand}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.neutral[100]} ${theme.neutral.text[600]} rounded-full ${theme.neutral.border[200]} border ${theme.neutral.hover.bg100} transition-colors cursor-pointer`}
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
	const userAccessStyles = getSectionClasses('userAccess');

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
			let finalAllowedUsers = flag.userAccess?.allowed || [];
			let finalBlockedUsers = flag.userAccess?.blocked || [];
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

	const rolloutPercentage = flag.userAccess?.rolloutPercentage || 0;
	const allowedUsers = flag.userAccess?.allowed || [];
	const blockedUsers = flag.userAccess?.blocked || [];

	const hasPercentageRestriction = rolloutPercentage > 0 && rolloutPercentage < 100;
	const hasUserTargeting = allowedUsers.length > 0 || blockedUsers.length > 0;
	const hasUserAccessControl = hasPercentageRestriction || hasUserTargeting;

	// Dynamic slider gradient style
	const sliderStyle = {
		background: `linear-gradient(to right, #f59e0b 0%, #f59e0b ${userAccessData.rolloutPercentage}%, #e5e7eb ${userAccessData.rolloutPercentage}%, #e5e7eb 100%)`
	};

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className={`font-medium ${theme.neutral.text[900]}`}>User Access Control</h4>
					<InfoTooltip content="Control user access with percentage rollouts for A/B testing, canary releases, and gradual feature deployment." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingUserAccess(true)}
						disabled={operationLoading}
						className={`text-sm flex items-center gap-1 disabled:opacity-50 ${userAccessStyles.buttonText} ${theme.success.hover.text800}`}
						data-testid="manage-users-button"
					>
						<Users className="w-4 h-4" />
						Manage Users
					</button>
					{hasUserAccessControl && (
						<button
							onClick={handleClearUserAccess}
							disabled={operationLoading}
							className={`${theme.danger.text[600]} ${theme.danger.hover.text800} text-sm flex items-center gap-1 disabled:opacity-50`}
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
				<div className={`${userAccessStyles.bg} ${userAccessStyles.border} border rounded-lg p-4`}>
					<div className="space-y-4">
						<div>
							<label className={`block text-sm font-medium ${userAccessStyles.text} mb-2`}>Percentage Rollout</label>
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
									style={sliderStyle}
									className="flex-1 slider-amber"
									disabled={operationLoading}
									data-testid="percentage-slider"
								/>
								<span className={`text-sm font-medium ${userAccessStyles.text} min-w-[3rem]`}>
									{userAccessData.rolloutPercentage}%
								</span>
							</div>
						</div>

						<div>
							<label className={`block text-sm font-medium ${userAccessStyles.text} mb-1`}>Add Allowed Users</label>
							<input
								type="text"
								value={userAccessData.allowedUsersInput}
								onChange={(e) => setUserAccessData({
									...userAccessData,
									allowedUsersInput: e.target.value
								})}
								placeholder="user1, user2, user3..."
								className={`w-full ${userAccessStyles.border} border rounded px-3 py-2 text-sm`}
								disabled={operationLoading}
								data-testid="allowed-users-input"
							/>
						</div>

						<div>
							<label className={`block text-sm font-medium ${userAccessStyles.text} mb-1`}>Add Blocked Users</label>
							<input
								type="text"
								value={userAccessData.blockedUsersInput}
								onChange={(e) => setUserAccessData({
									...userAccessData,
									blockedUsersInput: e.target.value
								})}
								placeholder="user4, user5, user6..."
								className={`w-full ${userAccessStyles.border} border rounded px-3 py-2 text-sm`}
								disabled={operationLoading}
								data-testid="blocked-users-input"
							/>
						</div>
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleUserAccessSubmit}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.warning[600]} text-white rounded text-sm hover:bg-sky-700 disabled:opacity-50`}
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
							className={`px-3 py-1 ${theme.neutral[300]} ${theme.neutral.text[700]} rounded text-sm ${theme.neutral.hover.bg400} disabled:opacity-50`}
							data-testid="cancel-user-access-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className={`text-sm ${theme.neutral.text[600]} space-y-1`}>
					{(() => {
						if (components.baseStatus === 'Enabled') {
							return <div className={`${theme.success.text[600]} font-medium`}>Open access - available to all users</div>;
						}

						if (hasPercentageRestriction || hasUserTargeting) {
							return (
								<>
									{hasPercentageRestriction && (
										<div>Percentage Rollout: {rolloutPercentage}%</div>
									)}
									{hasUserTargeting && (
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

						if (components.baseStatus === 'Other') {
							return <div className={`${theme.neutral.text[500]} italic`}>No user restrictions</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className={`${theme.warning.text[600]} font-medium`}>Access denied to all users - flag is disabled</div>;
						}

						return <div className={`${theme.neutral.text[500]} italic`}>User access control configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};