import React, { useState, useEffect, useCallback } from 'react';
import type { FileItem, ProgressReport, ConversionResult, ConversionRequest, ConversionMatrixMap, ConversionItem } from './types';
import { nativeBridge } from './services/nativeBridge';
import { Header } from './components/Header';
import { DropZone } from './components/DropZone';
import { FileQueue } from './components/FileQueue';
import { ActionBar } from './components/ActionBar';
import { SplitModal } from './components/SplitModal';
import { ConversionModal } from './components/ConversionModal';
import { MixedBatchModal } from './components/MixedBatchModal';

export const App: React.FC = () => {
  const [darkMode, setDarkMode] = useState<boolean>(() => {
    return localStorage.getItem('file_converter_dark_mode') === 'true';
  });

  const [outputDirectory, setOutputDirectory] = useState<string>('');
  const [fileQueue, setFileQueue] = useState<FileItem[]>([]);
  const [matrix, setMatrix] = useState<ConversionMatrixMap>({});
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [activeProgress, setActiveProgress] = useState<ProgressReport | null>(null);
  const [conversionResult, setConversionResult] = useState<ConversionResult | null>(null);
  const [isConversionModalOpen, setIsConversionModalOpen] = useState<boolean>(false);
  const [isSplitModalOpen, setIsSplitModalOpen] = useState<boolean>(false);
  const [isMixedModalOpen, setIsMixedModalOpen] = useState<boolean>(false);

  // Apply dark mode class to root html element
  useEffect(() => {
    if (darkMode) {
      document.documentElement.classList.add('dark');
      localStorage.setItem('file_converter_dark_mode', 'true');
    } else {
      document.documentElement.classList.remove('dark');
      localStorage.setItem('file_converter_dark_mode', 'false');
    }
  }, [darkMode]);

  // Load initial backend configuration and full conversion matrix
  useEffect(() => {
    nativeBridge.getInitialConfig().then((config) => {
      if (config && config.defaultOutputDirectory) {
        setOutputDirectory(config.defaultOutputDirectory);
      }
    });

    nativeBridge.getConversionMatrix().then((matrixData) => {
      if (matrixData) {
        setMatrix(matrixData);
      }
    });

    const unbindProgress = nativeBridge.on('conversionProgress', (data: ProgressReport) => {
      setActiveProgress(data);
    });

    const unbindDrops = nativeBridge.on('filesDropped', (items: FileItem[]) => {
      if (items && items.length > 0) {
        addItemsToQueue(items);
      }
    });

    return () => {
      unbindProgress();
      unbindDrops();
    };
  }, []);

  // Add items to queue and query dynamic target formats from C# backend
  const addItemsToQueue = useCallback(async (newItems: FileItem[]) => {
    setFileQueue((prev) => {
      const existingPaths = new Set(prev.map((f) => f.fullPath || f.name));
      const filtered = newItems.filter((item) => !existingPaths.has(item.fullPath || item.name));
      const withSelection = filtered.map((f) => ({ ...f, selected: true }));
      return [...prev, ...withSelection];
    });

    // Dynamic Target Resolution from C# backend
    const uniqueExts = Array.from(new Set(newItems.map((f) => f.extension.replace('.', '').toLowerCase())));
    for (const ext of uniqueExts) {
      try {
        const targets = await nativeBridge.getTargetFormats(ext);
        if (targets && targets.length > 0) {
          setMatrix((prev) => ({ ...prev, [ext]: targets }));
        }
      } catch (err) {
        console.warn(`Dynamic target resolution for .${ext} failed:`, err);
      }
    }
  }, []);

  const handleBrowseFiles = async () => {
    try {
      const files = await nativeBridge.selectFiles();
      if (files && files.length > 0) {
        await addItemsToQueue(files);
      }
    } catch (err) {
      console.error('Failed to open file browser dialog', err);
    }
  };

  const handleChangeOutputDirectory = async () => {
    try {
      const newFolder = await nativeBridge.selectFolder(outputDirectory);
      if (newFolder) {
        setOutputDirectory(newFolder);
      }
    } catch (err) {
      console.error('Failed to select output folder', err);
    }
  };

  const handleFilesDropped = async (droppedFiles: FileList | File[]) => {
    const paths: string[] = [];
    const simulatedItems: FileItem[] = [];

    for (let i = 0; i < droppedFiles.length; i++) {
      const file = droppedFiles[i];
      const anyFile = file as any;
      if (anyFile.path) {
        paths.push(anyFile.path);
      } else {
        const ext = '.' + file.name.split('.').pop()?.toLowerCase();
        let cat: any = 'other';
        if (ext === '.pdf') cat = 'pdf';
        else if (['.docx', '.doc', '.odt', '.rtf'].includes(ext)) cat = 'word';
        else if (['.png', '.jpg', '.jpeg', '.webp', '.bmp', '.gif', '.ico', '.tiff', '.cr2', '.nef', '.arw'].includes(ext)) cat = 'image';
        else if (['.txt', '.md', '.log'].includes(ext)) cat = 'text';
        else if (['.csv', '.json', '.xml', '.xlsx', '.xls', '.yaml'].includes(ext)) cat = 'data';
        else if (['.html', '.htm'].includes(ext)) cat = 'web';

        simulatedItems.push({
          id: Math.random().toString(36).substring(2, 9),
          name: file.name,
          fullPath: file.name,
          extension: ext,
          sizeBytes: file.size,
          formattedSize: `${(file.size / (1024 * 1024)).toFixed(1)} MB`,
          category: cat,
          selected: true,
        });
      }
    }

    if (paths.length > 0) {
      try {
        const inspected = await nativeBridge.inspectPaths(paths);
        await addItemsToQueue(inspected);
      } catch (err) {
        console.error('Failed to inspect dropped paths', err);
      }
    } else if (simulatedItems.length > 0) {
      // In desktop environment, if paths are not directly on File objects (e.g. clipboard paste),
      // query the native clipboard to resolve real filesystem paths
      if (nativeBridge.isDesktopEnvironment) {
        try {
          const nativeClipboardFiles = await nativeBridge.getClipboardFiles();
          if (nativeClipboardFiles && nativeClipboardFiles.length > 0) {
            await addItemsToQueue(nativeClipboardFiles);
            return;
          }
        } catch (err) {
          console.warn('Native clipboard file lookup fallback:', err);
        }
      }
      await addItemsToQueue(simulatedItems);
    }
  };

  const handleToggleSelect = (id: string) => {
    setFileQueue((prev) =>
      prev.map((f) => (f.id === id ? { ...f, selected: !f.selected } : f))
    );
  };

  const handleSelectAll = (select: boolean) => {
    setFileQueue((prev) => prev.map((f) => ({ ...f, selected: select })));
  };

  const handleRemoveFile = (id: string) => {
    setFileQueue((prev) => prev.filter((f) => f.id !== id));
  };

  const handleClearQueue = () => {
    setFileQueue([]);
  };

  // Convert selected files to specified target extension
  const handleTriggerTargetConversion = async (targetExtension: string) => {
    const selectedFiles = fileQueue.filter((f) => f.selected);
    if (selectedFiles.length === 0) return;

    // Filter to files that actually support this target extension
    const cleanTarget = targetExtension.toLowerCase();
    const convertibleFiles = selectedFiles.filter((f) => {
      const srcExt = f.extension.replace('.', '').toLowerCase();
      const available = matrix[srcExt] || [];
      return available.some((opt) => opt.extension.toLowerCase() === cleanTarget);
    });

    const filesToConvert = convertibleFiles.length > 0 ? convertibleFiles : selectedFiles;

    const request: ConversionRequest = {
      actionType: 0, // ConvertToTargetFormat
      targetExtension: cleanTarget,
      sourceFilePaths: filesToConvert.map((f) => f.fullPath),
      outputDirectory: outputDirectory,
    };

    executeConversion(request, `Converting ${filesToConvert.length} file(s) to ${cleanTarget.toUpperCase()}...`);
  };

  // Handle mixed batch conversion
  const handleConfirmMixedBatch = (items: ConversionItem[]) => {
    setIsMixedModalOpen(false);
    if (!items || items.length === 0) return;

    const request: ConversionRequest = {
      actionType: 0,
      targetExtension: 'mixed',
      sourceFilePaths: items.map((i) => i.sourceFilePath),
      items: items,
      outputDirectory: outputDirectory,
    };

    executeConversion(request, `Processing mixed batch of ${items.length} files...`);
  };

  // Quick single-file convert
  const handleQuickConvertFile = (file: FileItem, targetExt: string) => {
    const request: ConversionRequest = {
      actionType: 0,
      targetExtension: targetExt.toLowerCase(),
      sourceFilePaths: [file.fullPath],
      outputDirectory: outputDirectory,
    };
    executeConversion(request, `Converting ${file.name} to ${targetExt.toUpperCase()}...`);
  };

  // Merge selected files into a single unified PDF
  const handleTriggerMerge = () => {
    const selectedFiles = fileQueue.filter((f) => f.selected);
    if (selectedFiles.length < 2) return;

    const request: ConversionRequest = {
      actionType: 1, // MergeDocuments
      targetExtension: 'pdf',
      sourceFilePaths: selectedFiles.map((f) => f.fullPath),
      outputDirectory: outputDirectory,
    };

    executeConversion(request, `Merging ${selectedFiles.length} files into 1 PDF...`);
  };

  // Split selected PDF
  const handleOpenSplitModal = () => {
    const selectedPdf = fileQueue.find((f) => f.selected && f.extension.toLowerCase().endsWith('pdf'));
    if (!selectedPdf) return;
    setIsSplitModalOpen(true);
  };

  const handleConfirmSplit = (options: { pageRange?: string }) => {
    const selectedPdf = fileQueue.find((f) => f.selected && f.extension.toLowerCase().endsWith('pdf'));
    if (!selectedPdf) return;
    setIsSplitModalOpen(false);

    const request: ConversionRequest = {
      actionType: 2, // SplitDocument
      targetExtension: 'pdf',
      sourceFilePaths: [selectedPdf.fullPath],
      outputDirectory: outputDirectory,
      pageRangeExpression: options?.pageRange,
    };

    executeConversion(request, `Splitting ${selectedPdf.name}...`);
  };

  const executeConversion = async (request: ConversionRequest, initialStatusText: string) => {
    setIsProcessing(true);
    setActiveProgress({
      jobId: '',
      percentage: 5,
      currentFile: request.sourceFilePaths[0]?.split(/[\\/]/).pop() || '',
      statusText: initialStatusText,
    });
    setConversionResult(null);
    setIsConversionModalOpen(true);

    try {
      const res = await nativeBridge.convert(request);
      setConversionResult(res);
    } catch (err: any) {
      setConversionResult({
        jobId: '',
        success: false,
        message: err.message || 'Conversion failed with unexpected error.',
        generatedFilePaths: [],
        errors: [err.message || 'Unknown error'],
        durationMs: 0,
        processedFilesCount: 0,
      });
    } finally {
      setIsProcessing(false);
    }
  };

  const selectedFiles = fileQueue.filter((f) => f.selected);
  const selectedPdf = selectedFiles.find((f) => f.extension.toLowerCase().endsWith('pdf'));

  return (
    <div className="app-shell">
      <Header
        darkMode={darkMode}
        onToggleDarkMode={() => setDarkMode(!darkMode)}
        outputDirectory={outputDirectory}
        onChangeOutputDirectory={handleChangeOutputDirectory}
      />

      <main className="app-main-content">
        <div className="layout-grid">
          <section className="dropzone-section">
            <DropZone
              onBrowseFiles={handleBrowseFiles}
              onFilesDropped={handleFilesDropped}
            />
          </section>

          {fileQueue.length > 0 && (
            <section className="workspace-section">
              <FileQueue
                files={fileQueue}
                matrix={matrix}
                onToggleSelect={handleToggleSelect}
                onSelectAll={handleSelectAll}
                onRemoveFile={handleRemoveFile}
                onClearQueue={handleClearQueue}
                onQuickConvertFile={handleQuickConvertFile}
              />

              <ActionBar
                files={fileQueue}
                matrix={matrix}
                onTriggerTargetConversion={handleTriggerTargetConversion}
                onOpenSplitModal={handleOpenSplitModal}
                onTriggerMerge={handleTriggerMerge}
                onOpenMixedModal={() => setIsMixedModalOpen(true)}
                isProcessing={isProcessing}
              />
            </section>
          )}
        </div>
      </main>

      <SplitModal
        isOpen={isSplitModalOpen}
        onClose={() => setIsSplitModalOpen(false)}
        fileName={selectedPdf?.name || 'Selected PDF'}
        onConfirmSplit={handleConfirmSplit}
      />

      <MixedBatchModal
        isOpen={isMixedModalOpen}
        onClose={() => setIsMixedModalOpen(false)}
        selectedFiles={selectedFiles}
        matrix={matrix}
        onConfirmBatch={handleConfirmMixedBatch}
      />

      <ConversionModal
        isOpen={isConversionModalOpen}
        isProcessing={isProcessing}
        progress={activeProgress}
        result={conversionResult}
        outputDirectory={outputDirectory}
        onClose={() => setIsConversionModalOpen(false)}
      />
    </div>
  );
};

export default App;
