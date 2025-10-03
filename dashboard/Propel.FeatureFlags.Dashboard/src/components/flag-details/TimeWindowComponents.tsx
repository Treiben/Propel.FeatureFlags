import { useState, useEffect } from 'react';
import { Clock, Timer, X, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import { getTimeZones, getDaysOfWeek } from '../../services/apiService';
import {
	getTimeWindowStatus,
	formatTime,
	parseStatusComponents,
	getDayName
} from '../../utils/flagHelpers';
import { getSectionClasses, theme } from '../../styles/theme';

interface TimeWindowStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const TimeWindowStatusIndicator: React.FC<TimeWindowStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);
	const timeWindowStatus = getTimeWindowStatus(flag);
	const schedulingStyles = getSectionClasses('scheduling');

	if (!components.hasTimeWindow) return null;

	return (
		<div className={`mb-4 p-3 rounded-lg border ${timeWindowStatus.isActive
			? `${theme.success[50]} ${theme.success.border[200]}`
			: `${theme.neutral[50]} ${theme.neutral.border[200]}`
			}`}>
			<div className="flex items-center gap-2 mb-1">
				<Timer className={`w-4 h-4 ${timeWindowStatus.isActive ? theme.success.text[600] : theme.neutral.text[600]}`} />
				<span className={`font-medium ${timeWindowStatus.isActive ? theme.success.text[800] : theme.neutral.text[800]}`}>
					{timeWindowStatus.isActive ? 'Time Window Active' : 'Outside Time Window'}
				</span>
			</div>
			<div className={`text-sm ${timeWindowStatus.isActive ? theme.success.text[700] : theme.neutral.text[700]} space-y-1`}>
				<div>Active Time: {formatTime(flag.timeWindow?.startOn)} - {formatTime(flag.timeWindow?.stopOn)}</div>
				<div>Time Zone: {flag.timeWindow?.timeZone || 'UTC'}</div>
				{flag.timeWindow?.daysActive && flag.timeWindow.daysActive.length > 0 && (
					<div>Active Days: {flag.timeWindow.daysActive.map(day => getDayName(day)).join(', ')}</div>
				)}
				{timeWindowStatus.reason && (
					<div className="italic">{timeWindowStatus.reason}</div>
				)}
			</div>
		</div>
	);
};

interface TimeWindowSectionProps {
	flag: FeatureFlagDto;
	onUpdateTimeWindow: (flag: FeatureFlagDto, timeWindowData: {
		startOn: string;
		endOn: string;
		timeZone: string;
		daysActive: string[];
	}) => Promise<void>;
	onClearTimeWindow: () => Promise<void>;
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
				className={`${theme.neutral.text[400]} ${theme.neutral.hover.text600} transition-colors`}
				type="button"
			>
				<Info className="w-4 h-4" />
			</button>

			{showTooltip && (
				<div className={`absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-sm leading-relaxed ${theme.neutral.text[800]} bg-white ${theme.neutral.border[300]} border rounded-lg shadow-lg min-w-[280px] max-w-[320px]`}>
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-white"></div>
				</div>
			)}
		</div>
	);
};

export const TimeWindowSection: React.FC<TimeWindowSectionProps> = ({
	flag,
	onUpdateTimeWindow,
	onClearTimeWindow,
	operationLoading
}) => {
	const [editingTimeWindow, setEditingTimeWindow] = useState(false);
	const [timeWindowData, setTimeWindowData] = useState({
		startOn: flag.timeWindow?.startOn || '09:00:00',
		endOn: flag.timeWindow?.stopOn || '17:00:00',
		timeZone: flag.timeWindow?.timeZone || 'UTC',
		daysActive: flag.timeWindow?.daysActive ? flag.timeWindow.daysActive.map(day => getDayName(day)) : []
	});

	const components = parseStatusComponents(flag);
	const schedulingStyles = getSectionClasses('scheduling');

	useEffect(() => {
		setTimeWindowData({
			startOn: flag.timeWindow?.startOn || '09:00:00',
			endOn: flag.timeWindow?.stopOn || '17:00:00',
			timeZone: flag.timeWindow?.timeZone || 'UTC',
			daysActive: flag.timeWindow?.daysActive ? flag.timeWindow.daysActive.map(day => getDayName(day)) : []
		});
	}, [flag.key, flag.timeWindow?.startOn, flag.timeWindow?.stopOn, flag.timeWindow?.timeZone, flag.timeWindow?.daysActive]);

	const handleTimeWindowSubmit = async () => {
		try {
			await onUpdateTimeWindow(flag, timeWindowData);
			setEditingTimeWindow(false);
		} catch (error) {
			console.error('Failed to update time window:', error);
		}
	};

	const handleClearTimeWindow = async () => {
		try {
			await onClearTimeWindow();
		} catch (error) {
			console.error('Failed to clear time window:', error);
		}
	};

	const toggleWindowDay = (dayLabel: string) => {
		setTimeWindowData(prev => ({
			...prev,
			daysActive: prev.daysActive.includes(dayLabel)
				? prev.daysActive.filter(d => d !== dayLabel)
				: [...prev.daysActive, dayLabel]
		}));
	};

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className={`font-medium ${theme.neutral.text[900]}`}>Time Window</h4>
					<InfoTooltip content="Restrict flag activation to specific hours and days. Ideal for business hours, maintenance windows, and region-specific operations." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingTimeWindow(true)}
						disabled={operationLoading}
						className={`text-sm flex items-center gap-1 disabled:opacity-50 ${schedulingStyles.buttonText} ${schedulingStyles.buttonHover}`}
					>
						<Clock className="w-4 h-4" />
						Configure
					</button>
					{components.hasTimeWindow && (
						<button
							onClick={handleClearTimeWindow}
							disabled={operationLoading}
							className={`${theme.danger.text[600]} ${theme.danger.hover.text800} text-sm flex items-center gap-1 disabled:opacity-50`}
							title="Clear Time Window"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingTimeWindow ? (
				<div className={`${schedulingStyles.bg} ${schedulingStyles.border} border rounded-lg p-4`}>
					<div className="space-y-4">
						<div className="grid grid-cols-3 gap-4">
							<div>
								<label className={`block text-sm font-medium ${schedulingStyles.text} mb-1`}>Start Time</label>
								<input
									type="time"
									step="1"
									value={timeWindowData.startOn}
									onChange={(e) => setTimeWindowData({ ...timeWindowData, startOn: e.target.value })}
									className={`w-full ${schedulingStyles.border} border rounded px-3 py-2 text-sm`}
									disabled={operationLoading}
								/>
							</div>
							<div>
								<label className={`block text-sm font-medium ${schedulingStyles.text} mb-1`}>End Time</label>
								<input
									type="time"
									step="1"
									value={timeWindowData.endOn}
									onChange={(e) => setTimeWindowData({ ...timeWindowData, endOn: e.target.value })}
									className={`w-full ${schedulingStyles.border} border rounded px-3 py-2 text-sm`}
									disabled={operationLoading}
								/>
							</div>
							<div>
								<label className={`block text-sm font-medium ${schedulingStyles.text} mb-1`}>Time Zone</label>
								<select
									value={timeWindowData.timeZone}
									onChange={(e) => setTimeWindowData({ ...timeWindowData, timeZone: e.target.value })}
									className={`w-full ${schedulingStyles.border} border rounded px-3 py-2 text-sm`}
									disabled={operationLoading}
								>
									{getTimeZones().map(tz => (
										<option key={tz} value={tz}>{tz}</option>
									))}
								</select>
							</div>
						</div>

						<div>
							<label className={`block text-sm font-medium ${schedulingStyles.text} mb-2`}>Active Days</label>
							<div className="grid grid-cols-7 gap-2">
								{getDaysOfWeek().map(day => (
									<label key={day.value} className="flex items-center justify-center">
										<input
											type="checkbox"
											checked={timeWindowData.daysActive.includes(day.label)}
											onChange={() => toggleWindowDay(day.label)}
											className="sr-only"
											disabled={operationLoading}
										/>
										<div className={`px-2 py-1 text-xs rounded cursor-pointer transition-colors ${timeWindowData.daysActive.includes(day.label)
											? `${theme.warning[600]} text-white`
											: `${theme.warning[100]} ${theme.warning.text[800]} ${theme.warning.hover.bg600} hover:text-white`
											}`}>
											{day.label.slice(0, 3)}
										</div>
									</label>
								))}
							</div>
							<p className={`text-xs ${schedulingStyles.buttonText} mt-1`}>Leave empty to allow all days</p>
						</div>
					</div>
					<div className="flex gap-2 mt-4">
						<button
							onClick={handleTimeWindowSubmit}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.warning[600]} text-white rounded text-sm hover:bg-sky-700 disabled:opacity-50`}
						>
							{operationLoading ? 'Saving...' : 'Save Time Window'}
						</button>
						<button
							onClick={() => {
								setEditingTimeWindow(false);
								setTimeWindowData({
									startOn: flag.timeWindow?.startOn || '09:00:00',
									endOn: flag.timeWindow?.stopOn || '17:00:00',
									timeZone: flag.timeWindow?.timeZone || 'UTC',
									daysActive: flag.timeWindow?.daysActive ? flag.timeWindow.daysActive.map(day => getDayName(day)) : []
								});
							}}
							disabled={operationLoading}
							className={`px-3 py-1 ${theme.neutral[300]} ${theme.neutral.text[700]} rounded text-sm ${theme.neutral.hover.bg400} disabled:opacity-50`}
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className={`text-sm ${theme.neutral.text[600]} space-y-1`}>
					{(() => {
						if (components.baseStatus === 'Enabled') {
							return <div className={`${theme.success.text[600]} font-medium`}>Always active - no time restrictions</div>;
						}

						if (flag.timeWindow?.startOn || flag.timeWindow?.stopOn) {
							return (
								<>
									<div>Active Time: {formatTime(flag.timeWindow?.startOn)} - {formatTime(flag.timeWindow?.stopOn)}</div>
									<div>Time Zone: {flag.timeWindow?.timeZone || 'UTC'}</div>
									<div>Active Days: {flag.timeWindow?.daysActive && flag.timeWindow.daysActive.length > 0 ? flag.timeWindow.daysActive.map(day => getDayName(day)).join(', ') : 'All days'}</div>
								</>
							);
						}

						if (!components.hasTimeWindow && components.baseStatus === 'Other') {
							return <div className={`${theme.neutral.text[500]} italic`}>No time restrictions</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className={`${theme.warning.text[600]} font-medium`}>Inactive during all hours - flag is disabled</div>;
						}

						return <div className={`${theme.neutral.text[500]} italic`}>Operational time window configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};