import { useState, useEffect } from 'react';
import { Palette, Info, Plus, Trash2, X } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { getSectionClasses, theme } from '../../styles/theme';

interface VariationSectionProps {
	flag: FeatureFlagDto;
	onUpdateVariations?: (variations: Record<string, any>, defaultVariation: string) => Promise<void>;
	onClearVariations?: () => Promise<void>;
	operationLoading: boolean;
}

interface VariationForm {
	key: string;
	value: string;
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

// Helper function to parse variation value from string input
const parseVariationValue = (value: string): any => {
	if (value === 'true') return true;
	if (value === 'false') return false;
	if (value === 'null') return null;
	if (!isNaN(Number(value)) && value.trim() !== '') return Number(value);
	
	// Try to parse as JSON for objects/arrays
	try {
		return JSON.parse(value);
	} catch {
		// Return as string if not valid JSON
		return value;
	}
};

export const VariationSection: React.FC<VariationSectionProps> = ({
	flag,
	onUpdateVariations,
	onClearVariations,
	operationLoading
}) => {
	const [editingVariations, setEditingVariations] = useState(false);
	const [variationsForm, setVariationsForm] = useState<VariationForm[]>([]);
	const [defaultVariation, setDefaultVariation] = useState('');
	
	const hasCustomVariations = checkForCustomVariations(flag);
	const schedulingStyles = getSectionClasses('scheduling');
	
	// Check if variations exist and are not just the default on/off
	const hasVariations = flag.variations?.values && Object.keys(flag.variations.values).length > 0;

	useEffect(() => {
		try {
			const variations = flag.variations?.values || {};
			const variationEntries = Object.entries(variations);

			if (variationEntries.length > 0) {
				const formData = variationEntries.map(([key, value]) => ({
					key,
					value: formatVariationValue(value)
				}));
				setVariationsForm(formData);
			} else {
				setVariationsForm([]);
			}

			setDefaultVariation(flag.variations?.defaultVariation || 'off');
		} catch (error) {
			console.error('Error processing variations:', error);
			setVariationsForm([]);
			setDefaultVariation('off');
		}
	}, [flag.key, flag.variations]);

	const handleVariationsSubmit = async () => {
		if (!onUpdateVariations) return;

		try {
			const variations: Record<string, any> = {};
			
			variationsForm
				.filter(variation => variation.key?.trim() && variation.value?.trim())
				.forEach(variation => {
					variations[variation.key.trim()] = parseVariationValue(variation.value.trim());
				});

			await onUpdateVariations(variations, defaultVariation);
			setEditingVariations(false);
		} catch (error) {
			console.error('Failed to update variations:', error);
		}
	};

	const handleClearVariations = async () => {
		if (!onClearVariations) return;

		try {
			await onClearVariations();
		} catch (error) {
			console.error('Failed to clear variations:', error);
		}
	};

	const addVariation = () => {
		setVariationsForm([...variationsForm, { key: '', value: '' }]);
	};

	const removeVariation = (index: number) => {
		setVariationsForm(variationsForm.filter((_, i) => i !== index));
	};

	const updateVariation = (index: number, field: 'key' | 'value', value: string) => {
		setVariationsForm(variationsForm.map((variation, i) =>
			i === index ? { ...variation, [field]: value } : variation
		));
	};

	const resetForm = () => {
		try {
			const variations = flag.variations?.values || {};
			const variationEntries = Object.entries(variations);

			if (variationEntries.length > 0) {
				const formData = variationEntries.map(([key, value]) => ({
					key,
					value: formatVariationValue(value)
				}));
				setVariationsForm(formData);
			} else {
				setVariationsForm([]);
			}

			setDefaultVariation(flag.variations?.defaultVariation || 'off');
		} catch (error) {
			console.error('Error resetting form:', error);
			setVariationsForm([]);
			setDefaultVariation('off');
		}
	};

	const variations = flag.variations?.values || {};
	const variationEntries = Object.entries(variations);

	const variationStyles = getSectionClasses('variations');

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">Variations</h4>
					<InfoTooltip content="Custom variations define different feature values returned when the flag is enabled. Users can receive different variations based on targeting rules or hash-based selection." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingVariations(true)
						}
						disabled={operationLoading}
						className={`text-sm flex items-center gap-1 disabled:opacity-50 ${variationStyles.buttonText} ${variationStyles.buttonHover}`}
						data-testid="edit-variations-button"
					>
						<Palette className="w-4 h-4" />
						Configure Variations
					</button>
					{hasVariations && (
						<button
							onClick={handleClearVariations}
							disabled={operationLoading}
							className="text-red-600 hover:text-red-800 text-sm flex items-center gap-1 disabled:opacity-50"
							title="Clear All Variations"
							data-testid="clear-variations-button"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingVariations ? (
				<div className={`${variationStyles.bg} ${variationStyles.border} border rounded-lg p-4`}>
					<div className="space-y-4">
						<div className="flex justify-between items-center">
							<h5 className={`font-medium ${schedulingStyles.text}`}>Variation Configuration</h5>
							<button
								onClick={addVariation}
								disabled={operationLoading}
								className={`${schedulingStyles.buttonText} hover:${schedulingStyles.text} text-sm flex items-center gap-1 disabled:opacity-50`}
							>
								<Plus className="w-4 h-4" />
								Add Variation
							</button>
						</div>

						<div className="space-y-3">
							<div>
								<label className={`block text-sm font-medium ${schedulingStyles.text} mb-2`}>
									Default Variation
								</label>
								<input
									type="text"
									value={defaultVariation}
									onChange={(e) => setDefaultVariation(e.target.value)}
									placeholder="off"
									className={`w-full border ${schedulingStyles.border} rounded px-3 py-2 text-sm`}
									disabled={operationLoading}
								/>
								<p className={`text-xs ${schedulingStyles.text} mt-1`}>
									This variation is used when no other conditions are met
								</p>
							</div>

							<div>
								<label className={`block text-sm font-medium ${schedulingStyles.text} mb-2`}>
									Variations ({variationsForm.length})
								</label>
								
								{variationsForm.length === 0 ? (
									<div className={`text-center py-8 ${schedulingStyles.text}`}>
										<Palette className="w-8 h-8 mx-auto mb-2 opacity-50" />
										<p className="text-sm">No variations configured</p>
										<p className="text-xs mt-1">Click "Add Variation" to create your first variation</p>
									</div>
								) : (
									<div className="space-y-2">
										{variationsForm.map((variation, index) => (
											<div key={index} className={`border ${schedulingStyles.border} rounded-lg p-3 bg-white`}>
												<div className="flex justify-between items-start mb-3">
													<span className={`text-sm font-medium ${schedulingStyles.text}`}>Variation #{index + 1}</span>
													{variationsForm.length > 1 && (
														<button
															onClick={() => removeVariation(index)}
															disabled={operationLoading}
															className="text-red-500 hover:text-red-700 p-1"
															title="Remove Variation"
														>
															<Trash2 className="w-4 h-4" />
														</button>
													)}
												</div>

												<div className="grid grid-cols-1 md:grid-cols-2 gap-3">
													<div>
														<label className={`block text-xs font-medium ${schedulingStyles.text} mb-1`}>Key</label>
														<input
															type="text"
															value={variation.key}
															onChange={(e) => updateVariation(index, 'key', e.target.value)}
															placeholder="on, off, v1, v2..."
															className={`w-full border ${schedulingStyles.border} rounded px-2 py-1 text-xs`}
															disabled={operationLoading}
														/>
													</div>

													<div>
														<label className={`block text-xs font-medium ${schedulingStyles.text} mb-1`}>Value</label>
														<input
															type="text"
															value={variation.value}
															onChange={(e) => updateVariation(index, 'value', e.target.value)}
															placeholder='true, false, "text", 123, {"key":"value"}'
															className={`w-full border ${schedulingStyles.border} rounded px-2 py-1 text-xs`}
															disabled={operationLoading}
														/>
													</div>
												</div>
											</div>
										))}
									</div>
								)}
							</div>

							<div className="mt-4 p-3 bg-blue-50 border border-blue-200 rounded">
								<h6 className="text-sm font-medium text-blue-900 mb-1">How Variations Work</h6>
								<ul className="text-xs text-blue-800 space-y-1">
									<li>• Targeting rules can specify which variation to return</li>
									<li>• For percentage rollouts, variations are assigned via consistent hashing</li>
									<li>• If no conditions match, the default variation is returned</li>
									<li>• Values can be strings, booleans, numbers, or JSON objects</li>
								</ul>
							</div>
						</div>
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleVariationsSubmit}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.warning[600]} text-white rounded text-sm hover:bg-sky-700 disabled:opacity-50`}
							data-testid="save-variations-button"
						>
							{operationLoading ? 'Saving...' : 'Save Variations'}
						</button>
						<button
							onClick={() => {
								setEditingVariations(false);
								resetForm();
							}}
							disabled={operationLoading}
							className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
							data-testid="cancel-variations-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className="text-sm text-gray-600 space-y-1">
					{hasVariations ? (
						<div className="space-y-2">
							<div className="flex items-center gap-2">
								<span className="font-medium">Available Variations:</span>
								<span className={variationStyles.text}>{variationEntries.length} configured</span>
							</div>
							<div className="flex items-center gap-2">
								<span className="font-medium">Default:</span>
									<span className={`text-xs ${schedulingStyles.bg} ${schedulingStyles.text} px-2 py-1 rounded font-mono`}>
									{defaultVariation}
								</span>
									<span className={`${schedulingStyles.textPrimary}`}>
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
					) : (
						<div className="text-gray-500 italic">No variations configured</div>
					)}
				</div>
			)}
		</div>
	);
};