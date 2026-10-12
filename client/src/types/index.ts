export type FileCategory = 'pdf' | 'word' | 'image' | 'text' | 'data' | 'web' | 'other';

export interface FileItem {
  id: string;
  name: string;
  fullPath: string;
  extension: string;
  sizeBytes: number;
  formattedSize: string;
  category: FileCategory;
  selected?: boolean;
  targetExtension?: string;
}

export interface TargetOption {
  extension: string;
  label: string;
  category: 'document' | 'image' | 'data' | 'web';
  description: string;
}

export type ConversionMatrixMap = Record<string, TargetOption[]>;

export interface ConversionItem {
  sourceFilePath: string;
  targetExtension: string;
}

export interface ConversionRequest {
  jobId?: string;
  actionType: number; // 0: ConvertToTargetFormat, 1: MergeDocuments, 2: SplitDocument
  targetExtension: string;
  sourceFilePaths: string[];
  items?: ConversionItem[];
  outputDirectory: string;
  customOutputFileName?: string;
  pageRangeExpression?: string;
}

export interface ProgressReport {
  jobId: string;
  percentage: number;
  currentFile: string;
  statusText: string;
}

export interface ConversionResult {
  jobId: string;
  success: boolean;
  message: string;
  generatedFilePaths: string[];
  errors: string[];
  durationMs: number;
  processedFilesCount: number;
}

export interface InitialConfig {
  defaultOutputDirectory: string;
  appVersion: string;
  platform: string;
}
