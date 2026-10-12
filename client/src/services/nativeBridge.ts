import type { ConversionRequest, ConversionResult, FileItem, InitialConfig, ProgressReport, ConversionMatrixMap, TargetOption } from '../types';

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: string) => void;
        addEventListener: (event: string, handler: (e: { data: string | object }) => void) => void;
        removeEventListener: (event: string, handler: (e: { data: string | object }) => void) => void;
      };
    };
  }
}

type EventCallback = (data: any) => void;

class NativeBridgeService {
  private isAvailable: boolean;
  private pendingRequests = new Map<string, { resolve: (data: any) => void; reject: (err: any) => void }>();
  private eventListeners = new Map<string, Set<EventCallback>>();

  constructor() {
    this.isAvailable = typeof window !== 'undefined' && !!window.chrome?.webview?.postMessage;

    if (this.isAvailable && window.chrome?.webview) {
      window.chrome.webview.addEventListener('message', this.handleIncomingMessage.bind(this));
    }
  }

  public get isDesktopEnvironment(): boolean {
    return this.isAvailable;
  }

  private handleIncomingMessage(event: { data: string | object }) {
    let raw = event.data;
    let message: any;

    try {
      message = typeof raw === 'string' ? JSON.parse(raw) : raw;
    } catch {
      return;
    }

    if (!message) return;

    if (message.action && this.eventListeners.has(message.action)) {
      const callbacks = this.eventListeners.get(message.action);
      callbacks?.forEach((cb) => cb(message.data));
    }

    if (message.id && this.pendingRequests.has(message.id)) {
      const pending = this.pendingRequests.get(message.id)!;
      this.pendingRequests.delete(message.id);

      if (message.success) {
        pending.resolve(message.data);
      } else {
        pending.reject(new Error(message.error || 'Desktop operation failed'));
      }
    }
  }

  public sendRequest<T = any>(action: string, payload?: any): Promise<T> {
    return new Promise((resolve, reject) => {
      const id = 'REQ_' + Math.random().toString(36).substring(2, 11) + '_' + Date.now();

      if (!this.isAvailable) {
        this.simulateBrowserFallback(action, payload, resolve);
        return;
      }

      this.pendingRequests.set(id, { resolve, reject });

      try {
        const json = JSON.stringify({ id, action, payload });
        window.chrome!.webview!.postMessage(json);
      } catch (err) {
        this.pendingRequests.delete(id);
        reject(err);
      }
    });
  }

  public on(event: string, callback: EventCallback): () => void {
    if (!this.eventListeners.has(event)) {
      this.eventListeners.set(event, new Set());
    }
    this.eventListeners.get(event)!.add(callback);

    return () => {
      this.eventListeners.get(event)?.delete(callback);
    };
  }

  // High-level API methods
  public async getInitialConfig(): Promise<InitialConfig> {
    return this.sendRequest<InitialConfig>('getInitialConfig');
  }

  public async getConversionMatrix(): Promise<ConversionMatrixMap> {
    return this.sendRequest<ConversionMatrixMap>('getConversionMatrix');
  }

  public async getTargetFormats(extension: string): Promise<TargetOption[]> {
    return this.sendRequest<TargetOption[]>('getTargetFormats', { extension });
  }

  public async getMimeInfo(extension: string): Promise<{ extension: string; label: string; category: string; mimeType: string; description: string }> {
    return this.sendRequest('getMimeInfo', { extension });
  }

  public async selectFiles(): Promise<FileItem[]> {
    return this.sendRequest<FileItem[]>('selectFiles');
  }

  public async getClipboardFiles(): Promise<FileItem[]> {
    return this.sendRequest<FileItem[]>('getClipboardFiles');
  }

  public async inspectPaths(paths: string[]): Promise<FileItem[]> {
    return this.sendRequest<FileItem[]>('inspectPaths', paths);
  }

  public async selectFolder(initialPath?: string): Promise<string | null> {
    const res = await this.sendRequest<{ selectedFolder: string | null }>('selectFolder', { initialPath });
    return res.selectedFolder;
  }

  public async convert(request: ConversionRequest): Promise<ConversionResult> {
    return this.sendRequest<ConversionResult>('convert', request);
  }

  public async openDirectory(path: string): Promise<boolean> {
    return this.sendRequest<boolean>('openDirectory', { path });
  }

  public async openFile(path: string): Promise<boolean> {
    return this.sendRequest<boolean>('openFile', { path });
  }

  private simulateBrowserFallback(action: string, payload: any, resolve: (val: any) => void) {
    console.info(`[NativeBridge:BrowserFallback] Simulating action: ${action}`, payload);
    setTimeout(() => {
      switch (action.toLowerCase()) {
        case 'getinitialconfig':
          resolve({
            defaultOutputDirectory: 'C:\\Users\\User\\Downloads\\ConvertedFiles',
            appVersion: '1.0.0 (Browser Preview)',
            platform: 'Browser (Simulated)'
          });
          break;
        case 'getconversionmatrix':
          resolve(this.getMockMatrix());
          break;
        case 'selectfolder':
          resolve({ selectedFolder: 'C:\\Users\\User\\Documents\\Converted' });
          break;
        case 'convert': {
          let p = 0;
          const interval = setInterval(() => {
            p += 25;
            const progressReport: ProgressReport = {
              jobId: payload?.jobId || 'sim-1',
              percentage: p,
              currentFile: 'sample_document.' + (payload?.targetExtension || 'pdf'),
              statusText: `Converting to ${payload?.targetExtension?.toUpperCase() || 'PDF'} (${p}%)...`
            };
            const cbs = this.eventListeners.get('conversionProgress');
            cbs?.forEach((cb) => cb(progressReport));
            if (p >= 100) {
              clearInterval(interval);
              resolve({
                jobId: payload?.jobId || 'sim-1',
                success: true,
                message: `Successfully converted to ${payload?.targetExtension?.toUpperCase() || 'target'}.`,
                generatedFilePaths: [`C:\\Users\\User\\Downloads\\ConvertedFiles\\output.${payload?.targetExtension || 'pdf'}`],
                errors: [],
                durationMs: 460,
                processedFilesCount: 1
              });
            }
          }, 200);
          break;
        }
        case 'opendirectory':
        case 'openfile':
          resolve(true);
          break;
        default:
          resolve([]);
      }
    }, 100);
  }

  private getMockMatrix(): ConversionMatrixMap {
    return {
      pdf: [
        { extension: 'docx', label: 'Word (.docx)', category: 'document', description: 'Convert PDF to Word' },
        { extension: 'txt', label: 'Text (.txt)', category: 'document', description: 'Extract plain text' },
        { extension: 'md', label: 'Markdown (.md)', category: 'document', description: 'Extract to Markdown' },
        { extension: 'html', label: 'HTML (.html)', category: 'web', description: 'Export to HTML page' },
        { extension: 'png', label: 'PNG (.png)', category: 'image', description: 'Extract images as PNG' },
        { extension: 'jpg', label: 'JPEG (.jpg)', category: 'image', description: 'Extract images as JPG' }
      ],
      docx: [
        { extension: 'pdf', label: 'PDF (.pdf)', category: 'document', description: 'Render Word to PDF' },
        { extension: 'txt', label: 'Text (.txt)', category: 'document', description: 'Extract text' },
        { extension: 'md', label: 'Markdown (.md)', category: 'document', description: 'Convert to Markdown' },
        { extension: 'html', label: 'HTML (.html)', category: 'web', description: 'Generate HTML markup' }
      ],
      png: [
        { extension: 'jpg', label: 'JPEG (.jpg)', category: 'image', description: 'Convert to JPEG' },
        { extension: 'webp', label: 'WebP (.webp)', category: 'image', description: 'Convert to WebP' },
        { extension: 'bmp', label: 'Bitmap (.bmp)', category: 'image', description: 'Convert to BMP' },
        { extension: 'ico', label: 'Icon (.ico)', category: 'image', description: 'Convert to Windows Icon' },
        { extension: 'pdf', label: 'PDF (.pdf)', category: 'document', description: 'Embed into PDF' }
      ],
      csv: [
        { extension: 'json', label: 'JSON (.json)', category: 'data', description: 'Convert to JSON' },
        { extension: 'xlsx', label: 'Excel (.xlsx)', category: 'data', description: 'Export to Excel Workbook' },
        { extension: 'xml', label: 'XML (.xml)', category: 'data', description: 'Convert to XML' },
        { extension: 'html', label: 'HTML (.html)', category: 'web', description: 'Generate HTML table' },
        { extension: 'pdf', label: 'PDF (.pdf)', category: 'document', description: 'Render table to PDF' }
      ]
    };
  }
}

export const nativeBridge = new NativeBridgeService();
