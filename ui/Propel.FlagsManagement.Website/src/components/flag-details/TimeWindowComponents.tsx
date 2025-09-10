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

interface TimeWindowStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const TimeWindowStatusIndicator: React.FC<TimeWindowStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);
	const timeWindowStatus = getTimeWindowStatus(flag);

	if (!components.hasTimeWindow) return null;

	return (
		<div className={`mb-4 p-3 rounded-lg border ${timeWindowStatus.isActive
			? 'bg-indigo-50 border-indigo-200'
			: 'bg-gray-50 border-gray-200'
			}`}>
			<div className="flex items-center gap-2 mb-1">
				<Timer className={`w-4 h-4 ${timeWindowStatus.isActive ? 'text-indigo-600' : 'text-gray-600'}`} />
				<span className={`font-medium ${timeWindowStatus.isActive ? 'text-indigo-800' : 'text-gray-800'}`}>
					{timeWindowStatus.isActive ? 'Time Window Active' : 'Outside Time Window'}
				</span>
			</div>
			<div className={`text-sm ${timeWindowStatus.isActive ? 'text-indigo-700' : 'text-gray-700'} space-y-1`}>
				<div>Active Time: {formatTime(flag.windowStartTime)} - {formatTime(flag.windowEndTime)}</div>
				<div>Time Zone: {flag.timeZone || 'UTC'}</div>
				{flag.windowDays && flag.windowDays.length > 0 && (
					<div>Active Days: {flag.windowDays.map(day => getDayName(day)).join(', ')}</div>
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
		windowStartTime: string;
		windowEndTime: string;
		timeZone: string;
		windowDays: string[];
	}) => Promise<void>;
	onClearTimeWindow: () => Promise<void>;
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

export const TimeWindowSection: React.FC<TimeWindowSectionProps> = ({
	flag,
	onUpdateTimeWindow,
	onClearTimeWindow,
	operationLoading
}) => {
	const [editingTimeWindow, setEditingTimeWindow] = useState(false);
	const [timeWindowData, setTimeWindowData] = useState({
		windowStartTime: flag.windowStartTime || '09:00:00',
		windowEndTime: flag.windowEndTime || '17:00:00',
		timeZone: flag.timeZone || 'UTC',
		windowDays: flag.windowDays ? flag.windowDays.map(day => getDayName(day)) : []
	});

	const components = parseStatusComponents(flag);

	// Update local state when flag changes (when a different flag is selected)
	useEffect(() => {
		setTimeWindowData({
			windowStartTime: flag.windowStartTime || '09:00:00',
			windowEndTime: flag.windowEndTime || '17:00:00',
			timeZone: flag.timeZone || 'UTC',
			windowDays: flag.windowDays ? flag.windowDays.map(day => getDayName(day)) : []
		});
	}, [flag.key, flag.windowStartTime, flag.windowEndTime, flag.timeZone, flag.windowDays]);

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
			windowDays: prev.windowDays.includes(dayLabel)
				? prev.windowDays.filter(d => d !== dayLabel)
				: [...prev.windowDays, dayLabel]
		}));
	};

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">Time Window</h4>
					<InfoTooltip content="Restrict flag activation to specific hours and days. Ideal for business hours, maintenance windows, and region-specific operations." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingTimeWindow(true)}
						disabled={operationLoading}
						className="text-indigo-600 hover:text-indigo-800 text-sm flex items-center gap-1 disabled:opacity-50"
					>
						<Clock className="w-4 h-4" />
						Configure
					</button>
					{components.hasTimeWindow && (
						<button
							onClick={handleClearTimeWindow}
							disabled={operationLoading}
							className="text-red-600 hover:text-red-800 text-sm flex items-center gap-1 disabled:opacity-50"
							title="Clear Time Window"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingTimeWindow ? (
				<div className="bg-indigo-50 border border-indigo-200 rounded-lg p-4">
					<div className="space-y-4">
						<div className="grid grid-cols-3 gap-4">
							<div>
								<label className="block text-sm font-medium text-indigo-800 mb-1">Start Time</label>
								<input
									type="time"
									step="1"
									value={timeWindowData.windowStartTime}
									onChange={(e) => setTimeWindowData({ ...timeWindowData, windowStartTime: e.target.value })}
									className="w-full border border-indigo-300 rounded px-3 py-2 text-sm"
									disabled={operationLoading}
								/>
							</div>
							<div>
								<label className="block text-sm font-medium text-indigo-800 mb-1">End Time</label>
								<input
									type="time"
									step="1"
									value={timeWindowData.windowEndTime}
									onChange={(e) => setTimeWindowData({ ...timeWindowData, windowEndTime: e.target.value })}
									className="w-full border border-indigo-300 rounded px-3 py-2 text-sm"
									disabled={operationLoading}
								/>
							</div>
							<div>
								<label className="block text-sm font-medium text-indigo-800 mb-1">Time Zone</label>
								<select
									value={timeWindowData.timeZone}
									onChange={(e) => setTimeWindowData({ ...timeWindowData, timeZone: e.target.value })}
									className="w-full border border-indigo-300 rounded px-3 py-2 text-sm"
									disabled={operationLoading}
								>
									{getTimeZones().map(tz => (
										<option key={tz} value={tz}>{tz}</option>
									))}
								</select>
							</div>
						</div>

						<div>
							<label className="block text-sm font-medium text-indigo-800 mb-2">Active Days</label>
							<div className="grid grid-cols-7 gap-2">
								{getDaysOfWeek().map(day => (
									<label key={day.value} className="flex items-center justify-center">
										<input
											type="checkbox"
											checked={timeWindowData.windowDays.includes(day.label)}
											onChange={() => toggleWindowDay(day.label)}
											className="sr-only"
											disabled={operationLoading}
										/>
										<div className={`px-2 py-1 text-xs rounded cursor-pointer transition-colors ${timeWindowData.windowDays.includes(day.label)
											? 'bg-indigo-600 text-white'
											: 'bg-indigo-100 text-indigo-800 hover:bg-indigo-200'
											}`}>
											{day.label.slice(0, 3)}
										</div>
									</label>
								))}
							</div>
							<p className="text-xs text-indigo-600 mt-1">Leave empty to allow all days</p>
						</div>
					</div>
					<div className="flex gap-2 mt-4">
						<button
							onClick={handleTimeWindowSubmit}
							disabled={operationLoading}
							className="px-3 py-1 bg-indigo-600 text-white rounded text-sm hover:bg-indigo-700 disabled:opacity-50"
						>
							{operationLoading ? 'Saving...' : 'Save Time Window'}
						</button>
						<button
							onClick={() => {
								setEditingTimeWindow(false);
								setTimeWindowData({
									windowStartTime: flag.windowStartTime || '09:00:00',
									windowEndTime: flag.windowEndTime || '17:00:00',
									timeZone: flag.timeZone || 'UTC',
									windowDays: flag.windowDays ? flag.windowDays.map(day => getDayName(day)) : []
								});
							}}
							disabled={operationLoading}
							className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
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
							return <div className="text-green-600 font-medium">Always active - no time restrictions</div>;
						};

						// Show time window details if set
						if (flag.windowStartTime || flag.windowEndTime) {
							return (
								<>
									<div>Active Time: {formatTime(flag.windowStartTime)} - {formatTime(flag.windowEndTime)}</div>
									<div>Time Zone: {flag.timeZone || 'UTC'}</div>
									<div>Active Days: {flag.windowDays && flag.windowDays.length > 0 ? flag.windowDays.map(day => getDayName(day)).join(', ') : 'All days'}</div>
								</>
							);
						}
						// Check if time window is set
						if (!components.hasTimeWindow && components.baseStatus === 'Other') {
							return <div className="text-gray-500 italic">No time restrictions</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className="text-orange-600 font-medium">Inactive during all hours - flag is disabled</div>;
						}

						return <div className="text-gray-500 italic">Operational time window configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};