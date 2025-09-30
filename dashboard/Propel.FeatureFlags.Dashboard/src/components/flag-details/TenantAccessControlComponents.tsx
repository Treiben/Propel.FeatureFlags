import { useState, useEffect } from 'react';
import { Building, Percent, Shield, ShieldX, X, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';

interface TenantAccessControlStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const TenantAccessControlStatusIndicator: React.FC<TenantAccessControlStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);

	if (!components.hasPercentage && !components.hasTenantTargeting) return null;

	const allowedCount = flag.tenantAccess?.allowedIds?.length || 0;
	const blockedCount = flag.tenantAccess?.blockedIds?.length || 0;
	const rolloutPercentage = flag.tenantAccess?.rolloutPercentage || 0;

	return (
		<div className="mb-4 p-4 bg-teal-50 border border-teal-200 rounded-lg" data-testid="tenant-access-status-indicator">
			<div className="flex items-center gap-2 mb-3">
				<Building className="w-4 h-4 text-teal-600" />
				<h4 className="font-medium text-teal-900">Tenant Access Control</h4>
			</div>

			<div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
				{components.hasPercentage && (
					<div className="flex items-center gap-2">
						<Percent className="w-4 h-4 text-yellow-600" />
						<span className="font-medium">Percentage:</span>
						<span className="text-yellow-700">{rolloutPercentage}% rollout</span>
					</div>
				)}

				{components.hasTenantTargeting && (
					<div className="flex items-center gap-2">
						<Shield className="w-4 h-4 text-green-600" />
						<span className="font-medium">Allowed:</span>
						<span className="text-green-700 font-semibold">{allowedCount} tenant{allowedCount !== 1 ? 's' : ''}</span>
					</div>
				)}

				{components.hasTenantTargeting && (
					<div className="flex items-center gap-2">
						<ShieldX className="w-4 h-4 text-red-600" />
						<span className="font-medium">Blocked:</span>
						<span className="text-red-700 font-semibold">{blockedCount} tenant{blockedCount !== 1 ? 's' : ''}</span>
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

const renderAllowedTenants = (tenants: string[], expanded: boolean, onToggleExpand: () => void) => {
	const displayTenants = expanded ? tenants : tenants.slice(0, 3);
	const hasMore = tenants.length > 3;

	return (
		<div className="mt-2">
			<span className="text-xs font-medium text-green-700">Allowed: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayTenants.map((tenant) => (
					<span
						key={tenant}
						className="inline-flex items-center px-2 py-1 text-xs bg-green-100 text-green-800 rounded-full border border-green-200"
					>
						<Shield className="w-3 h-3 mr-1" />
						{tenant}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className="inline-flex items-center px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded-full border border-gray-200 hover:bg-gray-200 transition-colors cursor-pointer"
						title={`Show ${tenants.length - 3} more tenants`}
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

const renderBlockedTenants = (tenants: string[], expanded: boolean, onToggleExpand: () => void) => {
	const displayTenants = expanded ? tenants : tenants.slice(0, 3);
	const hasMore = tenants.length > 3;

	return (
		<div className="mt-2">
			<span className="text-xs font-medium text-red-700">Blocked: </span>
			<div className="flex flex-wrap gap-1 mt-1">
				{displayTenants.map((tenant) => (
					<span
						key={tenant}
						className="inline-flex items-center px-2 py-1 text-xs bg-red-100 text-red-800 rounded-full border border-red-200"
					>
						<ShieldX className="w-3 h-3 mr-1" />
						{tenant}
					</span>
				))}
				{hasMore && !expanded && (
					<button
						onClick={onToggleExpand}
						className="inline-flex items-center px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded-full border border-gray-200 hover:bg-gray-200 transition-colors cursor-pointer"
						title={`Show ${tenants.length - 3} more tenants`}
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
			let finalAllowedTenants = flag.tenantAccess?.allowedIds || [];
			let finalBlockedTenants = flag.tenantAccess?.blockedIds || [];
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

	const hasTenantAccessControl = components.hasTenantTargeting ||
		(flag.tenantAccess?.rolloutPercentage && flag.tenantAccess.rolloutPercentage > 0) ||
		(flag.tenantAccess?.allowedIds && flag.tenantAccess.allowedIds.length > 0) ||
		(flag.tenantAccess?.blockedIds && flag.tenantAccess.blockedIds.length > 0);

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">Tenant Access Control</h4>
					<InfoTooltip content="Manage multi-tenant rollouts with percentage controls for enterprise deployments and tenant-specific feature access." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingTenantAccess(true)}
						disabled={operationLoading}
						className="text-teal-600 hover:text-teal-800 text-sm flex items-center gap-1 disabled:opacity-50"
						data-testid="manage-tenants-button"
					>
						<Building className="w-4 h-4" />
						Manage Tenants
					</button>
					{hasTenantAccessControl && (
						<button
							onClick={handleClearTenantAccess}
							disabled={operationLoading}
							className="text-red-600 hover:text-red-800 text-sm flex items-center gap-1 disabled:opacity-50"
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
				<div className="bg-teal-50 border border-teal-200 rounded-lg p-4">
					<div className="space-y-4">
						<div>
							<label className="block text-sm font-medium text-teal-800 mb-2">Percentage Rollout</label>
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
									className="flex-1"
									disabled={operationLoading}
									data-testid="percentage-slider"
								/>
								<span className="text-sm font-medium text-teal-800 min-w-[3rem]">
									{tenantAccessData.rolloutPercentage}%
								</span>
							</div>
						</div>

						<div>
							<label className="block text-sm font-medium text-teal-800 mb-1">Add Allowed Tenants</label>
							<input
								type="text"
								value={tenantAccessData.allowedTenantsInput}
								onChange={(e) => setTenantAccessData({
									...tenantAccessData,
									allowedTenantsInput: e.target.value
								})}
								placeholder="company1, company2, company3..."
								className="w-full border border-teal-300 rounded px-3 py-2 text-sm"
								disabled={operationLoading}
								data-testid="allowed-tenants-input"
							/>
						</div>

						<div>
							<label className="block text-sm font-medium text-teal-800 mb-1">Add Blocked Tenants</label>
							<input
								type="text"
								value={tenantAccessData.blockedTenantsInput}
								onChange={(e) => setTenantAccessData({
									...tenantAccessData,
									blockedTenantsInput: e.target.value
								})}
								placeholder="company4, company5, company6..."
								className="w-full border border-teal-300 rounded px-3 py-2 text-sm"
								disabled={operationLoading}
								data-testid="blocked-tenants-input"
							/>
						</div>
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleTenantAccessSubmit}
							disabled={operationLoading}
							className="px-3 py-1 bg-teal-600 text-white rounded text-sm hover:bg-teal-700 disabled:opacity-50"
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
							className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
							data-testid="cancel-tenant-access-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className="text-sm text-gray-600 space-y-1">
					{(() => {
						if (components.baseStatus === 'Enabled') {
							return <div className="text-green-600 font-medium">Open access - available to all tenants</div>;
						}

						const rolloutPercentage = flag.tenantAccess?.rolloutPercentage || 0;
						const allowedTenants = flag.tenantAccess?.allowedIds || [];
						const blockedTenants = flag.tenantAccess?.blockedIds || [];

						if (rolloutPercentage > 0 || components.hasTenantTargeting) {
							return (
								<>
									{rolloutPercentage > 0 && (
										<div>Percentage Rollout: {rolloutPercentage}%</div>
									)}
									{components.hasTenantTargeting && (
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

						if ((!components.hasTenantTargeting && rolloutPercentage <= 0) && components.baseStatus === 'Other') {
							return <div className="text-gray-500 italic">No tenant restrictions configured</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className="text-orange-600 font-medium">Access denied to all tenants - flag is disabled</div>;
						}

						return <div className="text-gray-500 italic">Tenant access control configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};