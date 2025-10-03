import { useState, useEffect } from 'react';
import { Palette, Info, Eye, Settings } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';

interface VariationStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const VariationStatusIndicator: React.FC<VariationStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);
	const hasCustomVariations = checkForCustomVariations(flag);

	if (!hasCustomVariations) return null;

	const variationCount = flag.variations?.values ? Object.keys(flag.variations.values).length : 0;
	const defaultVar = flag.variations?.defaultVariation || 'off';

	return (
		<div className="mb-4 p-4 bg-purple-50 border border-purple-200 rounded-lg">
			<div className="flex items-center gap-2 mb-3">
				<Palette className="w-4 h-4 text-purple-600" />
				<h4 className="font-medium text-purple-900">Custom Variations</h4>
			</div>

			<div className="space-y-2">
				<div className="flex items-center gap-2 text-sm">
					<span className="font-medium">Available Variations:</span>
					<span className="text-purple-700 font-semibold">{variationCount} variation{variationCount !== 1 ? 's' : ''}</span>
				</div>

				<div className="flex items-center gap-2 text-sm">
					<span className="font-medium">Default:</span>
					<span className="text-xs text-purple-700 bg-purple-100 rounded px-2 py-1 font-mono">
						{defaultVar}
					</span>
				</div>

				{flag.variations?.values && (
					<div className="flex items-center gap-2 text-sm">
						<span className="font-medium">Values:</span>
						<div className="flex flex-wrap gap-1">
							{Object.keys(flag.variations.values).slice(0, 3).map((key, index) => (
								<span key={index} className="text-xs text-purple-700 bg-purple-100 rounded px-2 py-1 font-mono">
									{key}
								</span>
							))}
							{Object.keys(flag.variations.values).length > 3 && (
								<span className="text-xs text-purple-600 italic">
									+{Object.keys(flag.variations.values).length - 3} more
								</span>
							)}
						</div>
					</div>
				)}
			</div>
		</div>
	);
};

interface VariationSectionProps {
	flag: FeatureFlagDto;
	operationLoading: boolean;
}

// BUG FIX #10: Make tooltip wider and more readable
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
				<div className="absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-sm leading-relaxed text-gray-800 bg-white border border-gray-300 rounded-lg shadow-lg min-w-[280px] max-w-[360px]">
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-white"></div>
				</div>
			)}
		</div>
	);
};

// Helper function to check for custom variations
export const checkForCustomVariations = (flag: FeatureFlagDto): boolean => {
	if (!flag.variations?.values) return false;

	const values = flag.variations.values;
	const keys = Object.keys(values);
	
	// Check if it's the default on/off structure
	const isDefaultOnOff = keys.length === 2 && 
		keys.includes('on') && keys.includes('off') &&
		values['on'] === true && values['off'] === false &&
		flag.variations.defaultVariation === 'off';

	return !isDefaultOnOff;
};

// Helper function to format variation values for display
const formatVariationValue = (value: any): string => {
	if (typeof value === 'string') return `"${value}"`;
	if (typeof value === 'boolean') return value.toString();
	if (typeof value === 'number') return value.toString();
	if (value === null) return 'null';
	if (typeof value === 'object') return JSON.stringify(value);
	return String(value);
};

export const VariationSection: React.FC<VariationSectionProps> = ({
	flag,
	operationLoading
}) => {
	const [showDetails, setShowDetails] = useState(false);
	const hasCustomVariations = checkForCustomVariations(flag);

	if (!hasCustomVariations) return null;

	const variations = flag.variations?.values || {};
	const defaultVariation = flag.variations?.defaultVariation || 'off';
	const variationEntries = Object.entries(variations);

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">Variations</h4>
					<InfoTooltip content="Custom variations define different feature values returned when the flag is enabled. Users can receive different variations based on targeting rules or hash-based selection." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setShowDetails(!showDetails)}
						disabled={operationLoading}
						className="text-purple-600 hover:text-purple-800 text-sm flex items-center gap-1 disabled:opacity-50"
						data-testid="toggle-variation-details-button"
					>
						<Eye className="w-4 h-4" />
						{showDetails ? 'Hide Details' : 'Show Details'}
					</button>
				</div>
			</div>

			{showDetails ? (
				<div className="bg-purple-50 border border-purple-200 rounded-lg p-4">
					<div className="space-y-4">
						<div className="flex items-center gap-2 mb-3">
							<Settings className="w-4 h-4 text-purple-600" />
							<h5 className="font-medium text-purple-800">Variation Configuration</h5>
						</div>

						<div className="space-y-3">
							<div>
								<label className="block text-sm font-medium text-purple-800 mb-2">
									Default Variation
								</label>
								<div className="px-3 py-2 bg-purple-100 text-purple-900 rounded font-mono text-sm border border-purple-300">
									{defaultVariation}
								</div>
								<p className="text-xs text-purple-600 mt-1">
									This variation is used when no other conditions are met
								</p>
							</div>

							<div>
								<label className="block text-sm font-medium text-purple-800 mb-2">
									Available Variations ({variationEntries.length})
								</label>
								<div className="space-y-2">
									{variationEntries.map(([key, value], index) => (
										<div 
											key={index} 
											className={`flex items-center justify-between p-3 rounded border ${
												key === defaultVariation 
													? 'bg-purple-100 border-purple-300' 
													: 'bg-white border-purple-200'
											}`}
										>
											<div className="flex items-center gap-2">
												<span className="font-mono text-sm font-medium text-purple-900">
													{key}
												</span>
												{key === defaultVariation && (
													<span className="text-xs px-2 py-0.5 bg-purple-600 text-white rounded">
														default
													</span>
												)}
											</div>
											<div className="font-mono text-sm text-gray-700 bg-gray-100 px-2 py-1 rounded">
												{formatVariationValue(value)}
											</div>
										</div>
									))}
								</div>
							</div>

							<div className="mt-4 p-3 bg-blue-50 border border-blue-200 rounded">
								<h6 className="text-sm font-medium text-blue-900 mb-1">How Variations Work</h6>
								<ul className="text-xs text-blue-800 space-y-1">
									<li>• Targeting rules can specify which variation to return</li>
									<li>• For percentage rollouts, variations are assigned via consistent hashing</li>
									<li>• If no conditions match, the default variation is returned</li>
									<li>• Variations allow A/B testing and gradual feature rollouts</li>
								</ul>
							</div>
						</div>
					</div>
				</div>
			) : (
				<div className="text-sm text-gray-600 space-y-1">
					<div className="space-y-2">
						<div className="flex items-center gap-2">
							<span className="font-medium">Available Variations:</span>
							<span className="text-purple-700">{variationEntries.length} configured</span>
						</div>
						<div className="flex items-center gap-2">
							<span className="font-medium">Default:</span>
							<span className="text-xs bg-purple-100 text-purple-700 px-2 py-1 rounded font-mono">
								{defaultVariation}
							</span>
							<span className="text-purple-600">
								→ {formatVariationValue(variations[defaultVariation])}
							</span>
						</div>
						<div className="flex items-start gap-2">
							<span className="font-medium">Keys:</span>
							<div className="flex flex-wrap gap-1">
								{variationEntries.slice(0, 4).map(([key], index) => (
									<span key={index} className="text-xs bg-gray-100 text-gray-700 px-2 py-1 rounded font-mono">
										{key}
									</span>
								))}
								{variationEntries.length > 4 && (
									<span className="text-xs text-gray-500 italic">
										+{variationEntries.length - 4} more
									</span>
								)}
							</div>
						</div>
					</div>
				</div>
			)}
		</div>
	);
};