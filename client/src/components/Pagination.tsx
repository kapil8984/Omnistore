import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  totalCount?: number;
  pageSize?: number;
  onPageChange: (page: number) => void;
}

export const Pagination: React.FC<PaginationProps> = ({
  currentPage,
  totalPages,
  totalCount,
  pageSize,
  onPageChange,
}) => {
  if (totalPages <= 1) return null;

  const showItemCounts = typeof totalCount === 'number' && typeof pageSize === 'number';
  const startItem = showItemCounts ? (currentPage - 1) * pageSize! + 1 : null;
  const endItem = showItemCounts ? Math.min(currentPage * pageSize!, totalCount!) : null;

  return (
    <div className="flex flex-col sm:flex-row items-center justify-between gap-4 py-4 px-2 text-sm text-slate-400">
      <div>
        {showItemCounts ? (
          <>
            Showing <span className="font-semibold text-slate-200">{startItem}</span> to{' '}
            <span className="font-semibold text-slate-200">{endItem}</span> of{' '}
            <span className="font-semibold text-slate-200">{totalCount}</span> results
          </>
        ) : (
          <span>
            Page <span className="font-semibold text-slate-200">{currentPage}</span> of{' '}
            <span className="font-semibold text-slate-200">{totalPages}</span>
          </span>
        )}
      </div>

      <div className="flex items-center gap-2">
        <button
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage <= 1}
          className="p-2 rounded-lg bg-slate-800/80 hover:bg-slate-700/80 text-slate-200 disabled:opacity-40 disabled:cursor-not-allowed border border-slate-700/50 transition-colors"
          aria-label="Previous Page"
        >
          <ChevronLeft className="w-4 h-4" />
        </button>

        <span className="px-3 py-1 font-medium text-slate-300">
          Page {currentPage} of {totalPages}
        </span>

        <button
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage >= totalPages}
          className="p-2 rounded-lg bg-slate-800/80 hover:bg-slate-700/80 text-slate-200 disabled:opacity-40 disabled:cursor-not-allowed border border-slate-700/50 transition-colors"
          aria-label="Next Page"
        >
          <ChevronRight className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
};

export default Pagination;
