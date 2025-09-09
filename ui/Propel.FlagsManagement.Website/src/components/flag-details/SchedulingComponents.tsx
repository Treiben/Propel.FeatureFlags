import { useState, useEffect } from 'react';
import { Calendar, Clock, PlayCircle, X } from 'lucide-react';
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
	onSchedule: (flag: FeatureFlagDto, enableDate: string, disableDate?: string) => Promise<void>;
	onClearSchedule: () => Promise<void>;
	operationLoading: boolean;
}

export const SchedulingSection: React.FC<SchedulingSectionProps> = ({
	flag,
	onSchedule,
	onClearSchedule,
	operationLoading
}) => {
	const [editingSchedule, setEditingSchedule] = useState(false);
	const [scheduleData, setScheduleData] = useState({
		enableDate: flag.scheduledEnableDate ? flag.scheduledEnableDate.slice(0, 16) : '',
		disableDate: flag.scheduledDisableDate ? flag.scheduledDisableDate.slice(0, 16) : ''
	});

	const components = parseStatusComponents(flag);

	// Update local state when flag changes (when a different flag is selected)
	useEffect(() => {
		setScheduleData({
			enableDate: flag.scheduledEnableDate ? flag.scheduledEnableDate.slice(0, 16) : '',
			disableDate: flag.scheduledDisableDate ? flag.scheduledDisableDate.slice(0, 16) : ''
		});
	}, [flag.key, flag.scheduledEnableDate, flag.scheduledDisableDate]);

	const handleScheduleSubmit = async () => {
		try {
			await onSchedule(
				flag,
				scheduleData.enableDate ? new Date(scheduleData.enableDate).toISOString() : '',
				scheduleData.disableDate ? new Date(scheduleData.disableDate).toISOString() : undefined
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
				<h4 className="font-medium text-gray-900">Scheduling</h4>
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
								value={scheduleData.enableDate}
								onChange={(e) => setScheduleData({ ...scheduleData, enableDate: e.target.value })}
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
								value={scheduleData.disableDate}
								onChange={(e) => setScheduleData({ ...scheduleData, disableDate: e.target.value })}
								className="w-full border border-blue-300 rounded px-3 py-2 text-sm"
								disabled={operationLoading}
								min={scheduleData.enableDate || new Date().toISOString().slice(0, 16)}
								data-testid="disable-date-input"
							/>
						</div>
					</div>
					<div className="flex gap-2 mt-3">
						<button
							onClick={handleScheduleSubmit}
							disabled={operationLoading || !scheduleData.enableDate}
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
						// Check flag mode and show appropriate text
						if (components.baseStatus === 'Enabled') {
							return <div className="text-green-600 font-medium">Always active - no schedule restrictions</div>;
						};

						// Show scheduled dates only when schedule is set and has valid dates
						if (flag.scheduledEnableDate || flag.scheduledDisableDate) {
							return (
								<>
									<div>Enable: {formatDate(flag.scheduledEnableDate)}</div>
									<div>Disable: {formatDate(flag.scheduledDisableDate)}</div>
								</>
							);
						}
						// Check if schedule is set
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