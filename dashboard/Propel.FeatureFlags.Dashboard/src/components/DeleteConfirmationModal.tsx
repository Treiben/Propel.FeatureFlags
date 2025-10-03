import { AlertCircle, X } from 'lucide-react';
import { useState, useEffect } from 'react';

interface DeleteConfirmationModalProps {
    isOpen: boolean;
    flagKey: string;
    flagName: string;
    isDeleting: boolean;
    onConfirm: () => Promise<void>;
    onCancel: () => void;
}

export const DeleteConfirmationModal: React.FC<DeleteConfirmationModalProps> = ({
    isOpen,
    flagKey,
    flagName,
    isDeleting,
    onConfirm,
    onCancel
}) => {
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!isOpen) {
            setError(null);
        }
    }, [isOpen]);

    const handleConfirm = async () => {
        try {
            setError(null);
            await onConfirm();
        } catch (err: any) {
            console.error('Failed to delete flag:', err);
            
            let errorMessage = 'Failed to delete flag. Please try again.';
            
            if (err?.detail) {
                errorMessage = err.detail;
            } else if (err?.message) {
                errorMessage = err.message;
            }
            
            setError(errorMessage);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
                <div className="flex items-start gap-4">
                    <div className="flex-shrink-0">
                        <div className="w-12 h-12 rounded-full bg-red-100 flex items-center justify-center">
                            <AlertCircle className="w-6 h-6 text-red-600" />
                        </div>
                    </div>
                    <div className="flex-1">
                        <h3 className="text-lg font-semibold text-gray-900 mb-2">Delete Feature Flag</h3>
                        <p className="text-sm text-gray-600 mb-4">
                            Are you sure you want to delete <strong>{flagName}</strong> ({flagKey})? This action cannot be undone.
                        </p>

                        {error && (
                            <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg flex items-start gap-2">
                                <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
                                <div className="flex-1">
                                    <p className="text-sm text-red-800">{error}</p>
                                </div>
                                <button
                                    onClick={() => setError(null)}
                                    className="text-red-400 hover:text-red-600"
                                >
                                    <X className="w-4 h-4" />
                                </button>
                            </div>
                        )}

                        <div className="flex gap-3">
                            <button
                                onClick={onCancel}
                                className="flex-1 px-4 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50"
                                disabled={isDeleting}
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleConfirm}
                                disabled={isDeleting}
                                className="flex-1 px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {isDeleting ? 'Deleting...' : 'Delete'}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};