const pad = document.getElementById('pad');
const copyBtn = document.getElementById('copy-btn');
const clearBtn = document.getElementById('clear-btn');
const closeBtn = document.getElementById('close-btn');
const titleBar = document.getElementById('title-bar');
const linkBar = document.getElementById('link-bar');
const measurer = document.getElementById('measurer');

const MIN_HEIGHT = 300;
const MAX_HEIGHT = 800; // Will be capped by screen height in C#

// Helper to send message to WPF
function postToHost(data) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(data);
  }
}

// Window Dragging
titleBar.addEventListener('mousedown', (e) => {
  if (e.target !== closeBtn && e.target !== copyBtn && e.target !== clearBtn) {
    postToHost('drag');
  }
});

// Close Application
closeBtn.addEventListener('click', () => {
  postToHost('close');
});

// Clear all text
clearBtn.addEventListener('click', () => {
  pad.value = '';
  pad.focus();
  detectLinks();
  triggerResize();
});

// Copy all text
copyBtn.addEventListener('click', () => {
  if (!pad.value) return;
  postToHost({ type: 'copy', text: pad.value });
  const originalText = copyBtn.textContent;
  copyBtn.textContent = 'Copied';
  copyBtn.classList.add('btn-success');
  setTimeout(() => {
    copyBtn.textContent = originalText;
    copyBtn.classList.remove('btn-success');
  }, 1500);
});

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
      
      // Clean display name and add numbering
      let display = posInfo.url.replace(/https?:\/\/(www\.)?/, '');
      if (display.length > 20) display = display.substring(0, 18) + '...';
      a.textContent = (idx + 1) + ' · ' + display;
      
      // Hover highlighting in lila/purple using exact character coordinates
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
  // Use hidden measurer to calculate textarea height smoothly
  measurer.textContent = pad.value + '\n';
  let textareaHeight = Math.max(200, measurer.scrollHeight);
  pad.style.height = textareaHeight + 'px';
  
  // Measure the true height of the container frame
  let targetHeight = document.getElementById('window-frame').offsetHeight;
  targetHeight = Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, targetHeight));
  
  postToHost({ type: 'resize', width: 600, height: targetHeight });
}

// Initial Call on startup
detectLinks();
setTimeout(triggerResize, 200);
