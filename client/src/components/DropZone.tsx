import React, { useState, useRef, useEffect } from 'react';
import { UploadCloud, FolderSearch, FileText, Image, Database, Globe } from 'lucide-react';

interface DropZoneProps {
  onBrowseFiles: () => void;
  onFilesDropped: (files: FileList | File[]) => void;
}

export const DropZone: React.FC<DropZoneProps> = ({
  onBrowseFiles,
  onFilesDropped,
}) => {
  const [isDragOver, setIsDragOver] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Global clipboard paste listener
  useEffect(() => {
    const handlePaste = (e: ClipboardEvent) => {
      // Avoid intercepting paste inside text inputs or editable elements
      const target = e.target as HTMLElement;
      if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)) {
        return;
      }

      if (e.clipboardData && e.clipboardData.files && e.clipboardData.files.length > 0) {
        e.preventDefault();
        onFilesDropped(e.clipboardData.files);
      }
    };

    window.addEventListener('paste', handlePaste);
    return () => {
      window.removeEventListener('paste', handlePaste);
    };
  }, [onFilesDropped]);

  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);

    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      onFilesDropped(e.dataTransfer.files);
    }
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      onFilesDropped(e.target.files);
      e.target.value = '';
    }
  };

  return (
    <div
      className={`dropzone-container ${isDragOver ? 'drag-over' : ''}`}
      onDragEnter={handleDragEnter}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileInputChange}
        multiple
        style={{ display: 'none' }}
      />

      <div className="dropzone-glow" />

      <div className="dropzone-content">
        <div className="dropzone-icon-wrapper">
          <UploadCloud size={46} className="dropzone-icon" />
        </div>

        <h2 className="dropzone-title">
          Drag & Drop, Browse, or Paste your files here
        </h2>
        <p className="dropzone-subtitle">
          Universal multi-format studio: Documents, Images, Spreadsheets & Data files
        </p>

        <div className="dropzone-actions">
          <button
            type="button"
            className="btn-browse-primary"
            onClick={onBrowseFiles}
          >
            <FolderSearch size={18} />
            <span>Browse Files (Explorer)</span>
          </button>
        </div>

        <div className="supported-formats-pills">
          <span className="format-pill pdf-pill">
            <FileText size={12} /> Documents (.pdf, .docx, .txt, .md)
          </span>
          <span className="format-pill img-pill">
            <Image size={12} /> Images (.png, .jpg, .webp, .bmp, .gif, .ico)
          </span>
          <span className="format-pill data-pill">
            <Database size={12} /> Data (.csv, .json, .xml, .xlsx)
          </span>
          <span className="format-pill web-pill">
            <Globe size={12} /> Web (.html, .htm)
          </span>
        </div>
      </div>
    </div>
  );
};
