const pad = document.getElementById('pad');
const copyBtn = document.getElementById('copy-btn');
const closeBtn = document.getElementById('close-btn');
const titleBar = document.getElementById('title-bar');
const linkBar = document.getElementById('link-bar');

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
  if (e.target !== closeBtn && e.target !== copyBtn) {
    postToHost('drag');
  }
});

// Close Application
closeBtn.addEventListener('click', () => {
  postToHost('close');
});

// Copy all text
copyBtn.addEventListener('click', () => {
  if (!pad.value) return;
  postToHost({ type: 'copy', text: pad.value });
  const originalText = copyBtn.textContent;
  copyBtn.textContent = 'Copied';
  copyBtn.style.borderColor = 'var(--accent)';
  setTimeout(() => {
    copyBtn.textContent = originalText;
    copyBtn.style.borderColor = '';
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
  const matches = text.match(urlRegex) || [];
  
  // Deduplicate matches
  const uniqueUrls = [...new Set(matches)];
  
  linkBar.innerHTML = '';
  if (uniqueUrls.length > 0) {
    linkBar.style.display = 'flex';
    uniqueUrls.forEach(url => {
      const a = document.createElement('a');
      a.className = 'link-pill';
      a.href = url;
      a.target = '_blank';
      
      // Clean display name
      let display = url.replace(/https?:\/\/(www\.)?/, '');
      if (display.length > 20) display = display.substring(0, 18) + '...';
      a.textContent = display;
      
      a.addEventListener('click', (e) => {
        e.preventDefault();
        postToHost({ type: 'open_url', url: url });
      });
      linkBar.appendChild(a);
    });
  } else {
    linkBar.style.display = 'none';
  }
}

// Dynamically resize window based on scrollHeight
function triggerResize() {
  // Auto-grow the textarea
  pad.style.height = 'auto';
  pad.style.height = pad.scrollHeight + 'px';
  
  // Measure the true height of the container frame
  let targetHeight = document.getElementById('window-frame').offsetHeight;
  targetHeight = Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, targetHeight));
  
  postToHost({ type: 'resize', width: 600, height: targetHeight });
}

// Initial Call on startup
detectLinks();
setTimeout(triggerResize, 200);
