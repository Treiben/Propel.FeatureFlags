// Centralized color theme configuration
export const theme = {
	// Primary colors
	primary: {
		50: 'bg-blue-50',
		100: 'bg-blue-100',
		600: 'bg-blue-600',
		700: 'bg-blue-700',
		800: 'bg-blue-800',
		text: {
			600: 'text-blue-600',
			700: 'text-blue-700',
			800: 'text-blue-800',
			900: 'text-blue-900',
		},
		border: {
			200: 'border-blue-200',
			300: 'border-blue-300',
		},
		hover: {
			bg700: 'hover:bg-blue-700',
			bg800: 'hover:bg-blue-800',
			text700: 'hover:text-blue-700',
			text800: 'hover:text-blue-800',
		}
	},

	// Success (green) - for enabled states
	success: {
		50: 'bg-green-50',
		100: 'bg-green-100',
		600: 'bg-green-600',
		700: 'bg-green-700',
		text: {
			600: 'text-green-600',
			700: 'text-green-700',
			800: 'text-green-800',
			900: 'text-green-900',
		},
		border: {
			200: 'border-green-200',
			300: 'border-green-300',
		},
		hover: {
			bg700: 'hover:bg-green-700',
			text700: 'hover:text-green-700',
			text800: 'hover:text-green-800',
		}
	},

	// Warning (amber) - for scheduled, warnings
	warning: {
		50: 'bg-amber-50',
		100: 'bg-amber-100',
		500: 'bg-amber-500',
		600: 'bg-amber-600',
		text: {
			600: 'text-amber-600',
			700: 'text-amber-700',
			800: 'text-amber-800',
			900: 'text-amber-900',
		},
		border: {
			200: 'border-amber-200',
			300: 'border-amber-300',
		},
		hover: {
			bg600: 'hover:bg-amber-600',
			text700: 'hover:text-amber-700',
			text800: 'hover:text-amber-800',
		}
	},

	// Danger (red) - for disabled, errors, deletions
	danger: {
		50: 'bg-red-50',
		100: 'bg-red-100',
		500: 'bg-red-500',
		600: 'bg-red-600',
		text: {
			500: 'text-red-500',
			600: 'text-red-600',
			700: 'text-red-700',
			800: 'text-red-800',
		},
		border: {
			200: 'border-red-200',
			300: 'border-red-300',
		},
		hover: {
			text700: 'hover:text-red-700',
			text800: 'hover:text-red-800',
		}
	},

	// Info (sky) - for information, metadata
	info: {
		50: 'bg-sky-50',
		100: 'bg-sky-100',
		500: 'bg-sky-500',
		600: 'bg-sky-600',
		text: {
			600: 'text-sky-600',
			700: 'text-sky-700',
			800: 'text-sky-800',
		},
		border: {
			200: 'border-sky-200',
			300: 'border-sky-300',
		}
	},

	// Neutral (gray/slate)
	neutral: {
		50: 'bg-gray-50',
		100: 'bg-gray-100',
		200: 'bg-gray-200',
		300: 'bg-gray-300',
		400: 'bg-gray-400',
		500: 'bg-gray-500',
		600: 'bg-gray-600',
		700: 'bg-gray-700',
		800: 'bg-gray-800',
		900: 'bg-gray-900',
		text: {
			400: 'text-gray-400',
			500: 'text-gray-500',
			600: 'text-gray-600',
			700: 'text-gray-700',
			800: 'text-gray-800',
			900: 'text-gray-900',
		},
		border: {
			200: 'border-gray-200',
			300: 'border-gray-300',
		},
		hover: {
			bg50: 'hover:bg-gray-50',
			bg100: 'hover:bg-gray-100',
			bg400: 'hover:bg-gray-400',
			text600: 'hover:text-gray-600',
			text700: 'hover:text-gray-700',
		}
	}
} as const;

// Component-specific style mappings
export const componentStyles = {
	// Status badges
	statusBadge: {
		enabled: `${theme.success[100]} ${theme.success.text[800]}`,
		disabled: `${theme.danger[100]} ${theme.danger.text[800]}`,
		scheduled: `${theme.warning[100]} ${theme.warning.text[800]}`,
		partial: `${theme.info[100]} ${theme.info.text[800]}`,
	},

	// Buttons
	button: {
		primary: `${theme.primary[600]} text-white rounded ${theme.primary.hover.bg700}`,
		secondary: `${theme.neutral.border[300]} ${theme.neutral.text[700]} rounded ${theme.neutral.hover.bg50}`,
		danger: `${theme.danger[600]} text-white rounded ${theme.danger.hover.text700}`,
		text: `${theme.primary.text[600]} ${theme.primary.hover.text800}`,
	},

	// Feature sections
	section: {
		scheduling: {
			bg: theme.warning[50],
			border: theme.warning.border[200],
			text: theme.warning.text[800],
			textPrimary: theme.warning.text[900],
			buttonText: theme.warning.text[600],
			buttonHover: theme.warning.hover.text800,
		},
		targeting: {
			bg: theme.warning[50],
			border: theme.warning.border[200],
			text: theme.warning.text[800],
			textPrimary: theme.warning.text[900],
			buttonText: theme.warning.text[600],
		},
		variations: {
			bg: theme.warning[50],
			border: theme.warning.border[200],
			text: theme.warning.text[800],
			textPrimary: theme.warning.text[900],
			buttonText: theme.warning.text[600],
			buttonHover: theme.warning.hover.text800,
		},
		userAccess: {
			bg: theme.warning[50],
			border: theme.warning.border[200],
			text: theme.warning.text[800],
			textPrimary: theme.warning.text[900],
			buttonText: theme.warning.text[600],
		}
	}
};

// Utility functions for dynamic class generation
export const getStatusColor = (status: 'enabled' | 'disabled' | 'scheduled' | 'partial'): string => {
	return componentStyles.statusBadge[status];
};

export const getSectionClasses = (section: keyof typeof componentStyles.section) => {
	return componentStyles.section[section];
};