const pad = document.getElementById('pad');
const copyBtn = document.getElementById('copy-btn');
const clearBtn = document.getElementById('clear-btn');
const settingsBtn = document.getElementById('settings-btn');
const closeBtn = document.getElementById('close-btn');
const titleBar = document.getElementById('title-bar');
const linkBar = document.getElementById('link-bar');
const measurer = document.getElementById('measurer');

const settingsPanel = document.getElementById('settings-panel');
const closeSettingsBtn = document.getElementById('close-settings-btn');
const autosaveToggle = document.getElementById('autosave-toggle');
const copyHotkeyInput = document.getElementById('copy-hotkey-input');
const closeHotkeyInput = document.getElementById('close-hotkey-input');

const MIN_HEIGHT = 300;
const MAX_HEIGHT = 800; // Will be capped by screen height in C#

// Local configuration state
let settings = {
  autosave: true,
  copyHotkey: 'CTRL+SHIFT+C',
  closeHotkey: 'ESCAPE'
};

// Helper to send message to WPF host
function postToHost(data) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(data);
  }
}

// Window Dragging
titleBar.addEventListener('mousedown', (e) => {
  const isButton = e.target.closest('.btn') || e.target.closest('input') || e.target.closest('.switch');
  if (!isButton) {
    postToHost('drag');
  }
});

// Close Application Button
closeBtn.addEventListener('click', () => {
  postToHost('close');
});

// Toggle settings modal
function toggleSettings() {
  const isOpen = settingsPanel.classList.toggle('open');
  if (isOpen) {
    settingsBtn.classList.add('active');
    document.getElementById('content').classList.add('settings-open');
    autosaveToggle.checked = settings.autosave;
    copyHotkeyInput.value = settings.copyHotkey || 'None';
    closeHotkeyInput.value = settings.closeHotkey || 'None';
  } else {
    settingsBtn.classList.remove('active');
    document.getElementById('content').classList.remove('settings-open');
    pad.focus();
  }
}

settingsBtn.addEventListener('click', toggleSettings);
closeSettingsBtn.addEventListener('click', toggleSettings);

autosaveToggle.addEventListener('change', () => {
  settings.autosave = autosaveToggle.checked;
  saveSettings();
  if (settings.autosave) {
    postToHost({ type: 'save_note', text: pad.value });
  } else {
    postToHost({ type: 'clear_note' });
  }
});

// Hotkey recording UI registration
let activeHotkeyRecordingInput = null;
let activeSettingKey = null;

function registerHotkeyInput(inputEl, settingKey) {
  inputEl.addEventListener('focus', () => {
    inputEl.value = 'Press keys...';
    activeHotkeyRecordingInput = inputEl;
    activeSettingKey = settingKey;
  });
  inputEl.addEventListener('blur', () => {
    inputEl.value = settings[settingKey] || 'None';
    activeHotkeyRecordingInput = null;
    activeSettingKey = null;
  });
}

registerHotkeyInput(copyHotkeyInput, 'copyHotkey');
registerHotkeyInput(closeHotkeyInput, 'closeHotkey');

// Keyboard event listener for shortcuts and recording
window.addEventListener('keydown', (e) => {
  // If we are actively recording a hotkey
  if (activeHotkeyRecordingInput) {
    e.preventDefault();
    e.stopPropagation();
    
    const mainKey = e.key.toUpperCase();
    if (mainKey === 'BACKSPACE' || mainKey === 'DELETE') {
      settings[activeSettingKey] = 'not set';
      activeHotkeyRecordingInput.value = 'not set';
      saveSettings();
      activeHotkeyRecordingInput.blur();
      return;
    }
    
    let keys = [];
    if (e.ctrlKey) keys.push('CTRL');
    if (e.shiftKey) keys.push('SHIFT');
    if (e.altKey) keys.push('ALT');
    
    if (mainKey !== 'CONTROL' && mainKey !== 'SHIFT' && mainKey !== 'ALT' && mainKey !== 'OS') {
      const keyName = mainKey === 'ESCAPE' ? 'ESCAPE' : mainKey;
      keys.push(keyName);
      const hotkeyStr = keys.join('+');
      
      settings[activeSettingKey] = hotkeyStr;
      activeHotkeyRecordingInput.value = hotkeyStr;
      saveSettings();
      activeHotkeyRecordingInput.blur();
    }
    return;
  }

  // Normal hotkeys evaluation
  const pressedStr = getPressedHotkeyString(e);
  if (pressedStr) {
    if (settings.copyHotkey !== 'not set' && pressedStr === settings.copyHotkey) {
      e.preventDefault();
      triggerCopy();
    } else if (settings.closeHotkey !== 'not set' && pressedStr === settings.closeHotkey) {
      e.preventDefault();
      if (settingsPanel.classList.contains('open')) {
        toggleSettings();
      } else {
        postToHost('close');
      }
    }
  }
});

function getPressedHotkeyString(e) {
  let keys = [];
  if (e.ctrlKey) keys.push('CTRL');
  if (e.shiftKey) keys.push('SHIFT');
  if (e.altKey) keys.push('ALT');
  
  const mainKey = e.key.toUpperCase();
  if (mainKey !== 'CONTROL' && mainKey !== 'SHIFT' && mainKey !== 'ALT' && mainKey !== 'OS') {
    const keyName = mainKey === 'ESCAPE' ? 'ESCAPE' : mainKey;
    keys.push(keyName);
    return keys.join('+');
  }
  return '';
}

function saveSettings() {
  postToHost({ type: 'save_settings', settings: settings });
}

// Clear all text
clearBtn.addEventListener('click', () => {
  pad.value = '';
  pad.focus();
  detectLinks();
  triggerResize();
  
  if (settings.autosave) {
    postToHost({ type: 'clear_note' });
  }
  
  clearBtn.classList.add('btn-success');
  const originalText = clearBtn.textContent;
  clearBtn.textContent = 'Cleared';
  setTimeout(() => {
    clearBtn.textContent = originalText;
    clearBtn.classList.remove('btn-success');
  }, 1000);
});

// Copy action with animation
function triggerCopy() {
  if (!pad.value) return;
  postToHost({ type: 'copy', text: pad.value });
  const originalText = copyBtn.textContent;
  copyBtn.textContent = 'Copied';
  copyBtn.classList.add('btn-success');
  setTimeout(() => {
    copyBtn.textContent = originalText;
    copyBtn.classList.remove('btn-success');
  }, 1000);
}

copyBtn.addEventListener('click', triggerCopy);

// Capture Tab key
pad.addEventListener('keydown', function (e) {
  if (e.key === 'Tab') {
    e.preventDefault();
    const start = this.selectionStart;
    const end = this.selectionEnd;
    this.value = this.value.substring(0, start) + "\t" + this.value.substring(end);
    this.selectionStart = this.selectionEnd = start + 1;
    triggerResize();
  }
});

// Detect links and resize on input
pad.addEventListener('input', () => {
  detectLinks();
  triggerResize();
  if (settings.autosave) {
    postToHost({ type: 'save_note', text: pad.value });
  }
});

function detectLinks() {
  const text = pad.value;
  const urlRegex = /(https?:\/\/[^\s]+)/g;
  
  const urlPositions = [];
  let match;
  while ((match = urlRegex.exec(text)) !== null) {
    urlPositions.push({
      url: match[0],
      start: match.index,
      end: match.index + match[0].length
    });
  }
  
  linkBar.innerHTML = '';
  if (urlPositions.length > 0) {
    linkBar.style.display = 'flex';
    urlPositions.forEach((posInfo, idx) => {
      const a = document.createElement('a');
      a.className = 'link-pill';
      a.href = posInfo.url;
      a.target = '_blank';
      
      let display = posInfo.url.replace(/https?:\/\/(www\.)?/, '');
      if (display.length > 20) display = display.substring(0, 18) + '...';
      a.textContent = (idx + 1) + ' · ' + display;
      
      let originalStart = 0;
      let originalEnd = 0;
      a.addEventListener('mouseenter', () => {
        originalStart = pad.selectionStart;
        originalEnd = pad.selectionEnd;
        pad.focus();
        pad.setSelectionRange(posInfo.start, posInfo.end);
      });
      a.addEventListener('mouseleave', () => {
        pad.setSelectionRange(originalStart, originalEnd);
      });
      
      a.addEventListener('click', (e) => {
        e.preventDefault();
        postToHost({ type: 'open_url', url: posInfo.url });
      });
      linkBar.appendChild(a);
    });
  } else {
    linkBar.style.display = 'none';
  }
}

// Dynamically resize window based on scrollHeight
function triggerResize() {
  measurer.textContent = pad.value + '\n';
  let textareaHeight = Math.max(200, measurer.scrollHeight);
  pad.style.height = textareaHeight + 'px';
  
  let targetHeight = document.getElementById('window-frame').offsetHeight;
  targetHeight = Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, targetHeight));
  
  postToHost({ type: 'resize', width: 600, height: targetHeight });
}

// WebView2 message listener from Host
if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', (e) => {
    const data = e.data;
    if (data && data.type === 'init') {
      if (data.settings) {
        settings = data.settings;
      }
      if (data.noteText !== undefined) {
        pad.value = data.noteText;
      }
      detectLinks();
      triggerResize();
    }
  });
}

// Startup call to signal host we are ready
setTimeout(() => {
  postToHost({ type: 'ready' });
}, 50);
