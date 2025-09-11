import { useState, useEffect } from 'react';
import { Target, Plus, Trash2, X, Info } from 'lucide-react';
import type { FeatureFlagDto, TargetingRule, TargetingRulesRequest } from '../../services/apiService';
import { getTargetingOperators, getTargetingOperatorLabel, TargetingOperator, parseTargetingRules } from '../../services/apiService';
import { parseStatusComponents, hasValidTargetingRules } from '../../utils/flagHelpers';

interface TargetingRulesStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const TargetingRulesStatusIndicator: React.FC<TargetingRulesStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);
	
	// Parse targeting rules from JSON string
	const targetingRules = parseTargetingRules(flag.targetingRules);
	const targetingRulesCount = targetingRules.length;

	if (!components.hasTargetingRules && targetingRulesCount === 0) return null;

	// Get unique attributes from the rules for display
	const uniqueAttributes = targetingRules.length > 0 
		? [...new Set(targetingRules.map(rule => rule?.attribute).filter(attr => attr))]
		: [];

	return (
		<div className="mb-4 p-4 bg-orange-50 border border-orange-200 rounded-lg">
			<div className="flex items-center gap-2 mb-3">
				<Target className="w-4 h-4 text-orange-600" />
				<h4 className="font-medium text-orange-900">Custom Targeting Rules</h4>
			</div>

			<div className="space-y-2">
				<div className="flex items-center gap-2 text-sm">
					<span className="font-medium">Active Rules:</span>
					<span className="text-orange-700 font-semibold">{targetingRulesCount} rule{targetingRulesCount !== 1 ? 's' : ''}</span>
				</div>
				
				{/* Show rule attributes instead of full rule details */}
				{uniqueAttributes.length > 0 && (
					<div className="flex items-center gap-2 text-sm">
						<span className="font-medium">Targeting:</span>
						<div className="flex flex-wrap gap-1">
							{uniqueAttributes.slice(0, 3).map((attribute, index) => (
								<span key={index} className="text-xs text-orange-700 bg-orange-100 rounded px-2 py-1 font-mono">
									{attribute}
								</span>
							))}
							{uniqueAttributes.length > 3 && (
								<span className="text-xs text-orange-600 italic">
									+{uniqueAttributes.length - 3} more
								</span>
							)}
						</div>
					</div>
				)}
			</div>
		</div>
	);
};

interface TargetingRulesSectionProps {
	flag: FeatureFlagDto;
	onUpdateTargetingRules: (targetingRules?: TargetingRule[], removeTargetingRules?: boolean) => Promise<void>;
	onClearTargetingRules: () => Promise<void>;
	operationLoading: boolean;
}

interface TargetingRuleForm {
	attribute: string;
	operator: TargetingOperator;
	values: string[];
	variation: string;
}

const emptyRule: TargetingRuleForm = {
	attribute: '',
	operator: TargetingOperator.Equals,
	values: [''],
	variation: 'on'
};

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
				<div className="absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-xs text-white bg-gray-900 rounded-lg shadow-lg max-w-sm whitespace-normal">
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-gray-900"></div>
				</div>
			)}
		</div>
	);
};

// Helper function to safely convert operator
const safeConvertOperator = (operator: any): TargetingOperator => {
	if (typeof operator === 'number') {
		return operator as TargetingOperator;
	}
	if (typeof operator === 'string') {
		const operatorMap: Record<string, TargetingOperator> = {
			'Equals': TargetingOperator.Equals,
			'NotEquals': TargetingOperator.NotEquals,
			'Contains': TargetingOperator.Contains,
			'NotContains': TargetingOperator.NotContains,
			'In': TargetingOperator.In,
			'NotIn': TargetingOperator.NotIn,
			'GreaterThan': TargetingOperator.GreaterThan,
			'LessThan': TargetingOperator.LessThan
		};
		return operatorMap[operator] || TargetingOperator.Equals;
	}
	return TargetingOperator.Equals;
};

export const TargetingRulesSection: React.FC<TargetingRulesSectionProps> = ({
	flag,
	onUpdateTargetingRules,
	onClearTargetingRules,
	operationLoading
}) => {
	const [editingTargetingRules, setEditingTargetingRules] = useState(false);
	const [targetingRulesForm, setTargetingRulesForm] = useState<TargetingRuleForm[]>([]);

	const components = parseStatusComponents(flag);
	const targetingOperators = getTargetingOperators();

	// Update local state when flag changes - parse JSON string to array
	useEffect(() => {
		console.log('TargetingRulesSection Effect Debug:', {
			flagKey: flag.key,
			targetingRulesJson: flag.targetingRules,
			isString: typeof flag.targetingRules === 'string'
		});

		try {
			// Parse targeting rules from JSON string
			const targetingRules = parseTargetingRules(flag.targetingRules);
			
			if (targetingRules.length > 0) {
				const safeRules = targetingRules.map((rule, index) => {
					console.log(`Processing rule ${index}:`, rule);
					return {
						attribute: rule?.attribute || '',
						operator: safeConvertOperator(rule?.operator),
						values: Array.isArray(rule?.values) ? [...rule.values] : [''],
						variation: rule?.variation || 'on'
					};
				});
				setTargetingRulesForm(safeRules);
			} else {
				setTargetingRulesForm([]);
			}
		} catch (error) {
			console.error('Error processing targeting rules:', error);
			setTargetingRulesForm([]);
		}
	}, [flag.key, flag.targetingRules]);

	const handleTargetingRulesSubmit = async () => {
		try {
			// Convert form data to API format with null safety
			const targetingRules: TargetingRule[] = targetingRulesForm
				.filter(rule => rule?.attribute?.trim() && Array.isArray(rule?.values) && rule.values.some(v => v?.trim()))
				.map(rule => ({
					attribute: rule.attribute.trim(),
					operator: getTargetingOperatorLabel(rule.operator), // Convert to string for API
					values: rule.values.filter(v => v?.trim()).map(v => v.trim()),
					variation: rule.variation?.trim() || 'on'
				}));

			console.log('Submitting targeting rules:', targetingRules);
			
			// Use the callback prop to update targeting rules
			await onUpdateTargetingRules(
				targetingRules.length > 0 ? targetingRules : undefined,
				targetingRules.length === 0
			);
			
			setEditingTargetingRules(false);
		} catch (error) {
			console.error('Failed to update targeting rules:', error);
		}
	};

	const handleClearTargetingRules = async () => {
		try {
			// Use the callback prop to clear targeting rules
			await onClearTargetingRules();
		} catch (error) {
			console.error('Failed to clear targeting rules:', error);
		}
	};

	const addRule = () => {
		setTargetingRulesForm([...targetingRulesForm, { ...emptyRule }]);
	};

	const removeRule = (index: number) => {
		setTargetingRulesForm(targetingRulesForm.filter((_, i) => i !== index));
	};

	const updateRule = (index: number, updates: Partial<TargetingRuleForm>) => {
		setTargetingRulesForm(targetingRulesForm.map((rule, i) => 
			i === index ? { ...rule, ...updates } : rule
		));
	};

	const addValue = (ruleIndex: number) => {
		const updatedRules = [...targetingRulesForm];
		if (updatedRules[ruleIndex] && Array.isArray(updatedRules[ruleIndex].values)) {
			updatedRules[ruleIndex].values.push('');
			setTargetingRulesForm(updatedRules);
		}
	};

	const removeValue = (ruleIndex: number, valueIndex: number) => {
		const updatedRules = [...targetingRulesForm];
		if (updatedRules[ruleIndex] && Array.isArray(updatedRules[ruleIndex].values)) {
			updatedRules[ruleIndex].values = updatedRules[ruleIndex].values.filter((_, i) => i !== valueIndex);
			setTargetingRulesForm(updatedRules);
		}
	};

	const updateValue = (ruleIndex: number, valueIndex: number, value: string) => {
		const updatedRules = [...targetingRulesForm];
		if (updatedRules[ruleIndex] && Array.isArray(updatedRules[ruleIndex].values)) {
			updatedRules[ruleIndex].values[valueIndex] = value;
			setTargetingRulesForm(updatedRules);
		}
	};

	// Parse targeting rules from JSON string for display
	const targetingRules = parseTargetingRules(flag.targetingRules);
	const hasTargetingRules = targetingRules.length > 0;

	const resetForm = () => {
		try {
			if (hasTargetingRules) {
				const safeRules = targetingRules.map(rule => ({
					attribute: rule?.attribute || '',
					operator: safeConvertOperator(rule?.operator),
					values: Array.isArray(rule?.values) ? [...rule.values] : [''],
					variation: rule?.variation || 'on'
				}));
				setTargetingRulesForm(safeRules);
			} else {
				setTargetingRulesForm([]);
			}
		} catch (error) {
			console.error('Error resetting form:', error);
			setTargetingRulesForm([]);
		}
	};

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">Custom Targeting Rules</h4>
					<InfoTooltip content="Advanced conditional logic for complex feature targeting. Create rules based on user attributes (userId, country, plan, etc.). Variation determines which feature version users get when rules match. Setup: Add attribute name, choose operator (equals, contains, etc.), specify values to match, and set the variation to return. Multiple rules are evaluated in order until one matches." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingTargetingRules(true)}
						disabled={operationLoading}
						className="text-orange-600 hover:text-orange-800 text-sm flex items-center gap-1 disabled:opacity-50"
						data-testid="manage-targeting-rules-button"
					>
						<Target className="w-4 h-4" />
						Configure Rules
					</button>
					{hasTargetingRules && (
						<button
							onClick={handleClearTargetingRules}
							disabled={operationLoading}
							className="text-red-600 hover:text-red-800 text-sm flex items-center gap-1 disabled:opacity-50"
							title="Clear All Targeting Rules"
							data-testid="clear-targeting-rules-button"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingTargetingRules ? (
				<div className="bg-orange-50 border border-orange-200 rounded-lg p-4">
					<div className="space-y-4">
						<div className="flex justify-between items-center">
							<h5 className="font-medium text-orange-800">Targeting Rules Configuration</h5>
							<button
								onClick={addRule}
								disabled={operationLoading}
								className="text-orange-600 hover:text-orange-800 text-sm flex items-center gap-1 disabled:opacity-50"
							>
								<Plus className="w-4 h-4" />
								Add Rule
							</button>
						</div>
						
						{targetingRulesForm.length === 0 ? (
							<div className="text-center py-8 text-orange-600">
								<Target className="w-8 h-8 mx-auto mb-2 opacity-50" />
								<p className="text-sm">No targeting rules configured</p>
								<p className="text-xs mt-1">Click "Add Rule" to create your first targeting rule</p>
							</div>
						) : (
							<div className="space-y-4">
								{targetingRulesForm.map((rule, ruleIndex) => (
									<div key={ruleIndex} className="border border-orange-300 rounded-lg p-3 bg-white">
										<div className="flex justify-between items-start mb-3">
											<span className="text-sm font-medium text-orange-700">Rule #{ruleIndex + 1}</span>
											<button
												onClick={() => removeRule(ruleIndex)}
												disabled={operationLoading}
												className="text-red-500 hover:text-red-700 p-1"
												title="Remove Rule"
											>
												<Trash2 className="w-4 h-4" />
											</button>
										</div>
										
										<div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-3">
											{/* Attribute */}
											<div>
												<label className="block text-xs font-medium text-orange-700 mb-1">Attribute</label>
												<input
													type="text"
													value={rule?.attribute || ''}
													onChange={(e) => updateRule(ruleIndex, { attribute: e.target.value })}
													placeholder="userId, tenantId, country..."
													className="w-full border border-orange-300 rounded px-2 py-1 text-xs"
													disabled={operationLoading}
												/>
											</div>
											
											{/* Operator */}
											<div>
												<label className="block text-xs font-medium text-orange-700 mb-1">Operator</label>
												<select
													value={rule?.operator ?? TargetingOperator.Equals}
													onChange={(e) => updateRule(ruleIndex, { operator: parseInt(e.target.value) as TargetingOperator })}
													className="w-full border border-orange-300 rounded px-2 py-1 text-xs"
													disabled={operationLoading}
												>
													{targetingOperators.map(op => (
														<option key={op.value} value={op.value} title={op.description}>
															{op.label}
														</option>
													))}
												</select>
											</div>
											
											{/* Variation */}
											<div>
												<label className="block text-xs font-medium text-orange-700 mb-1">Variation</label>
												<input
													type="text"
													value={rule?.variation || ''}
													onChange={(e) => updateRule(ruleIndex, { variation: e.target.value })}
													placeholder="on, off, v1, v2..."
													className="w-full border border-orange-300 rounded px-2 py-1 text-xs"
													disabled={operationLoading}
												/>
											</div>
											
											{/* Add Value Button */}
											<div className="flex items-end">
												<button
													onClick={() => addValue(ruleIndex)}
													disabled={operationLoading}
													className="w-full px-2 py-1 text-xs bg-orange-600 text-white rounded hover:bg-orange-700 disabled:opacity-50 flex items-center justify-center gap-1"
												>
													<Plus className="w-3 h-3" />
													Add Value
												</button>
											</div>
										</div>
										
										{/* Values */}
										<div>
											<label className="block text-xs font-medium text-orange-700 mb-1">Values</label>
											<div className="grid grid-cols-1 md:grid-cols-2 gap-2">
												{Array.isArray(rule?.values) && rule.values.map((value, valueIndex) => (
													<div key={valueIndex} className="flex gap-1">
														<input
															type="text"
															value={value || ''}
															onChange={(e) => updateValue(ruleIndex, valueIndex, e.target.value)}
															placeholder="Enter value..."
															className="flex-1 border border-orange-300 rounded px-2 py-1 text-xs"
															disabled={operationLoading}
														/>
														{rule.values.length > 1 && (
															<button
																onClick={() => removeValue(ruleIndex, valueIndex)}
																disabled={operationLoading}
																className="text-red-500 hover:text-red-700 p-1"
																title="Remove Value"
															>
																<X className="w-3 h-3" />
															</button>
														)}
													</div>
												))}
											</div>
										</div>
									</div>
								))}

								{/* Debugging: Show parsed targeting rules */}
								<div className="p-4 bg-gray-50 border border-gray-300 rounded-lg">
									<h6 className="font-medium text-gray-800 mb-2">Debug Info</h6>
									<div className="text-xs text-gray-600">
										<div className="mb-2">
											<strong>Original JSON:</strong> {flag.targetingRules}
										</div>
										<div>
											<strong>Parsed Rules:</strong>
											{targetingRulesForm.map((rule, index) => (
												<div key={index} className="whitespace-pre-wrap">
													{`Rule ${index + 1}: ${JSON.stringify(rule, null, 2)}`}
												</div>
											))}
										</div>
									</div>
								</div>
							</div>
						)}
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleTargetingRulesSubmit}
							disabled={operationLoading}
							className="px-3 py-1 bg-orange-600 text-white rounded text-sm hover:bg-orange-700 disabled:opacity-50"
							data-testid="save-targeting-rules-button"
						>
							{operationLoading ? 'Saving...' : 'Save Targeting Rules'}
						</button>
						<button
							onClick={() => {
								setEditingTargetingRules(false);
								resetForm();
							}}
							disabled={operationLoading}
							className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
							data-testid="cancel-targeting-rules-button"
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
							return <div className="text-green-600 font-medium">No custom targeting - flag enabled for all users</div>;
						}

						// Check if targeting rules are set
						if (hasTargetingRules) {
							return (
								<div className="space-y-2">
									<div>Active Targeting Rules: {targetingRules.length}</div>
									<div className="space-y-1">
										{targetingRules.slice(0, 3).map((rule, index) => (
											<div key={index} className="text-xs bg-gray-100 rounded px-2 py-1 font-mono">
												{rule?.attribute || 'Unknown'} {getTargetingOperatorLabel(rule?.operator).toLowerCase()} [{Array.isArray(rule?.values) ? rule.values.join(', ') : 'No values'}] → {rule?.variation || 'on'}
											</div>
										))}
										{targetingRules.length > 3 && (
											<div className="text-xs text-gray-500 italic">
												...and {targetingRules.length - 3} more rule{targetingRules.length - 3 !== 1 ? 's' : ''}
											</div>
										)}
									</div>
								</div>
							);
						}

						// Check if no targeting rules and not enabled
						if (!hasTargetingRules && components.baseStatus === 'Other') {
							return <div className="text-gray-500 italic">No custom targeting rules configured</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className="text-orange-600 font-medium">Custom targeting disabled - flag is disabled</div>;
						}

						return <div className="text-gray-500 italic">Targeting rules configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};