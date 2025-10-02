import { useState, useEffect } from 'react';
import { Calendar, Clock, PlayCircle, X, Info } from 'lucide-react';
import type { FeatureFlagDto } from '../../services/apiService';
import {
	getScheduleStatus,
	formatDate,
	formatRelativeTime,
	parseStatusComponents
} from '../../utils/flagHelpers';

interface SchedulingStatusIndicatorProps {
	flag: FeatureFlagDto;
}

export const SchedulingStatusIndicator: React.FC<SchedulingStatusIndicatorProps> = ({ flag }) => {
	const components = parseStatusComponents(flag);
	const scheduleStatus = getScheduleStatus(flag);

	if (!components.isScheduled) return null;

	return (
		<div className={`mb-4 p-3 rounded-lg border ${scheduleStatus.isActive
			? 'bg-green-50 border-green-200'
			: scheduleStatus.phase === 'upcoming'
				? 'bg-blue-50 border-blue-200'
				: 'bg-gray-50 border-gray-200'
			}`} data-testid="scheduling-status-indicator">
			<div className="flex items-center gap-2 mb-1">
				{scheduleStatus.isActive ? (
					<>
						<PlayCircle className="w-4 h-4 text-green-600" />
						<span className="font-medium text-green-800">Schedule Currently Active</span>
					</>
				) : scheduleStatus.phase === 'upcoming' ? (
					<>
						<Clock className="w-4 h-4 text-blue-600" />
						<span className="font-medium text-blue-800">Schedule Upcoming</span>
					</>
				) : scheduleStatus.phase === 'expired' ? (
					<>
						<Clock className="w-4 h-4 text-gray-600" />
						<span className="font-medium text-gray-800">Schedule Expired</span>
					</>
				) : null}
			</div>

			{scheduleStatus.nextAction && scheduleStatus.nextActionTime && (
				<p className={`text-sm ${scheduleStatus.isActive ? 'text-green-700' :
					scheduleStatus.phase === 'upcoming' ? 'text-blue-700' : 'text-gray-700'
					}`} data-testid="next-action-info">
					{scheduleStatus.nextAction} {formatRelativeTime(scheduleStatus.nextActionTime)}
				</p>
			)}

			{scheduleStatus.isActive && !scheduleStatus.nextAction && (
				<p className="text-sm text-green-700">
					Flag is currently enabled via schedule (no end date)
				</p>
			)}

			{scheduleStatus.phase === 'expired' && (
				<p className="text-sm text-gray-700">
					Scheduled period has ended
				</p>
			)}
		</div>
	);
};

interface SchedulingSectionProps {
	flag: FeatureFlagDto;
	onSchedule: (flag: FeatureFlagDto, enableOn: string, disableOn?: string) => Promise<void>;
	onClearSchedule: () => Promise<void>;
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
				<div className="absolute z-50 bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 text-sm leading-relaxed text-gray-800 bg-white border border-gray-300 rounded-lg shadow-lg min-w-[280px] max-w-[320px]">
					{content}
					<div className="absolute top-full left-1/2 transform -translate-x-1/2 border-4 border-transparent border-t-white"></div>
				</div>
			)}
		</div>
	);
};

export const SchedulingSection: React.FC<SchedulingSectionProps> = ({
	flag,
	onSchedule,
	onClearSchedule,
	operationLoading
}) => {
	const [editingSchedule, setEditingSchedule] = useState(false);
	const [scheduleData, setScheduleData] = useState({
		enableOn: flag.schedule?.enableOnUtc ? flag.schedule.enableOnUtc.slice(0, 16) : '',
		disableOn: flag.schedule?.disableOnUtc ? flag.schedule.disableOnUtc.slice(0, 16) : ''
	});

	const components = parseStatusComponents(flag);

	useEffect(() => {
		setScheduleData({
			enableOn: flag.schedule?.enableOnUtc ? flag.schedule.enableOnUtc.slice(0, 16) : '',
			disableOn: flag.schedule?.disableOnUtc ? flag.schedule.disableOnUtc.slice(0, 16) : ''
		});
	}, [flag.key, flag.schedule?.enableOnUtc, flag.schedule?.disableOnUtc]);

	const handleScheduleSubmit = async () => {
		try {
			await onSchedule(
				flag,
				scheduleData.enableOn ? new Date(scheduleData.enableOn).toISOString() : '',
				scheduleData.disableOn ? new Date(scheduleData.disableOn).toISOString() : undefined
			);
			setEditingSchedule(false);
		} catch (error) {
			console.error('Failed to schedule flag:', error);
		}
	};

	const handleClearSchedule = async () => {
		try {
			await onClearSchedule();
		} catch (error) {
			console.error('Failed to clear schedule:', error);
		}
	};

	return (
		<div className="space-y-4 mb-6">
			<div className="flex justify-between items-center">
				<div className="flex items-center gap-2">
					<h4 className="font-medium text-gray-900">Scheduling</h4>
					<InfoTooltip content="Automatically enable/disable flags at specific dates and times. Perfect for coordinated releases, marketing campaigns, and planned rollouts." />
				</div>
				<div className="flex gap-2">
					<button
						onClick={() => setEditingSchedule(true)}
						disabled={operationLoading}
						className="text-blue-600 hover:text-blue-800 text-sm flex items-center gap-1 disabled:opacity-50"
						data-testid="edit-schedule-button"
					>
						<Calendar className="w-4 h-4" />
						Schedule
					</button>
					{components.isScheduled && (
						<button
							onClick={handleClearSchedule}
							disabled={operationLoading}
							className="text-red-600 hover:text-red-800 text-sm flex items-center gap-1 disabled:opacity-50"
							title="Clear Schedule"
							data-testid="clear-schedule-button"
						>
							<X className="w-4 h-4" />
							Clear
						</button>
					)}
				</div>
			</div>

			{editingSchedule ? (
				<div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
					<div className="space-y-3">
						<div>
							<label className="block text-sm font-medium text-blue-800 mb-1">Enable Date</label>
							<input
								type="datetime-local"
								value={scheduleData.enableOn}
								onChange={(e) => setScheduleData({ ...scheduleData, enableOn: e.target.value })}
								className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
								disabled={operationLoading}
								min={new Date().toISOString().slice(0, 16)}
								data-testid="enable-date-input"
							/>
						</div>
						<div>
							<label className="block text-sm font-medium text-blue-800 mb-1">Disable Date (Optional)</label>
							<input
								type="datetime-local"
								value={scheduleData.disableOn}
								onChange={(e) => setScheduleData({ ...scheduleData, disableOn: e.target.value })}
								className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
								min={scheduleData.enableOn || new Date().toISOString().slice(0, 16)}
								data-testid="disable-date-input"
							/>
						</div>
					</div>
					<div className="flex gap-2 mt-3">
						<button
							onClick={handleScheduleSubmit}
							disabled={operationLoading || !scheduleData.enableOn}
							className="px-3 py-1 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
							data-testid="submit-schedule-button"
						>
							{operationLoading ? 'Scheduling...' : 'Schedule'}
						</button>
						<button
							onClick={() => setEditingSchedule(false)}
							disabled={operationLoading}
							className="px-3 py-1 bg-gray-300 text-gray-700 rounded text-sm hover:bg-gray-400 disabled:opacity-50"
							data-testid="cancel-schedule-button"
						>
							Cancel
						</button>
					</div>
				</div>
			) : (
				<div className="text-sm text-gray-600 space-y-1">
					{(() => {
						if (components.baseStatus === 'Enabled') {
							return <div className="text-green-600 font-medium">Always active - no schedule restrictions</div>;
						}

						if (flag.schedule?.enableOnUtc || flag.schedule?.disableOnUtc) {
							return (
								<>
									<div>Enable: {formatDate(flag.schedule?.enableOnUtc)}</div>
									<div>Disable: {formatDate(flag.schedule?.disableOnUtc)}</div>
								</>
							);
						}

						if (!components.isScheduled && components.baseStatus === 'Other') {
							return <div className="text-gray-500 italic">No scheduled activation</div>;
						} else if (components.baseStatus === 'Disabled') {
							return <div className="text-orange-600 font-medium">Scheduling unavailable - flag is disabled</div>;
						}

						return <div className="text-gray-500 italic">Schedule configuration incomplete</div>;
					})()}
				</div>
			)}
		</div>
	);
};