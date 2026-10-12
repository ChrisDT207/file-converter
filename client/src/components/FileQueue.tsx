import React from 'react';
import type { FileItem, ConversionMatrixMap } from '../types';
import { 
  FileText, 
  Image as ImageIcon, 
  FileCode, 
  Trash2, 
  CheckSquare, 
  Square, 
  X,
  Database,
  Globe
} from 'lucide-react';

interface FileQueueProps {
  files: FileItem[];
  matrix: ConversionMatrixMap;
  onToggleSelect: (id: string) => void;
  onSelectAll: (select: boolean) => void;
  onRemoveFile: (id: string) => void;
  onClearQueue: () => void;
  onQuickConvertFile?: (file: FileItem, targetExt: string) => void;
}

export const FileQueue: React.FC<FileQueueProps> = ({
  files,
  matrix,
  onToggleSelect,
  onSelectAll,
  onRemoveFile,
  onClearQueue,
  onQuickConvertFile,
}) => {
  if (files.length === 0) return null;

  const selectedCount = files.filter((f) => f.selected).length;
  const allSelected = selectedCount === files.length && files.length > 0;

  const renderFileIcon = (category: string) => {
    switch (category) {
      case 'pdf':
        return <FileText className="file-cat-icon text-pdf" size={20} />;
      case 'word':
        return <FileText className="file-cat-icon text-word" size={20} />;
      case 'image':
        return <ImageIcon className="file-cat-icon text-image" size={20} />;
      case 'text':
        return <FileCode className="file-cat-icon text-text" size={20} />;
      case 'data':
        return <Database className="file-cat-icon text-accent" size={20} />;
      case 'web':
        return <Globe className="file-cat-icon text-emerald" size={20} />;
      default:
        return <FileText className="file-cat-icon text-muted" size={20} />;
    }
  };

  const getBadgeClass = (category: string) => {
    switch (category) {
      case 'pdf': return 'badge-pdf';
      case 'word': return 'badge-word';
      case 'image': return 'badge-image';
      case 'text': return 'badge-text';
      case 'data': return 'badge-data';
      case 'web': return 'badge-web';
      default: return 'badge-other';
    }
  };

  return (
    <div className="queue-wrapper">
      <div className="queue-header">
        <div className="queue-header-left">
          <button
            type="button"
            className="btn-select-all"
            onClick={() => onSelectAll(!allSelected)}
          >
            {allSelected ? (
              <CheckSquare size={18} className="text-accent" />
            ) : (
              <Square size={18} className="text-muted" />
            )}
            <span>Select All</span>
          </button>
          <div className="queue-counter-tag">
            <span className="counter-highlight">{selectedCount}</span> of {files.length} selected
          </div>
        </div>

        <div className="queue-header-right">
          <button
            type="button"
            className="btn-clear-queue"
            onClick={onClearQueue}
            title="Clear all queued files"
          >
            <Trash2 size={15} />
            <span>Clear Queue</span>
          </button>
        </div>
      </div>

      <div className="queue-list-container">
        <div className="queue-list">
          {files.map((file) => {
            const ext = file.extension.replace('.', '').toLowerCase();
            const targets = matrix[ext] || [];

            return (
              <div
                key={file.id}
                className={`queue-item ${file.selected ? 'item-selected' : ''}`}
              >
                <div 
                  className="item-checkbox"
                  onClick={() => onToggleSelect(file.id)}
                >
                  {file.selected ? (
                    <CheckSquare size={18} className="text-accent" />
                  ) : (
                    <Square size={18} className="text-muted" />
                  )}
                </div>

                <div className="item-icon-area">
                  {renderFileIcon(file.category)}
                </div>

                <div className="item-details" onClick={() => onToggleSelect(file.id)}>
                  <div className="item-name" title={file.fullPath || file.name}>
                    {file.name}
                  </div>
                  <div className="item-meta">
                    <span className={`format-badge ${getBadgeClass(file.category)}`}>
                      {file.extension.toUpperCase().replace('.', '')}
                    </span>
                    <span className="item-size">{file.formattedSize}</span>

                    {targets.length > 0 && (
                      <span className="available-targets-hint" title={targets.map(t => t.label).join(', ')}>
                        {targets.length} targets available
                      </span>
                    )}

                    {file.fullPath && (
                      <span className="item-path-preview" title={file.fullPath}>
                        {file.fullPath}
                      </span>
                    )}
                  </div>
                </div>

                {/* Quick single-file convert dropdown if handler supplied */}
                {targets.length > 0 && onQuickConvertFile && (
                  <div className="item-quick-convert">
                    <select
                      className="quick-convert-select"
                      value=""
                      onChange={(e) => {
                        if (e.target.value) {
                          onQuickConvertFile(file, e.target.value);
                        }
                      }}
                      title="Convert this single file"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <option value="" disabled>Convert to...</option>
                      {targets.map((t) => (
                        <option key={t.extension} value={t.extension}>
                          {t.label}
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                <div className="item-actions">
                  <button
                    type="button"
                    className="btn-remove-item"
                    onClick={(e) => {
                      e.stopPropagation();
                      onRemoveFile(file.id);
                    }}
                    title="Remove from queue"
                  >
                    <X size={16} />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
