import { useState, useEffect } from 'react';
import { Users, Percent, UserCheck, UserX, X } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';

interface TenantAccessControlStatusIndicatorProps {
    flag: FeatureFlagDto;
}

export const TenantAccessControlStatusIndicator: React.FC<TenantAccessControlStatusIndicatorProps> = ({ flag }) => {
    const components = parseStatusComponents(flag);
    
    if (!components.hasPercentage && !components.hasTenantTargeting) return null;

    const allowedCount = flag.allowedTenants?.length || 0;
    const blockedCount = flag.blockedTenants?.length || 0;
    const percentage = flag.tenantRolloutPercentage || 0;

    return (
        <div className="mb-4 p-4 bg-purple-50 border border-purple-200 rounded-lg">
            <div className="flex items-center gap-2 mb-3">
                <UserCheck className="w-4 h-4 text-purple-600" />
                <h4 className="font-medium text-purple-900">Tenant Access Control</h4>
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

                {/* Allowed Tenants */}
                {components.hasTenantTargeting && (
                    <div className="flex items-center gap-2">
                        <UserCheck className="w-4 h-4 text-green-600" />
                        <span className="font-medium">Allowed:</span>
                        <span className="text-green-700 font-semibold">{allowedCount} tenant{allowedCount !== 1 ? 's' : ''}</span>
                    </div>
                )}

                {/* Blocked Tenants */}
                {components.hasTenantTargeting && (
                    <div className="flex items-center gap-2">
                        <UserX className="w-4 h-4 text-red-600" />
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
    onUpdateTenantAccess: (allowedTenants?: string[], blockedTenants?: string[], percentage?: number) => Promise<void>;
    onClearTenantAccess: () => Promise<void>;
    operationLoading: boolean;
}

export const TenantAccessSection: React.FC<TenantAccessSectionProps> = ({
    flag,
    onUpdateTenantAccess,
    onClearTenantAccess,
    operationLoading
}) => {
    const [editingTenantAccess, setEditingTenantAccess] = useState(false);
    const [tenantAccessData, setTenantAccessData] = useState({
        percentage: flag.tenantRolloutPercentage || 0,
        allowedTenantsInput: '',
        blockedTenantsInput: ''
    });

    const components = parseStatusComponents(flag);

    // Update local state when flag changes (when a different flag is selected)
    useEffect(() => {
        setTenantAccessData({
            percentage: flag.tenantRolloutPercentage || 0,
            allowedTenantsInput: '',
            blockedTenantsInput: ''
        });
    }, [flag.key, flag.tenantRolloutPercentage]);

    const handleTenantAccessSubmit = async () => {
        try {
            // Apply percentage if changed
            if (tenantAccessData.percentage !== (flag.tenantRolloutPercentage || 0)) {
                await onUpdateTenantAccess(undefined, undefined, tenantAccessData.percentage);
            }

            // Add allowed tenants if any
            if (tenantAccessData.allowedTenantsInput.trim()) {
                const tenantIds = tenantAccessData.allowedTenantsInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
                const currentAllowedTenants = flag.allowedTenants || [];
                const updatedAllowedTenants = [...new Set([...currentAllowedTenants, ...tenantIds])];
                await onUpdateTenantAccess(updatedAllowedTenants, flag.blockedTenants);
            }

            // Add blocked tenants if any
            if (tenantAccessData.blockedTenantsInput.trim()) {
                const tenantIds = tenantAccessData.blockedTenantsInput.split(',').map(u => u.trim()).filter(u => u.length > 0);
                const currentBlockedTenants = flag.blockedTenants || [];
                const updatedBlockedTenants = [...new Set([...currentBlockedTenants, ...tenantIds])];
                await onUpdateTenantAccess(flag.allowedTenants, updatedBlockedTenants);
            }

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

    const hasTenantAccessControl = components.hasPercentage || components.hasTenantTargeting || 
                                (flag.allowedTenants && flag.allowedTenants.length > 0) || 
                                (flag.blockedTenants && flag.blockedTenants.length > 0);

    return (
        <div className="space-y-4 mb-6">
            <div className="flex justify-between items-center">
                <h4 className="font-medium text-gray-900">Tenant Access Control</h4>
                <div className="flex gap-2">
                    <button
                        onClick={() => setEditingTenantAccess(true)}
                        disabled={operationLoading}
                        className="text-purple-600 hover:text-purple-800 text-sm flex items-center gap-1 disabled:opacity-50"
                        data-testid="manage-tenants-button"
                    >
                        <Users className="w-4 h-4" />
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
                                    value={tenantAccessData.percentage}
                                    onChange={(e) => setTenantAccessData({ 
                                        ...tenantAccessData, 
                                        percentage: parseInt(e.target.value) 
                                    })}
                                    className="flex-1"
                                    disabled={operationLoading}
                                    data-testid="percentage-slider"
                                />
                                <span className="text-sm font-medium text-purple-800 min-w-[3rem]">
                                    {tenantAccessData.percentage}%
                                </span>
                            </div>
                        </div>

                        {/* Allowed Tenants */}
                        <div>
                            <label className="block text-sm font-medium text-purple-800 mb-1">Add Allowed Tenants</label>
                            <input
                                type="text"
                                value={tenantAccessData.allowedTenantsInput}
                                onChange={(e) => setTenantAccessData({
                                    ...tenantAccessData,
                                    allowedTenantsInput: e.target.value 
                                })}
                                placeholder="company1, company2, company3..."
                                className="w-full border border-purple-300 rounded px-3 py-2 text-sm"
                                disabled={operationLoading}
                                data-testid="allowed-tenants-input"
                            />
                        </div>

                        {/* Blocked Tenants */}
                        <div>
                            <label className="block text-sm font-medium text-purple-800 mb-1">Add Blocked Tenants</label>
                            <input
                                type="text"
                                value={tenantAccessData.blockedTenantsInput}
                                onChange={(e) => setTenantAccessData({
                                    ...tenantAccessData,
                                    blockedTenantsInput: e.target.value 
                                })}
                                placeholder="company4, company5, company6..."
                                className="w-full border border-purple-300 rounded px-3 py-2 text-sm"
                                disabled={operationLoading}
                                data-testid="blocked-tenants-input"
                            />
                        </div>
                    </div>
                    
                    <div className="flex gap-2 mt-4">
                        <button
                            onClick={handleTenantAccessSubmit}
                            disabled={operationLoading}
                            className="px-3 py-1 bg-purple-600 text-white rounded text-sm hover:bg-purple-700 disabled:opacity-50"
                            data-testid="save-tenant-access-button"
                        >
                            {operationLoading ? 'Saving...' : 'Save Tenant Access'}
                        </button>
                        <button
                            onClick={() => {
                                setEditingTenantAccess(false);
                                setTenantAccessData({
                                    percentage: flag.tenantRolloutPercentage || 0,
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
                    <div>Percentage: {flag.tenantRolloutPercentage || 0}%</div>
                    <div>Allowed Tenants: {flag.allowedTenants?.length || 0}</div>
                    <div>Blocked Tenants: {flag.blockedTenants?.length || 0}</div>
                    
                    {/* Display current tenants */}
                    {flag.allowedTenants && flag.allowedTenants.length > 0 && (
                        <div className="mt-2">
                            <span className="text-xs font-medium text-green-700">Allowed: </span>
                            <div className="flex flex-wrap gap-1 mt-1">
                                {flag.allowedTenants.slice(0, 5).map((tenant) => (
                                    <span
                                        key={tenant}
                                        className="inline-flex items-center px-2 py-1 text-xs bg-green-100 text-green-800 rounded-full border border-green-200"
                                    >
                                        <UserCheck className="w-3 h-3 mr-1" />
                                        {tenant}
                                    </span>
                                ))}
                                {flag.allowedTenants.length > 5 && (
                                    <span className="text-xs text-gray-500">+{flag.allowedTenants.length - 5} more</span>
                                )}
                            </div>
                        </div>
                    )}
                    
                    {flag.blockedTenants && flag.blockedTenants.length > 0 && (
                        <div className="mt-2">
                            <span className="text-xs font-medium text-red-700">Blocked: </span>
                            <div className="flex flex-wrap gap-1 mt-1">
                                {flag.blockedTenants.slice(0, 5).map((tenant) => (
                                    <span
                                        key={tenant}
                                        className="inline-flex items-center px-2 py-1 text-xs bg-red-100 text-red-800 rounded-full border border-red-200"
                                    >
                                        <UserX className="w-3 h-3 mr-1" />
                                        {tenant}
                                    </span>
                                ))}
                                {flag.blockedTenants.length > 5 && (
                                    <span className="text-xs text-gray-500">+{flag.blockedTenants.length - 5} more</span>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};