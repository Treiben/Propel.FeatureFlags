import { useState, useEffect } from 'react';
import { Building, Percent, Shield, ShieldX, X, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';
import { getSectionClasses, theme } from '../../styles/theme';

interface TenantAccessControlStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const TenantAccessControlStatusIndicator: React.FC<TenantAccessControlStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);

	if (!components.hasPercentage && !components.hasTenantTargeting) return null;

	const allowedCount = flag.tenantAccess?.allowed?.length || 0;
	const blockedCount = flag.tenantAccess?.blocked?.length || 0;
	// eslint-disable-next-line @typescript-eslint/no-unused-vars
	const rolloutPercentage = flag.tenantAccess?.rolloutPercentage || 0;

	const targetingStyles = getSectionClasses('targeting');

	return (
		<div className={`mb-4 p-4 ${targetingStyles.bg} ${targetingStyles.border} border rounded-lg`} data-testid="tenant-access-status-indicator">
			<div className="flex items-center gap-2 mb-3">
				<Building className={`w-4 h-4 ${targetingStyles.buttonText}`} />
				<h4 className={`font-medium ${targetingStyles.textPrimary}`}>Tenant Access Control</h4>
			</div>

			<div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
				{components.hasPercentage && (
					<div className="flex items-center gap-2">
						<Percent className={`w-4 h-4 ${theme.warning.text[600]}`} />
						<span className="font-medium">Percentage:</span>
						<span className={theme.warning.text[700]}>{rolloutPercentage}% rollout</span>
					</div>
				)}

				{components.hasTenantTargeting && (
					<div className="flex items-center gap-2">
						<Shield className={`w-4 h-4 ${theme.success.text[600]}`} />
						<span className="font-medium">Allowed:</span>
						<span className={`${theme.success.text[700]} font-semibold`}>{allowedCount} tenant{allowedCount !== 1 ? 's' : ''}</span>
					</div>
				)}

				{components.hasTenantTargeting && (
					<div className="flex items-center gap-2">
						<ShieldX className={`w-4 h-4 ${theme.danger.text[600]}`} />
						<span className="font-medium">Blocked:</span>
						<span className={`${theme.danger.text[700]} font-semibold`}>{blockedCount} tenant{blockedCount !== 1 ? 's' : ''}</span>
					</div>
				)}
			</div>
		</div>
	);
};

interface TenantAccessSectionProps {
	flag: FeatureFlagDto;
	onUpdateTenantAccess: (allowedTenants?: string[], blockedTenants?: string[], rolloutPercentage?: number) => Promise<void>;
	onClearTenantAccess: () => Promise<void>;
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
				<div className={`absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-sm leading-relaxed ${theme.neutral.text[800]} bg-white ${theme.neutral.border[300]} border rounded-lg shadow-lg w-64`}>
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-white"></div>
				</div>
			)}
		</div>
	);
};

const renderAllowedTenants = (tenants: string[], expanded: boolean, onToggleExpand: () => void) => {
	const displayTenants = expanded ? tenants : tenants.slice(0, 3);
	const hasMore = tenants.length > 3;

	return (
		<div className="mt-2">
			<span className={`text-xs font-medium ${theme.success.text[700]}`}>Allowed: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayTenants.map((tenant) => (
					<span
						key={tenant}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.success[100]} ${theme.success.text[800]} rounded-full ${theme.success.border[200]} border`}
					>
						<Shield className="w-3 h-3 mr-1" />
						{tenant}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.neutral[100]} ${theme.neutral.text[600]} rounded-full ${theme.neutral.border[200]} border ${theme.neutral.hover.bg100} transition-colors cursor-pointer`}
						title={`Show ${tenants.length - 3} more tenants`}
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

const renderBlockedTenants = (tenants: string[], expanded: boolean, onToggleExpand: () => void) => {
	const displayTenants = expanded ? tenants : tenants.slice(0, 3);
	const hasMore = tenants.length > 3;

	return (
		<div className="mt-2">
			<span className={`text-xs font-medium ${theme.danger.text[700]}`}>Blocked: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayTenants.map((tenant) => (
					<span
						key={tenant}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.danger[100]} ${theme.danger.text[800]} rounded-full ${theme.danger.border[200]} border`}
					>
						<ShieldX className="w-3 h-3 mr-1" />
						{tenant}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className={`inline-flex items-center px-2 py-1 text-xs ${theme.neutral[100]} ${theme.neutral.text[600]} rounded-full ${theme.neutral.border[200]} border ${theme.neutral.hover.bg100} transition-colors cursor-pointer`}
						title={`Show ${tenants.length - 3} more tenants`}
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

export const TenantAccessSection: React.FC<TenantAccessSectionProps> = ({
	flag,
	onUpdateTenantAccess,
	onClearTenantAccess,
	operationLoading
}) => {
	const [editingTenantAccess, setEditingTenantAccess] = useState(false);
	const [tenantAccessData, setTenantAccessData] = useState({
		rolloutPercentage: flag.tenantAccess?.rolloutPercentage || 0,
		allowedTenantsInput: '',
		blockedTenantsInput: ''
	});
	const [expandedAllowedTenants, setExpandedAllowedTenants] = useState(false);
	const [expandedBlockedTenants, setExpandedBlockedTenants] = useState(false);

	const components = parseStatusComponents(flag);
	const targetingStyles = getSectionClasses('targeting');

	useEffect(() => {
		setTenantAccessData({
			rolloutPercentage: flag.tenantAccess?.rolloutPercentage || 0,
			allowedTenantsInput: '',
			blockedTenantsInput: ''
		});
		setExpandedAllowedTenants(false);
		setExpandedBlockedTenants(false);
	}, [flag.key, flag.tenantAccess?.rolloutPercentage]);

	const handleTenantAccessSubmit = async () => {
		try {
			let finalAllowedTenants = flag.tenantAccess?.allowed || [];
			let finalBlockedTenants = flag.tenantAccess?.blocked || [];
			let finalPercentage = flag.tenantAccess?.rolloutPercentage || 0;

			if (tenantAccessData.rolloutPercentage !== (flag.tenantAccess?.rolloutPercentage || 0)) {
				finalPercentage = tenantAccessData.rolloutPercentage;
			}

			if (tenantAccessData.allowedTenantsInput.trim()) {
				const tenantIds = tenantAccessData.allowedTenantsInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
				finalAllowedTenants = [...new Set([...finalAllowedTenants, ...tenantIds])];
			}

			if (tenantAccessData.blockedTenantsInput.trim()) {
				const tenantIds = tenantAccessData.blockedTenantsInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
				finalBlockedTenants = [...new Set([...finalBlockedTenants, ...tenantIds])];
			}

			await onUpdateTenantAccess(finalAllowedTenants, finalBlockedTenants, finalPercentage);

			setEditingTenantAccess(false);
		} catch (error) {
			console.error('Failed to update tenant access:', error);
		}
	};

	const handleClearTenantAccess = async () => {
		try {
			await onClearTenantAccess();
		} catch (error) {
			console.error('Failed to clear tenant access:', error);
		}
	};

	const rolloutPercentage = flag.tenantAccess?.rolloutPercentage || 0;
	const allowedTenants = flag.tenantAccess?.allowed || [];
	const blockedTenants = flag.tenantAccess?.blocked || [];

	const hasPercentageRestriction = rolloutPercentage > 0 && rolloutPercentage < 100;
	const hasTenantTargeting = allowedTenants.length > 0 || blockedTenants.length > 0;
	const hasTenantAccessControl = hasPercentageRestriction || hasTenantTargeting;

	// Dynamic slider gradient style
	const sliderStyle = {
		background: `linear-gradient(to right, #f59e0b 0%, #f59e0b ${tenantAccessData.rolloutPercentage}%, #e5e7eb ${tenantAccessData.rolloutPercentage}%, #e5e7eb 100%)`
	};

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className={`font-medium ${theme.neutral.text[900]}`}>Tenant Access Control</h4>
					<InfoTooltip content="Manage multi-tenant rollouts with percentage controls for enterprise deployments and tenant-specific feature access." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingTenantAccess(true)}
						disabled={operationLoading}
						className={`text-sm flex items-center gap-1 disabled:opacity-50 ${targetingStyles.buttonText} ${theme.info.hover?.text700 || 'hover:text-sky-700'}`}
						data-testid="manage-tenants-button"
					>
						<Building className="w-4 h-4" />
						Manage Tenants
					</button>
					{hasTenantAccessControl && (
						<button
							onClick={handleClearTenantAccess}
							disabled={operationLoading}
							className={`${theme.danger.text[600]} ${theme.danger.hover.text800} text-sm flex items-center gap-1 disabled:opacity-50`}
							title="Clear Tenant Access Control"
							data-testid="clear-tenant-access-button"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingTenantAccess ? (
				<div className={`${targetingStyles.bg} ${targetingStyles.border} border rounded-lg p-4`}>
					<div className="space-y-4">
						<div>
							<label className={`block text-sm font-medium ${targetingStyles.text} mb-2`}>Percentage Rollout</label>
							<div className="flex items-center gap-3">
								<input
									type="range"
									min="0"
									max="100"
									value={tenantAccessData.rolloutPercentage}
									onChange={(e) => setTenantAccessData({
										...tenantAccessData,
										rolloutPercentage: parseInt(e.target.value)
									})}
									style={sliderStyle}
									className="flex-1 slider-amber"
									disabled={operationLoading}
									data-testid="percentage-slider"
								/>
								<span className={`text-sm font-medium ${targetingStyles.text} min-w-[3rem]`}>
									{tenantAccessData.rolloutPercentage}%
								</span>
							</div>
						</div>

						<div>
							<label className={`block text-sm font-medium ${targetingStyles.text} mb-1`}>Add Allowed Tenants</label>
							<input
								type="text"
								value={tenantAccessData.allowedTenantsInput}
								onChange={(e) => setTenantAccessData({
									...tenantAccessData,
									allowedTenantsInput: e.target.value
								})}
								placeholder="company1, company2, company3..."
								className={`w-full ${targetingStyles.border} border rounded px-3 py-2 text-sm`}
								disabled={operationLoading}
								data-testid="allowed-tenants-input"
							/>
						</div>

						<div>
							<label className={`block text-sm font-medium ${targetingStyles.text} mb-1`}>Add Blocked Tenants</label>
							<input
								type="text"
								value={tenantAccessData.blockedTenantsInput}
								onChange={(e) => setTenantAccessData({
									...tenantAccessData,
									blockedTenantsInput: e.target.value
								})}
								placeholder="company4, company5, company6..."
								className={`w-full ${targetingStyles.border} border rounded px-3 py-2 text-sm`}
								disabled={operationLoading}
								data-testid="blocked-tenants-input"
							/>
						</div>
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleTenantAccessSubmit}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.warning[600]} text-white rounded text-sm hover:bg-sky-700 disabled:opacity-50`}
							data-testid="save-tenant-access-button"
						>
							{operationLoading ? 'Saving...' : 'Save Tenant Access'}
						</button>
						<button
							onClick={() => {
								setEditingTenantAccess(false);
								setTenantAccessData({
									rolloutPercentage: flag.tenantAccess?.rolloutPercentage || 0,
									allowedTenantsInput: '',
									blockedTenantsInput: ''
								});
							}}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.neutral[300]} ${theme.neutral.text[700]} rounded text-sm ${theme.neutral.hover.bg400} disabled:opacity-50`}
							data-testid="cancel-tenant-access-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className={`text-sm ${theme.neutral.text[600]} space-y-1`}>
					{(() => {
						if (components.baseStatus === 'Enabled') {
							return <div className={`${theme.success.text[600]} font-medium`}>Open access - available to all tenants</div>;
						}

						if (hasPercentageRestriction || hasTenantTargeting) {
							return (
								<>
									{hasPercentageRestriction && (
										<div>Percentage Rollout: {rolloutPercentage}%</div>
									)}
									{hasTenantTargeting && (
										<>
											{allowedTenants.length > 0 &&
												renderAllowedTenants(
													allowedTenants,
													expandedAllowedTenants,
													() => setExpandedAllowedTenants(!expandedAllowedTenants)
												)
											}
											{blockedTenants.length > 0 &&
												renderBlockedTenants(
													blockedTenants,
													expandedBlockedTenants,
													() => setExpandedBlockedTenants(!expandedBlockedTenants)
												)
											}
										</>
									)}
								</>
							);
						}

						if (components.baseStatus === 'Other') {
							return <div className={`${theme.neutral.text[500]} italic`}>No tenant restrictions</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className={`${theme.warning.text[600]} font-medium`}>Access denied to all tenants - flag is disabled</div>;
						}

						return <div className={`${theme.neutral.text[500]} italic`}>Tenant access control configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};