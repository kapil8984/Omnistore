import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import Alert from '../components/Alert';

describe('Alert Component', () => {
  it('renders error alert with title and message', () => {
    render(<Alert type="error" title="Stock Failure" message="Item is out of stock" />);

    expect(screen.getByText('Stock Failure')).toBeInTheDocument();
    expect(screen.getByText('Item is out of stock')).toBeInTheDocument();
  });

  it('renders success alert and triggers dismiss callback', () => {
    const handleClose = vi.fn();
    render(<Alert type="success" message="Operation completed successfully" onClose={handleClose} />);

    expect(screen.getByText('Operation completed successfully')).toBeInTheDocument();

    const closeBtn = screen.getByRole('button');
    fireEvent.click(closeBtn);

    expect(handleClose).toHaveBeenCalledTimes(1);
  });
});
