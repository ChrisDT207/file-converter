import React, { useState } from 'react';
import { Scissors, X, Check, AlertCircle } from 'lucide-react';

interface SplitModalProps {
  isOpen: boolean;
  onClose: () => void;
  fileName: string;
  onConfirmSplit: (options: { pageRange?: string }) => void;
}

export const SplitModal: React.FC<SplitModalProps> = ({
  isOpen,
  onClose,
  fileName,
  onConfirmSplit,
}) => {
  const [splitMode, setSplitMode] = useState<'all' | 'custom'>('all');
  const [rangeInput, setRangeInput] = useState('');

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (splitMode === 'all') {
      onConfirmSplit({});
    } else {
      if (!rangeInput.trim()) return;
      onConfirmSplit({ pageRange: rangeInput.trim() });
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal-card">
        <div className="modal-header">
          <div className="modal-title-group">
            <div className="modal-icon-badge">
              <Scissors size={20} className="text-accent" />
            </div>
            <div>
              <h3 className="modal-title">Split Document</h3>
              <p className="modal-subtitle">File: {fileName}</p>
            </div>
          </div>
          <button type="button" className="btn-close-modal" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            <div className="split-options-group">
              <label className={`split-option-card ${splitMode === 'all' ? 'option-selected' : ''}`}>
                <input
                  type="radio"
                  name="splitMode"
                  value="all"
                  checked={splitMode === 'all'}
                  onChange={() => setSplitMode('all')}
                />
                <div className="option-info">
                  <div className="option-title">Extract Every Page</div>
                  <div className="option-desc">Split into separate 1-page PDF files</div>
                </div>
              </label>

              <label className={`split-option-card ${splitMode === 'custom' ? 'option-selected' : ''}`}>
                <input
                  type="radio"
                  name="splitMode"
                  value="custom"
                  checked={splitMode === 'custom'}
                  onChange={() => setSplitMode('custom')}
                />
                <div className="option-info">
                  <div className="option-title">Custom Page Ranges</div>
                  <div className="option-desc">Define specific page segments or ranges</div>
                </div>
              </label>
            </div>

            {splitMode === 'custom' && (
              <div className="range-input-container">
                <label className="input-label" htmlFor="rangeInput">
                  Page Ranges Expression:
                </label>
                <input
                  id="rangeInput"
                  type="text"
                  className="text-input"
                  placeholder="e.g. 1-3, 5, 7-10"
                  value={rangeInput}
                  onChange={(e) => setRangeInput(e.target.value)}
                  autoFocus
                />
                <div className="range-help-text">
                  <AlertCircle size={14} />
                  <span>Example: <code>1-3, 5, 8-12</code> will create 3 documents: pages 1 to 3, page 5, and pages 8 to 12.</span>
                </div>
              </div>
            )}
          </div>

          <div className="modal-footer">
            <button type="button" className="btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button
              type="submit"
              className="btn-primary"
              disabled={splitMode === 'custom' && !rangeInput.trim()}
            >
              <Check size={16} />
              <span>Split PDF Now</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
