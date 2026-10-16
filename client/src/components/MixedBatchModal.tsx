import React, { useState, useEffect } from 'react';
import type { FileItem, ConversionMatrixMap, ConversionItem } from '../types';
import { Layers, ArrowRight, X, Sparkles, CheckCircle2 } from 'lucide-react';

interface MixedBatchModalProps {
  isOpen: boolean;
  onClose: () => void;
  selectedFiles: FileItem[];
  matrix: ConversionMatrixMap;
  onConfirmBatch: (items: ConversionItem[]) => void;
}

interface GroupConfig {
  extension: string;
  files: FileItem[];
  selectedTarget: string;
}

export const MixedBatchModal: React.FC<MixedBatchModalProps> = ({
  isOpen,
  onClose,
  selectedFiles,
  matrix,
  onConfirmBatch,
}) => {
  const [groups, setGroups] = useState<GroupConfig[]>([]);

  // When modal opens or selected files change, group by extension
  useEffect(() => {
    if (!isOpen) return;

    const groupMap = new Map<string, FileItem[]>();
    for (const file of selectedFiles) {
      const ext = file.extension.replace('.', '').toLowerCase();
      if (!groupMap.has(ext)) {
        groupMap.set(ext, []);
      }
      groupMap.get(ext)!.push(file);
    }

    const initialGroups: GroupConfig[] = Array.from(groupMap.entries()).map(([ext, files]) => {
      const available = matrix[ext] || [];
      // Pick sensible default target: pdf for documents, jpg/png for images/raw, mp3 for audio/video, zip for archives
      let defaultTarget = available[0]?.extension || '';
      if (available.length > 0) {
        if (['jpg', 'jpeg', 'cr2', 'nef', 'arw', 'dng', 'png', 'webp'].includes(ext)) {
          defaultTarget = available.find((t) => t.extension === 'jpg' || t.extension === 'png')?.extension || defaultTarget;
        } else if (['mp4', 'mkv', 'avi', 'mov'].includes(ext)) {
          defaultTarget = available.find((t) => t.extension === 'mp3' || t.extension === 'mp4')?.extension || defaultTarget;
        } else if (['docx', 'doc', 'odt', 'rtf', 'txt', 'html'].includes(ext)) {
          defaultTarget = available.find((t) => t.extension === 'pdf')?.extension || defaultTarget;
        }
      }

      return {
        extension: ext,
        files,
        selectedTarget: defaultTarget,
      };
    });

    setGroups(initialGroups);
  }, [isOpen, selectedFiles, matrix]);

  if (!isOpen) return null;

  const handleTargetChange = (extension: string, newTarget: string) => {
    setGroups((prev) =>
      prev.map((g) => (g.extension === extension ? { ...g, selectedTarget: newTarget } : g))
    );
  };

  const handleExecute = () => {
    const items: ConversionItem[] = [];
    for (const group of groups) {
      if (!group.selectedTarget) continue;
      for (const file of group.files) {
        items.push({
          sourceFilePath: file.fullPath,
          targetExtension: group.selectedTarget,
        });
      }
    }
    if (items.length === 0) return;
    onConfirmBatch(items);
  };

  const totalConvertibleFiles = groups
    .filter((g) => !!g.selectedTarget)
    .reduce((sum, g) => sum + g.files.length, 0);

  const getFormatBadgeStyle = (ext: string) => {
    if (['pdf', 'docx', 'doc', 'odt', 'txt', 'rtf'].includes(ext)) return 'badge-doc';
    if (['png', 'jpg', 'jpeg', 'webp', 'cr2', 'nef', 'arw', 'dng', 'gif'].includes(ext)) return 'badge-img';
    if (['mp3', 'wav', 'flac', 'aac', 'ogg'].includes(ext)) return 'badge-audio';
    if (['mp4', 'mkv', 'mov', 'webm'].includes(ext)) return 'badge-video';
    if (['zip', '7z', 'tar', 'rar'].includes(ext)) return 'badge-archive';
    return 'badge-data';
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content modal-large mixed-batch-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="modal-title-group">
            <div className="modal-icon-badge">
              <Layers size={22} className="text-accent" />
            </div>
            <div>
              <h2 className="modal-title">Mixed Batch Conversion</h2>
              <p className="modal-subtitle">
                Configure independent target formats for each format group in your selection.
              </p>
            </div>
          </div>
          <button type="button" className="modal-close-btn" onClick={onClose} aria-label="Close">
            <X size={20} />
          </button>
        </div>

        <div className="modal-body mixed-batch-body">
          <div className="mixed-summary-banner">
            <Sparkles size={18} className="text-accent" />
            <span>
              Detected <strong>{groups.length} different format groups</strong> containing{' '}
              <strong>{selectedFiles.length} total files</strong>.
            </span>
          </div>

          <div className="format-groups-list">
            {groups.map((group) => {
              const availableTargets = matrix[group.extension] || [];

              return (
                <div key={group.extension} className="group-card">
                  <div className="group-card-header">
                    <div className="group-source-info">
                      <span className={`format-pill ${getFormatBadgeStyle(group.extension)}`}>
                        .{group.extension.toUpperCase()}
                      </span>
                      <div className="group-text">
                        <h4 className="group-title">
                          {group.extension.toUpperCase()} Files ({group.files.length})
                        </h4>
                        <div className="group-files-preview">
                          {group.files.map((f) => f.name).join(', ')}
                        </div>
                      </div>
                    </div>

                    <ArrowRight size={20} className="group-arrow text-muted" />

                    <div className="group-target-selection">
                      <label htmlFor={`target-select-${group.extension}`} className="target-select-label">
                        Convert into:
                      </label>
                      {availableTargets.length > 0 ? (
                        <select
                          id={`target-select-${group.extension}`}
                          className="group-target-select"
                          value={group.selectedTarget}
                          onChange={(e) => handleTargetChange(group.extension, e.target.value)}
                        >
                          {availableTargets.map((target) => (
                            <option key={target.extension} value={target.extension}>
                              {target.label}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <span className="text-warning text-sm" style={{ padding: '6px 12px', background: 'rgba(234, 179, 8, 0.12)', borderRadius: '6px' }}>
                          No compatible converter available
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="modal-footer">
          <button type="button" className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            className="btn-primary"
            onClick={handleExecute}
            disabled={totalConvertibleFiles === 0}
          >
            <CheckCircle2 size={18} />
            <span>Convert {totalConvertibleFiles} File(s)</span>
          </button>
        </div>
      </div>
    </div>
  );
};
