import React from 'react';
import { Sun, Moon, FolderOpen } from 'lucide-react';

interface HeaderProps {
  darkMode: boolean;
  onToggleDarkMode: () => void;
  outputDirectory: string;
  onChangeOutputDirectory: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  darkMode,
  onToggleDarkMode,
  outputDirectory,
  onChangeOutputDirectory,
}) => {
  return (
    <header className="app-header">
      <div className="header-brand">
        <div>
          <h1 className="brand-title">File converter</h1>
          <p className="brand-caption">Universal File & Document Processing Studio</p>
        </div>
      </div>

      <div className="header-actions">
        {/* Output Directory Picker */}
        <button
          type="button"
          className="folder-picker-btn"
          onClick={onChangeOutputDirectory}
          title={`Output Folder: ${outputDirectory || 'Not set'}`}
        >
          <FolderOpen size={16} className="btn-icon text-accent" />
          <span className="folder-label">
            <span className="folder-prefix">Save to: </span>
            <span className="folder-path">{outputDirectory || 'Select destination...'}</span>
          </span>
          <span className="change-badge">Change</span>
        </button>

        {/* Dark Mode Switch */}
        <div className="theme-toggle-container">
          <button
            type="button"
            className={`theme-toggle-btn ${darkMode ? 'dark-active' : 'light-active'}`}
            onClick={onToggleDarkMode}
            aria-label="Toggle Dark Mode"
            title={darkMode ? 'Switch to Light Mode' : 'Switch to Dark Mode'}
          >
            <div className="toggle-track">
              <Sun size={14} className="sun-icon" />
              <Moon size={14} className="moon-icon" />
              <div className="toggle-thumb" />
            </div>
            <span className="toggle-text">{darkMode ? 'Dark' : 'Light'}</span>
          </button>
        </div>
      </div>
    </header>
  );
};
