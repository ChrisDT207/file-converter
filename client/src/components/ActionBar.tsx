import React, { useState, useMemo } from 'react';
import type { FileItem, ConversionMatrixMap, TargetOption } from '../types';
import { 
  Sparkles, 
  GitMerge, 
  Scissors, 
  ArrowRight,
  FileText,
  Image as ImageIcon,
  Database,
  Globe,
  Layers,
  Music,
  Video,
  Box,
  Archive,
  SlidersHorizontal
} from 'lucide-react';

interface ActionBarProps {
  files: FileItem[];
  matrix: ConversionMatrixMap;
  onTriggerTargetConversion: (targetExtension: string) => void;
  onOpenSplitModal: () => void;
  onTriggerMerge: () => void;
  onOpenMixedModal: () => void;
  isProcessing: boolean;
}

export const ActionBar: React.FC<ActionBarProps> = ({
  files,
  matrix,
  onTriggerTargetConversion,
  onOpenSplitModal,
  onTriggerMerge,
  onOpenMixedModal,
  isProcessing,
}) => {
  const [activeCategory, setActiveCategory] = useState<string>('all');
  const selectedFiles = useMemo(() => files.filter((f) => f.selected), [files]);
  const count = selectedFiles.length;

  const uniqueExtensions = useMemo(() => {
    return Array.from(new Set(selectedFiles.map((f) => f.extension.replace('.', '').toLowerCase())));
  }, [selectedFiles]);

  const isMixedBatch = uniqueExtensions.length > 1;

  // Compute all available target options across all selected files
  const availableTargets = useMemo(() => {
    if (selectedFiles.length === 0) return [];

    const targetMap = new Map<string, { option: TargetOption; count: number }>();

    for (const file of selectedFiles) {
      const ext = file.extension.replace('.', '').toLowerCase();
      const options = matrix[ext] || [];

      for (const opt of options) {
        if (!targetMap.has(opt.extension)) {
          targetMap.set(opt.extension, { option: opt, count: 1 });
        } else {
          targetMap.get(opt.extension)!.count += 1;
        }
      }
    }

    return Array.from(targetMap.values());
  }, [selectedFiles, matrix]);

  // Filter by active category tab
  const filteredTargets = useMemo(() => {
    if (activeCategory === 'all') return availableTargets;
    return availableTargets.filter((item) => item.option.category === activeCategory);
  }, [availableTargets, activeCategory]);

  if (count === 0) {
    return (
      <div className="action-bar-empty">
        <Layers className="text-muted" size={24} />
        <p>Select files from your queue to view available conversion targets.</p>
      </div>
    );
  }

  const hasPdf = selectedFiles.some((f) => f.extension.toLowerCase().endsWith('pdf'));
  const canMerge = count >= 2;
  const canSplit = count === 1 && hasPdf;

  const getCategoryIcon = (cat: string) => {
    switch (cat) {
      case 'document': return <FileText size={18} />;
      case 'spreadsheet': case 'data': return <Database size={18} />;
      case 'image': case 'raw': return <ImageIcon size={18} />;
      case 'audio': return <Music size={18} />;
      case 'video': return <Video size={18} />;
      case '3d': return <Box size={18} />;
      case 'archive': return <Archive size={18} />;
      case 'web': return <Globe size={18} />;
      default: return <Sparkles size={18} />;
    }
  };

  const getTargetBadgeColor = (ext: string) => {
    switch (ext) {
      case 'pdf': return 'target-badge-pdf';
      case 'docx': case 'doc': case 'odt': case 'rtf': return 'target-badge-word';
      case 'png': case 'jpg': case 'jpeg': case 'webp': case 'bmp': case 'gif': case 'ico': case 'tiff': case 'avif': return 'target-badge-img';
      case 'csv': case 'json': case 'xlsx': case 'xml': case 'tsv': case 'yaml': return 'target-badge-data';
      case 'mp3': case 'wav': case 'flac': case 'aac': case 'ogg': case 'm4a': return 'target-badge-audio';
      case 'mp4': case 'mkv': case 'mov': case 'webm': case 'avi': return 'target-badge-video';
      case 'zip': case '7z': case 'tar': case 'gz': return 'target-badge-archive';
      case 'obj': case 'stl': case 'gltf': case 'glb': return 'target-badge-3d';
      default: return 'target-badge-default';
    }
  };

  return (
    <div className="action-bar-card">
      {/* Mixed Batch Alert Banner */}
      {isMixedBatch && (
        <div className="mixed-batch-banner">
          <div className="mixed-banner-content">
            <div className="mixed-banner-title">
              <SlidersHorizontal size={18} className="text-accent" />
              <span>
                Mixed Batch: <strong>{count} files</strong> across{' '}
                <strong>{uniqueExtensions.length} different formats</strong> (
                {uniqueExtensions.map((e) => '.' + e.toUpperCase()).join(', ')})
              </span>
            </div>
            <p className="mixed-banner-desc">
              Convert each file type to independent formats simultaneously, or choose a common format below.
            </p>
          </div>
          <button
            type="button"
            className="btn-mixed-configure"
            onClick={onOpenMixedModal}
            disabled={isProcessing}
          >
            <Sparkles size={16} />
            <span>Configure Mixed Batch...</span>
          </button>
        </div>
      )}

      <div className="action-bar-header">
        <div className="action-title-group">
          <Sparkles className="text-accent" size={20} />
          <h3 className="action-heading">
            {isMixedBatch ? 'Quick Convert or Batch Group' : `Convert .${uniqueExtensions[0]?.toUpperCase()} Files`}
          </h3>
          <span className="action-file-badge">{count} file(s) selected</span>
        </div>

        {/* Category Tabs */}
        <div className="category-tabs-container">
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'all' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('all')}
          >
            All ({availableTargets.length})
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'document' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('document')}
          >
            <FileText size={13} /> Documents
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'image' || activeCategory === 'raw' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('image')}
          >
            <ImageIcon size={13} /> Images
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'spreadsheet' || activeCategory === 'data' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('spreadsheet')}
          >
            <Database size={13} /> Spreadsheets
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'audio' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('audio')}
          >
            <Music size={13} /> Audio
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'video' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('video')}
          >
            <Video size={13} /> Video
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === '3d' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('3d')}
          >
            <Box size={13} /> 3D
          </button>
          <button
            type="button"
            className={`category-tab-btn ${activeCategory === 'archive' ? 'tab-active' : ''}`}
            onClick={() => setActiveCategory('archive')}
          >
            <Archive size={13} /> Archives
          </button>
        </div>
      </div>

      {/* Target Conversions Grid */}
      <div className="target-conversions-section">
        {filteredTargets.length === 0 ? (
          <div className="no-targets-msg">
            No conversion options available in this category for the selected files.
          </div>
        ) : (
          <div className="targets-grid">
            {filteredTargets.map(({ option, count: convertibleCount }) => (
              <button
                key={option.extension}
                type="button"
                className="target-card-btn"
                disabled={isProcessing}
                onClick={() => onTriggerTargetConversion(option.extension)}
                title={option.description}
              >
                <div className={`target-icon-wrap ${getTargetBadgeColor(option.extension)}`}>
                  {getCategoryIcon(option.category)}
                </div>

                <div className="target-info">
                  <div className="target-label-row">
                    <span className="target-label">{option.label}</span>
                    {convertibleCount < count && (
                      <span className="target-count-tag" title={`${convertibleCount} of ${count} files compatible`}>
                        {convertibleCount}/{count}
                      </span>
                    )}
                  </div>
                  <div className="target-desc">{option.description}</div>
                </div>

                <ArrowRight size={15} className="target-arrow" />
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Specialized Tools Bar (Merge & Split) */}
      <div className="tools-divider" />
      <div className="tools-bar">
        <span className="tools-title">Specialized Tools:</span>

        {/* Merge Tool */}
        <button
          type="button"
          className="tool-action-btn"
          disabled={isProcessing || !canMerge}
          onClick={onTriggerMerge}
          title={canMerge ? `Consolidate all ${count} files into 1 unified PDF` : 'Select at least 2 files to merge'}
        >
          <GitMerge size={16} className="text-accent" />
          <span>Merge {count} Files to 1 PDF</span>
        </button>

        {/* Split Tool */}
        <button
          type="button"
          className="tool-action-btn"
          disabled={isProcessing || !canSplit}
          onClick={onOpenSplitModal}
          title={canSplit ? 'Split selected PDF into pages or custom ranges' : 'Select a single PDF file to split'}
        >
          <Scissors size={16} className="text-accent" />
          <span>Split PDF Document</span>
        </button>

        {/* Mixed Batch Direct Trigger */}
        {isMixedBatch && (
          <button
            type="button"
            className="tool-action-btn"
            disabled={isProcessing}
            onClick={onOpenMixedModal}
            title="Open Mixed Batch configuration modal"
          >
            <SlidersHorizontal size={16} className="text-accent" />
            <span>Configure Mixed Batch ({uniqueExtensions.length} groups)</span>
          </button>
        )}
      </div>
    </div>
  );
};
