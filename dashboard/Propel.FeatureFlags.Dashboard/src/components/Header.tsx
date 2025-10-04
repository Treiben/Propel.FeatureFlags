import { useState } from 'react';
import { Menu, X, Info, HelpCircle } from 'lucide-react';
import { PropelIcon } from './PropelIcon';

interface HeaderProps {
    title?: string;
    subtitle?: string;
}

export const Header: React.FC<HeaderProps> = ({ 
    title = "Propel Feature Flags",
    subtitle = "Manage feature releases, rollouts, and targeting strategies"
}) => {
    const [showMobileMenu, setShowMobileMenu] = useState(false);
    const [showAboutModal, setShowAboutModal] = useState(false);
    const [showFlagsInfoModal, setShowFlagsInfoModal] = useState(false);

    return (
        <>
            <header className="bg-white border-b border-gray-200 shadow-sm">
                <div className="max-w-[1600px] mx-auto px-8">
                    <div className="flex items-center justify-between h-20">
                        {/* Left: Logo and Title */}
                        <div className="flex items-center gap-5">
                            <div className="flex items-center gap-4">
                                <div>
                                    <PropelIcon size={68} className="flex-shrink-0 text-white" />
                                </div>
                                <div>
                                    <h1 className="text-2xl font-bold text-gray-900 leading-tight">{title}</h1>
                                    <p className="text-sm text-gray-600 hidden md:block">{subtitle}</p>
                                </div>
                            </div>
                        </div>

                        {/* Right: Navigation */}
                        <nav className="hidden md:flex items-center gap-6">
                            <button
                                onClick={() => setShowFlagsInfoModal(true)}
                                className="flex items-center gap-2 text-gray-700 hover:text-blue-600 transition-colors"
                            >
                                <HelpCircle className="w-4 h-4" />
                                Feature Flags Guide
                            </button>
                            <button
                                onClick={() => setShowAboutModal(true)}
                                className="flex items-center gap-2 text-gray-700 hover:text-blue-600 transition-colors"
                            >
                                <Info className="w-4 h-4" />
                                About
                            </button>
                        </nav>

                        {/* Mobile menu button */}
                        <button
                            onClick={() => setShowMobileMenu(!showMobileMenu)}
                            className="md:hidden p-2 text-gray-600 hover:text-gray-900"
                        >
                            {showMobileMenu ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
                        </button>
                    </div>

                    {/* Mobile subtitle */}
                    <div className="md:hidden pb-3">
                        <p className="text-sm text-gray-600">{subtitle}</p>
                    </div>

                    {/* Mobile Navigation */}
                    {showMobileMenu && (
                        <div className="md:hidden border-t border-gray-200 py-4">
                            <div className="space-y-3">
                                <button
                                    onClick={() => {
                                        setShowFlagsInfoModal(true);
                                        setShowMobileMenu(false);
                                    }}
                                    className="flex items-center gap-2 w-full text-left text-gray-700 hover:text-blue-600 transition-colors py-2"
                                >
                                    <HelpCircle className="w-4 h-4" />
                                    Feature Flags Guide
                                </button>
                                <button
                                    onClick={() => {
                                        setShowAboutModal(true);
                                        setShowMobileMenu(false);
                                    }}
                                    className="flex items-center gap-2 w-full text-left text-gray-700 hover:text-blue-600 transition-colors py-2"
                                >
                                    <Info className="w-4 h-4" />
                                    About
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            </header>

            {/* About Modal */}
            {showAboutModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
                    <div className="bg-white rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
                        <div className="p-6">
                            <div className="flex justify-between items-start mb-4">
                                <div className="flex items-center gap-4">
                                    <div>
                                        <PropelIcon size={76} className="text-white" />
                                    </div>
                                    <div>
                                        <h2 className="text-2xl font-bold text-gray-900">Propel Feature Flags</h2>
                                        <p className="text-gray-600">Dashboard v1.0.0</p>
                                    </div>
                                </div>
                                <button
                                    onClick={() => setShowAboutModal(false)}
                                    className="text-gray-400 hover:text-gray-600 p-1"
                                >
                                    <X className="w-6 h-6" />
                                </button>
                            </div>
                            
                            <div className="space-y-4">
                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-2">About</h3>
                                    <p className="text-gray-700 leading-relaxed">
                                        Propel Feature Flags is a comprehensive feature management system built for .NET applications. 
                                        This dashboard provides a powerful interface to manage feature releases, control rollouts, 
                                        and implement sophisticated targeting strategies.
                                    </p>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-2">Key Features</h3>
                                    <ul className="text-gray-700 space-y-1">
                                        <li>✓ Type-safe feature flag definitions</li>
                                        <li>✓ Multiple evaluation modes (toggle, scheduled, percentage rollouts)</li>
                                        <li>✓ Advanced targeting rules and user/tenant management</li>
                                        <li>✓ Time window controls for business hours operations</li>
                                        <li>✓ Real-time flag evaluation and testing</li>
                                        <li>✓ Comprehensive filtering and search capabilities</li>
                                    </ul>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-2">Technology Stack</h3>
                                    <div className="text-gray-700 space-y-1">
                                        <p><strong>Backend:</strong> .NET 9, C# 13, ASP.NET Core</p>
                                        <p><strong>Frontend:</strong> React, TypeScript, Tailwind CSS</p>
                                        <p><strong>Database:</strong> PostgreSQL, SQL Server support</p>
                                        <p><strong>Caching:</strong> Redis, In-Memory</p>
                                    </div>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-2">License</h3>
                                    <p className="text-gray-700">
                                        Licensed under the Apache License 2.0. Open source and free to use in commercial applications.
                                    </p>
                                </div>
                            </div>

                            <div className="mt-6 pt-4 border-t border-gray-200">
                                <button
                                    onClick={() => setShowAboutModal(false)}
                                    className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
                                >
                                    Close
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Feature Flags Guide Modal */}
            {showFlagsInfoModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
                    <div className="bg-white rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] overflow-y-auto">
                        <div className="p-6">
                            <div className="flex justify-between items-start mb-6">
                                <h2 className="text-2xl font-bold text-gray-900">Feature Flags Guide</h2>
                                <button
                                    onClick={() => setShowFlagsInfoModal(false)}
                                    className="text-gray-400 hover:text-gray-600 p-1"
                                >
                                    <X className="w-6 h-6" />
                                </button>
                            </div>

                            <div className="space-y-6">
                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-3">Purpose of Feature Flags</h3>
                                    <p className="text-gray-700 leading-relaxed mb-4">
                                        Feature flags (also known as feature toggles or feature switches) are a software development 
                                        technique that allows teams to enable or disable features in production without deploying new code. 
                                        They provide a powerful way to control feature rollouts, perform A/B testing, and reduce deployment risks.
                                    </p>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-3">Management Strategies</h3>
                                    <div className="grid md:grid-cols-2 gap-4">
                                        <div className="space-y-3">
                                            <h4 className="font-medium text-gray-900">Release Management</h4>
                                            <ul className="text-sm text-gray-700 space-y-1">
                                                <li>✓ Gradual feature rollouts</li>
                                                <li>✓ Canary deployments</li>
                                                <li>✓ Emergency kill switches</li>
                                                <li>✓ Scheduled releases</li>
                                            </ul>
                                        </div>
                                        <div className="space-y-3">
                                            <h4 className="font-medium text-gray-900">User Experience</h4>
                                            <ul className="text-sm text-gray-700 space-y-1">
                                                <li>✓ A/B testing and experiments</li>
                                                <li>✓ Personalized experiences</li>
                                                <li>✓ Beta user programs</li>
                                                <li>✓ Performance optimization</li>
                                            </ul>
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-3">Flag Evaluation Modes</h3>
                                    <div className="space-y-3">
                                        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
                                            <div className="border border-gray-200 rounded-lg p-3">
                                                <h4 className="font-medium text-gray-900 text-sm">Simple Toggle</h4>
                                                <p className="text-xs text-gray-600 mt-1">Basic on/off switching for quick feature control</p>
                                            </div>
                                            <div className="border border-gray-200 rounded-lg p-3">
                                                <h4 className="font-medium text-gray-900 text-sm">Scheduled</h4>
                                                <p className="text-xs text-gray-600 mt-1">Time-based activation for coordinated releases</p>
                                            </div>
                                            <div className="border border-gray-200 rounded-lg p-3">
                                                <h4 className="font-medium text-gray-900 text-sm">Time Window</h4>
                                                <p className="text-xs text-gray-600 mt-1">Business hours or maintenance window controls</p>
                                            </div>
                                            <div className="border border-gray-200 rounded-lg p-3">
                                                <h4 className="font-medium text-gray-900 text-sm">User Targeting</h4>
                                                <p className="text-xs text-gray-600 mt-1">Specific user allowlists and blocklists</p>
                                            </div>
                                            <div className="border border-gray-200 rounded-lg p-3">
                                                <h4 className="font-medium text-gray-900 text-sm">Percentage Rollouts</h4>
                                                <p className="text-xs text-gray-600 mt-1">Gradual rollouts to percentage of users/tenants</p>
                                            </div>
                                            <div className="border border-gray-200 rounded-lg p-3">
                                                <h4 className="font-medium text-gray-900 text-sm">Custom Rules</h4>
                                                <p className="text-xs text-gray-600 mt-1">Advanced targeting based on attributes</p>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-3">Flag Varieties</h3>
                                    <div className="grid md:grid-cols-2 gap-4">
                                        <div className="space-y-3">
                                            <h4 className="font-medium text-gray-900">By Scope</h4>
                                            <ul className="text-sm text-gray-700 space-y-1">
                                                <li><strong>Global Flags:</strong> Shared across all applications</li>
                                                <li><strong>Application Flags:</strong> Specific to individual applications</li>
                                            </ul>
                                        </div>
                                        <div className="space-y-3">
                                            <h4 className="font-medium text-gray-900">By Lifecycle</h4>
                                            <ul className="text-sm text-gray-700 space-y-1">
                                                <li><strong>Permanent Flags:</strong> Long-term configuration switches</li>
                                                <li><strong>Temporary Flags:</strong> Short-term release management</li>
                                                <li><strong>Experiment Flags:</strong> A/B testing and experiments</li>
                                            </ul>
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <h3 className="text-lg font-semibold text-gray-900 mb-3">Best Practices</h3>
                                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                                        <ul className="text-sm text-gray-700 space-y-2">
                                            <li>✓ <strong>Start simple:</strong> Begin with basic on/off flags before advanced targeting</li>
                                            <li>✓ <strong>Clean up regularly:</strong> Remove obsolete flags to prevent technical debt</li>
                                            <li>✓ <strong>Use descriptive names:</strong> Clear, consistent naming conventions</li>
                                            <li>✓ <strong>Monitor performance:</strong> Track flag evaluation impact on application performance</li>
                                            <li>✓ <strong>Plan rollback strategies:</strong> Always have a plan to disable features quickly</li>
                                            <li>✓ <strong>Test thoroughly:</strong> Validate flag behavior in all states before deployment</li>
                                        </ul>
                                    </div>
                                </div>
                            </div>

                            <div className="mt-8 pt-4 border-t border-gray-200">
                                <button
                                    onClick={() => setShowFlagsInfoModal(false)}
                                    className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
                                >
                                    Close Guide
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};