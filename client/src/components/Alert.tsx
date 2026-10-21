import React from 'react';
import { AlertCircle, CheckCircle, Info, XCircle } from 'lucide-react';

interface AlertProps {
  type?: 'error' | 'success' | 'warning' | 'info';
  title?: string;
  message: string;
  onClose?: () => void;
}

export const Alert: React.FC<AlertProps> = ({ type = 'info', title, message, onClose }) => {
  if (!message) return null;

  const styles = {
    error: 'bg-red-950/70 border-red-500/50 text-red-200',
    success: 'bg-emerald-950/70 border-emerald-500/50 text-emerald-200',
    warning: 'bg-amber-950/70 border-amber-500/50 text-amber-200',
    info: 'bg-blue-950/70 border-blue-500/50 text-blue-200',
  }[type];

  const icons = {
    error: <XCircle className="w-5 h-5 text-red-400 shrink-0" />,
    success: <CheckCircle className="w-5 h-5 text-emerald-400 shrink-0" />,
    warning: <AlertCircle className="w-5 h-5 text-amber-400 shrink-0" />,
    info: <Info className="w-5 h-5 text-blue-400 shrink-0" />,
  }[type];

  return (
    <div className={`p-4 rounded-xl border backdrop-blur-md flex items-center justify-between gap-3 shadow-lg transition-all animate-in fade-in slide-in-from-top-2 ${styles}`}>
      <div className="flex items-start sm:items-center gap-3">
        {icons}
        <div>
          {title && <div className="text-sm font-bold text-white mb-0.5">{title}</div>}
          <span className="text-sm font-medium">{message}</span>
        </div>
      </div>
      {onClose && (
        <button
          onClick={onClose}
          className="text-xs hover:opacity-80 underline ml-2 shrink-0"
        >
          Dismiss
        </button>
      )}
    </div>
  );
};

export default Alert;

