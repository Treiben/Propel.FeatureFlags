import { useState, useEffect } from 'react';
import { Target, Plus, Trash2, X, Info } from 'lucide-react';
import type { FeatureFlagDto, TargetingRule } from '../../services/apiService';
import { getTargetingOperators, getTargetingOperatorLabel, TargetingOperator, parseTargetingRules } from '../../services/apiService';
import { parseStatusComponents } from '../../utils/flagHelpers';
import { getSectionClasses, theme } from '../../styles/theme';

interface TargetingRulesStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const TargetingRulesStatusIndicator: React.FC<TargetingRulesStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);

	const targetingRules = parseTargetingRules(flag.targetingRules);
	const targetingRulesCount = targetingRules.length;

	if (!components.hasTargetingRules && targetingRulesCount === 0) return null;

	const uniqueAttributes = targetingRules.length > 0
		? [...new Set(targetingRules.map(rule => rule?.attribute).filter(attr => attr))]
		: [];

	const schedulingStyles = getSectionClasses('scheduling');

	return (
		<div className={`mb-4 p-4 ${schedulingStyles.bg} ${schedulingStyles.border} border rounded-lg`}>
			<div className="flex items-center gap-2 mb-3">
				<Target className={`w-4 h-4 ${schedulingStyles.buttonText}`} />
				<h4 className={`font-medium ${schedulingStyles.textPrimary}`}>Custom Targeting Rules</h4>
			</div>

			<div className="space-y-2">
				<div className="flex items-center gap-2 text-sm">
					<span className="font-medium">Active Rules:</span>
					<span className={`${schedulingStyles.text} font-semibold`}>{targetingRulesCount} rule{targetingRulesCount !== 1 ? 's' : ''}</span>
				</div>

				{uniqueAttributes.length > 0 && (
					<div className="flex items-center gap-2 text-sm">
						<span className="font-medium">Targeting:</span>
						<div className="flex flex-wrap gap-1">
							{uniqueAttributes.slice(0, 3).map((attribute, index) => (
								<span key={index} className={`text-xs ${schedulingStyles.text} ${theme.warning[100]} rounded px-2 py-1 font-mono`}>
									{attribute}
								</span>
							))}
							{uniqueAttributes.length > 3 && (
								<span className={`text-xs ${schedulingStyles.buttonText} italic`}>
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
	const schedulingStyles = getSectionClasses('scheduling');

	useEffect(() => {
		try {
			const targetingRules = parseTargetingRules(flag.targetingRules);

			if (targetingRules.length > 0) {
				const safeRules = targetingRules.map(rule => ({
					attribute: rule?.attribute || '',
					operator: safeConvertOperator(rule?.operator),
					// Convert all values to strings for display in input fields
					values: Array.isArray(rule?.values) 
						? rule.values.map(v => typeof v === 'number' ? v.toString() : (v || ''))
						: [''],
					variation: rule?.variation || 'on'
				}));
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
			const targetingRules: TargetingRule[] = targetingRulesForm
				.filter(rule => {
					if (!rule?.attribute?.trim()) return false;
					if (!Array.isArray(rule?.values)) return false;
					// Check if at least one value exists (handle both strings and numbers)
					return rule.values.some(v => {
						if (typeof v === 'string') return v.trim();
						if (typeof v === 'number') return true;
						return false;
					});
				})
				.map(rule => ({
					attribute: rule.attribute.trim(),
					operator: rule.operator,
					// Convert all values to strings and trim string values
					values: rule.values
						.filter(v => {
							if (typeof v === 'string') return v.trim();
							if (typeof v === 'number') return true;
							return false;
						})
						.map(v => {
							if (typeof v === 'string') return v.trim();
							if (typeof v === 'number') return v.toString();
							return String(v);
						}),
					variation: rule.variation?.trim() || 'on'
				}));

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

	const targetingRules = parseTargetingRules(flag.targetingRules);
	const hasTargetingRules = targetingRules.length > 0;

	const resetForm = () => {
		try {
			if (hasTargetingRules) {
				const safeRules = targetingRules.map(rule => ({
					attribute: rule?.attribute || '',
					operator: safeConvertOperator(rule?.operator),
					// Convert all values to strings for display in input fields
					values: Array.isArray(rule?.values) 
						? rule.values.map(v => typeof v === 'number' ? v.toString() : (v || ''))
						: [''],
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
					<h4 className={`font-medium ${theme.neutral.text[900]}`}>Custom Targeting Rules</h4>
					<InfoTooltip content="Advanced conditional logic for complex feature targeting. Create rules based on user attributes (userId, country, plan, etc.). Variation determines which feature version users get when rules match." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingTargetingRules(true)}
						disabled={operationLoading}
						className={`text-sm flex items-center gap-1 disabled:opacity-50 ${schedulingStyles.buttonText} ${schedulingStyles.buttonHover}`}
						data-testid="manage-targeting-rules-button"
					>
						<Target className="w-4 h-4" />
						Configure Rules
					</button>
					{hasTargetingRules && (
						<button
							onClick={handleClearTargetingRules}
							disabled={operationLoading}
							className={`${theme.danger.text[600]} ${theme.danger.hover.text800} text-sm flex items-center gap-1 disabled:opacity-50`}
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
				<div className={`${schedulingStyles.bg} ${schedulingStyles.border} border rounded-lg p-4`}>
					<div className="space-y-4">
						<div className="flex justify-between items-center">
							<h5 className={`font-medium ${schedulingStyles.text}`}>Targeting Rules Configuration</h5>
							<button
								onClick={addRule}
								disabled={operationLoading}
								className={`text-sm flex items-center gap-1 disabled:opacity-50 ${schedulingStyles.buttonText} ${schedulingStyles.buttonHover}`}
							>
								<Plus className="w-4 h-4" />
								Add Rule
							</button>
						</div>

						{targetingRulesForm.length === 0 ? (
							<div className={`text-center py-8 ${schedulingStyles.buttonText}`}>
								<Target className="w-8 h-8 mx-auto mb-2 opacity-50" />
								<p className="text-sm">No targeting rules configured</p>
								<p className="text-xs mt-1">Click "Add Rule" to create your first targeting rule</p>
							</div>
						) : (
							<div className="space-y-4">
								{targetingRulesForm.map((rule, ruleIndex) => (
									<div key={ruleIndex} className={`${schedulingStyles.border} border rounded-lg p-3 bg-white`}>
										<div className="flex justify-between items-start mb-3">
											<span className={`text-sm font-medium ${schedulingStyles.text}`}>Rule #{ruleIndex + 1}</span>
											<button
												onClick={() => removeRule(ruleIndex)}
												disabled={operationLoading}
												className={`${theme.danger.text[500]} ${theme.danger.hover.text700} p-1`}
												title="Remove Rule"
											>
												<Trash2 className="w-4 h-4" />
											</button>
										</div>

										<div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-3">
											<div>
												<label className={`block text-xs font-medium ${schedulingStyles.text} mb-1`}>Attribute</label>
												<input
													type="text"
													value={rule?.attribute || ''}
													onChange={(e) => updateRule(ruleIndex, { attribute: e.target.value })}
													placeholder="userId, tenantId, country..."
													className={`w-full ${schedulingStyles.border} border rounded px-2 py-1 text-xs`}
													disabled={operationLoading}
												/>
											</div>

											<div>
												<label className={`block text-xs font-medium ${schedulingStyles.text} mb-1`}>Operator</label>
												<select
													value={rule?.operator ?? TargetingOperator.Equals}
													onChange={(e) => updateRule(ruleIndex, { operator: parseInt(e.target.value) as TargetingOperator })}
													className={`w-full ${schedulingStyles.border} border rounded px-2 py-1 text-xs`}
													disabled={operationLoading}
												>
													{targetingOperators.map(op => (
														<option key={op.value} value={op.value} title={op.description}>
															{op.label}
														</option>
													))}
												</select>
											</div>

											<div>
												<label className={`block text-xs font-medium ${schedulingStyles.text} mb-1`}>Variation</label>
												<input
													type="text"
													value={rule?.variation || ''}
													onChange={(e) => updateRule(ruleIndex, { variation: e.target.value })}
													placeholder="on, off, v1, v2..."
													className={`w-full ${schedulingStyles.border} border rounded px-2 py-1 text-xs`}
													disabled={operationLoading}
												/>
											</div>

											<div className="flex items-end">
												<button
													onClick={() => addValue(ruleIndex)}
													disabled={operationLoading}
													className={`w-full px-2 py-1 text-xs ${theme.warning[600]} text-white rounded ${theme.warning.hover.bg600} disabled:opacity-50 flex items-center justify-center gap-1`}
												>
													<Plus className="w-3 h-3" />
													Add Value
												</button>
											</div>
										</div>

										<div>
											<label className={`block text-xs font-medium ${schedulingStyles.text} mb-1`}>Values</label>
											<div className="grid grid-cols-1 md:grid-cols-2 gap-2">
												{Array.isArray(rule?.values) && rule.values.map((value, valueIndex) => (
													<div key={valueIndex} className="flex gap-1">
														<input
															type="text"
															value={value || ''}
															onChange={(e) => updateValue(ruleIndex, valueIndex, e.target.value)}
															placeholder="Enter value..."
															className={`flex-1 ${schedulingStyles.border} border rounded px-2 py-1 text-xs`}
															disabled={operationLoading}
														/>
														{rule.values.length > 1 && (
															<button
																onClick={() => removeValue(ruleIndex, valueIndex)}
																disabled={operationLoading}
																className={`${theme.danger.text[500]} ${theme.danger.hover.text700} p-1`}
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
							</div>
						)}
					</div>

					<div className="flex gap-2 mt-4">
						<button
							onClick={handleTargetingRulesSubmit}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.warning[600]} text-white rounded text-sm hover:bg-sky-700 disabled:opacity-50`}
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
							className={`px-3 py-1 ${theme.neutral[300]} ${theme.neutral.text[700]} rounded text-sm ${theme.neutral.hover.bg400} disabled:opacity-50`}
							data-testid="cancel-targeting-rules-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className={`text-sm ${theme.neutral.text[600]} space-y-1`}>
					{(() => {
						if (components.baseStatus === 'Enabled') {
							return <div className={`${theme.success.text[600]} font-medium`}>No custom targeting - flag enabled for all users</div>;
						}

						if (hasTargetingRules) {
							return (
								<div className="space-y-2">
									<div>Active Targeting Rules: {targetingRules.length}</div>
									<div className="space-y-1">
										{targetingRules.slice(0, 3).map((rule, index) => {
											// Safely format values (handle both strings and numbers)
											const formattedValues = Array.isArray(rule?.values) 
												? rule.values.map(v => typeof v === 'number' ? v.toString() : (v || '')).join(', ')
												: 'No values';
					
											return (
												<div key={index} className={`text-xs ${theme.neutral[100]} rounded px-2 py-1 font-mono`}>
													{rule?.attribute || 'Unknown'} {getTargetingOperatorLabel(rule?.operator).toLowerCase()} [{formattedValues}] → {rule?.variation || 'on'}
												</div>
											);
										})}
										{targetingRules.length > 3 && (
											<div className={`text-xs ${theme.neutral.text[500]} italic`}>
												...and {targetingRules.length - 3} more rule{targetingRules.length - 3 !== 1 ? 's' : ''}
											</div>
										)}
									</div>
								</div>
							);
						}

						if (!hasTargetingRules && components.baseStatus === 'Other') {
							return <div className={`${theme.neutral.text[500]} italic`}>No custom targeting rules configured</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className={`${theme.warning.text[600]} font-medium`}>Custom targeting disabled - flag is disabled</div>;
						}

						return <div className={`${theme.neutral.text[500]} italic`}>Targeting rules configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};