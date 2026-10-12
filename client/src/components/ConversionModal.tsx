import React from 'react';
import type { ConversionResult, ProgressReport } from '../types';
import { 
  CheckCircle2, 
  XCircle, 
  FolderOpen, 
  FileText, 
  ExternalLink, 
  X, 
  Loader2,
  Clock
} from 'lucide-react';
import { nativeBridge } from '../services/nativeBridge';

interface ConversionModalProps {
  isOpen: boolean;
  isProcessing: boolean;
  progress: ProgressReport | null;
  result: ConversionResult | null;
  outputDirectory: string;
  onClose: () => void;
}

export const ConversionModal: React.FC<ConversionModalProps> = ({
  isOpen,
  isProcessing,
  progress,
  result,
  outputDirectory,
  onClose,
}) => {
  if (!isOpen) return null;

  const handleOpenFolder = () => {
    nativeBridge.openDirectory(outputDirectory);
  };

  const handleOpenFile = (path: string) => {
    nativeBridge.openFile(path);
  };

  return (
    <div className="modal-backdrop">
      <div className="modal-card modal-progress-card">
        {/* Header */}
        <div className="modal-header">
          <h3 className="modal-title">
            {isProcessing ? 'Processing Files...' : result?.success ? 'Conversion Complete!' : 'Conversion Result'}
          </h3>
          {!isProcessing && (
            <button type="button" className="btn-close-modal" onClick={onClose}>
              <X size={18} />
            </button>
          )}
        </div>

        <div className="modal-body">
          {/* Ongoing Processing State */}
          {isProcessing && (
            <div className="progress-body">
              <div className="progress-spinner-container">
                <Loader2 className="animate-spin text-accent" size={48} />
              </div>

              <div className="progress-bar-wrapper">
                <div 
                  className="progress-bar-fill" 
                  style={{ width: `${progress ? progress.percentage : 15}%` }} 
                />
              </div>

              <div className="progress-stats">
                <span className="progress-status-text">
                  {progress?.statusText || 'Initializing file engine...'}
                </span>
                <span className="progress-percent">
                  {progress ? `${progress.percentage}%` : '...'}
                </span>
              </div>

              {progress?.currentFile && (
                <div className="progress-current-file">
                  Current file: <strong>{progress.currentFile}</strong>
                </div>
              )}
            </div>
          )}

          {/* Finished State */}
          {!isProcessing && result && (
            <div className="result-body">
              <div className={`result-status-banner ${result.success ? 'banner-success' : 'banner-error'}`}>
                {result.success ? (
                  <CheckCircle2 size={32} className="text-emerald" />
                ) : (
                  <XCircle size={32} className="text-rose" />
                )}
                <div className="banner-details">
                  <div className="banner-title">
                    {result.success ? 'Tasks Successfully Processed' : 'Conversion Completed with Errors'}
                  </div>
                  <div className="banner-message">{result.message}</div>
                </div>
              </div>

              <div className="result-meta-row">
                <span className="meta-pill">
                  <Clock size={14} /> {result.durationMs} ms
                </span>
                <span className="meta-pill">
                  {result.generatedFilePaths.length} file(s) generated
                </span>
              </div>

              {/* Generated Files List */}
              {result.generatedFilePaths.length > 0 && (
                <div className="generated-files-container">
                  <h4 className="generated-heading">Generated Output Files:</h4>
                  <div className="generated-files-list">
                    {result.generatedFilePaths.map((filePath, idx) => {
                      const fileName = filePath.split(/[\\/]/).pop() || filePath;
                      return (
                        <div key={idx} className="generated-file-row">
                          <div className="file-name-group">
                            <FileText size={16} className="text-accent" />
                            <span className="gen-file-name" title={filePath}>{fileName}</span>
                          </div>
                          <button
                            type="button"
                            className="btn-open-file"
                            onClick={() => handleOpenFile(filePath)}
                            title="Open file in default system application"
                          >
                            <ExternalLink size={14} />
                            <span>Open</span>
                          </button>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Errors Display if any */}
              {result.errors.length > 0 && (
                <div className="errors-container">
                  <h4 className="errors-heading">Warnings & Errors:</h4>
                  <ul className="errors-list">
                    {result.errors.map((err, i) => (
                      <li key={i}>{err}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="modal-footer">
          {!isProcessing && (
            <>
              <button
                type="button"
                className="btn-secondary"
                onClick={handleOpenFolder}
              >
                <FolderOpen size={16} />
                <span>Open Destination Folder</span>
              </button>
              <button
                type="button"
                className="btn-primary"
                onClick={onClose}
              >
                <span>Done</span>
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
